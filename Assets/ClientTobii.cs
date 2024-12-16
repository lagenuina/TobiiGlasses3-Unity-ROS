using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;
using Unity.Robotics.ROSTCPConnector;
using Unity.Robotics.ROSTCPConnector.MessageGeneration;
using RosMessageTypes.Std;

public class ClientTobii : MonoBehaviour
{
    private TcpClient client;
    private NetworkStream stream;
    private Thread clientThread;
    private bool isRunning = true;

    // Server details
    private string serverIp = "127.0.0.1"; // IP address of the server
    private int serverPort = 5555;         // Port of the server
    private float retryDelay = 5f;         // Delay in seconds before retrying connection

    // Variables to display in Inspector
    [Header("Gaze Data")]
    private Vector2 gaze2D;
    [SerializeField] private Vector2 gazePixelCoords;
    private Vector3 gaze3D;

    [Header("Left Eye")]
    [SerializeField] private Vector3 leftEyeOrigin;
    [SerializeField] private Vector3 leftEyeDirection;
    [SerializeField] private float leftPupilDiameter;

    [Header("Right Eye")]
    [SerializeField] private Vector3 rightEyeOrigin;
    [SerializeField] private Vector3 rightEyeDirection;
    [SerializeField] private float rightPupilDiameter;

    // Thread-safe queue for main thread actions
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();

    void Start()
    {
        // Start the client thread to manage the connection
        clientThread = new Thread(ManageConnection);
        clientThread.IsBackground = true;
        clientThread.Start();

        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32MultiArrayMsg>("/gaze/pixel_coordinates");

        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32MultiArrayMsg>("/gaze/left_eye_origin");
        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32MultiArrayMsg>("/gaze/left_eye_direction");
        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32Msg>("/gaze/left_eye_pupil");

        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32MultiArrayMsg>("/gaze/right_eye_origin");
        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32MultiArrayMsg>("/gaze/right_eye_direction");
        ROSConnection.GetOrCreateInstance().RegisterPublisher<Float32Msg>("/gaze/right_eye_pupil");
    }

    private void ManageConnection()
    {
        while (isRunning)
        {
            if (client == null || !client.Connected)
            {
                ConnectToServer();
            }
            else
            {
                Thread.Sleep((int)(retryDelay * 1000)); // Prevent tight loop
            }
        }
    }

    private void ConnectToServer()
    {
        while (isRunning)
        {
            try
            {
                client = new TcpClient();
                Debug.Log($"Attempting to connect to server at {serverIp}:{serverPort}...");
                client.Connect(serverIp, serverPort);
                stream = client.GetStream();
                Debug.Log("Connected to server.");

                // Start listening for data
                ReceiveData();
                break; // Exit retry loop after successful connection
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Connection failed: {e.Message}. Retrying in {retryDelay} seconds...");
                Thread.Sleep((int)(retryDelay * 1000));
            }
        }
    }

    private void ReceiveData()
    {
        try
        {
            byte[] buffer = new byte[16384];
            StringBuilder incompleteData = new StringBuilder();

            while (isRunning && client.Connected)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);

                if (bytesRead == 0)
                {
                    Debug.LogWarning("Server has disconnected.");
                    break;
                }

                // Append received data to the incompleteData buffer
                string receivedData = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                incompleteData.Append(receivedData);

                // Process each complete JSON object
                string[] jsonObjects = incompleteData.ToString().Split('\n'); // Assuming each JSON ends with '\n'

                for (int i = 0; i < jsonObjects.Length - 1; i++) // Process all except the last, incomplete one
                {
                    string jsonData = jsonObjects[i].Trim();
                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        try
                        {
                            // Deserialize and process the JSON object
                            GazeData gazeData = JsonUtility.FromJson<GazeData>(jsonData);

                            // Update Inspector variables
                            gaze2D = new Vector2(gazeData.gaze2d[0], gazeData.gaze2d[1]);

                            gazePixelCoords = new Vector2(gazeData.gazePixelCoords[0], gazeData.gazePixelCoords[1]);
                            PublishToROSVector2(gazePixelCoords, "/gaze/pixel_coordinates");

                            gaze3D = new Vector3(gazeData.gaze3d[0], gazeData.gaze3d[1], gazeData.gaze3d[2]);

                            leftEyeOrigin = new Vector3(gazeData.eyeleft.gazeorigin[0], gazeData.eyeleft.gazeorigin[1], gazeData.eyeleft.gazeorigin[2]);
                            PublishToROSVector3(leftEyeOrigin, "/gaze/left_eye_origin");

                            leftEyeDirection = new Vector3(gazeData.eyeleft.gazedirection[0], gazeData.eyeleft.gazedirection[1], gazeData.eyeleft.gazedirection[2]);
                            PublishToROSVector3(leftEyeDirection, "/gaze/left_eye_direction");

                            leftPupilDiameter = gazeData.eyeleft.pupildiameter;
                            PublishToROSFloat32(leftPupilDiameter, "/gaze/left_eye_pupil");

                            rightEyeOrigin = new Vector3(gazeData.eyeright.gazeorigin[0], gazeData.eyeright.gazeorigin[1], gazeData.eyeright.gazeorigin[2]);
                            PublishToROSVector3(rightEyeOrigin, "/gaze/right_eye_origin");

                            rightEyeDirection = new Vector3(gazeData.eyeright.gazedirection[0], gazeData.eyeright.gazedirection[1], gazeData.eyeright.gazedirection[2]);
                            PublishToROSVector3(rightEyeDirection, "/gaze/right_eye_direction");

                            rightPupilDiameter = gazeData.eyeright.pupildiameter;
                            PublishToROSFloat32(rightPupilDiameter, "/gaze/right_eye_pupil");

                        }
                        catch (Exception)
                        {
                            
                        }
                    }
                }
                
                // Keep the last incomplete JSON string for the next iteration
                incompleteData.Clear();
                incompleteData.Append(jsonObjects[jsonObjects.Length - 1]);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error receiving data: {e.Message}");
        }
        finally
        {
            Disconnect();
        }
    }

    private void PublishToROSFloat32(float data, string topicName){

        Float32Msg floatMsg = new Float32Msg {data = data};
        ROSConnection.GetOrCreateInstance().Publish(topicName, floatMsg);
    }

    private void PublishToROSVector3(Vector3 data, string topicName){

        Float32MultiArrayMsg vector2Msg = new Float32MultiArrayMsg
        {
            data = new float[] { data.x, data.y, data.z }
        };

        // Publish to the topic
        ROSConnection.GetOrCreateInstance().Publish(topicName, vector2Msg);

    }

    private void PublishToROSVector2(Vector2 data, string topicName){

        Float32MultiArrayMsg vector2Msg = new Float32MultiArrayMsg
        {
            data = new float[] { data.x, data.y }
        };

        // Publish to the topic
        ROSConnection.GetOrCreateInstance().Publish(topicName, vector2Msg);

    }

    private void RunOnMainThread(Action action)
    {
        mainThreadActions.Enqueue(action);
    }

    void Update()
    {
        // Execute all queued actions on the main thread
        while (mainThreadActions.TryDequeue(out var action))
        {
            action?.Invoke();
        }
    }

    private void Disconnect()
    {
        if (stream != null) stream.Close();
        if (client != null) client.Close();
        client = null; // Set to null to trigger reconnection
        Debug.Log("Disconnected from server.");
    }

    void OnApplicationQuit()
    {
        isRunning = false;
        if (stream != null) stream.Close();
        if (client != null) client.Close();
        if (clientThread != null && clientThread.IsAlive)
        {
            clientThread.Abort();
        }
    }
}

[Serializable]
public class GazeData
{
    public float[] gaze2d;
    public float[] gazePixelCoords;
    public float[] gaze3d;
    public EyeData eyeleft;
    public EyeData eyeright;

    [Serializable]
    public class EyeData
    {
        public float[] gazeorigin;
        public float[] gazedirection;
        public float pupildiameter;
    }
}
