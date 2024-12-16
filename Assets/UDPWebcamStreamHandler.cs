using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;
using UnityEditor;

public class UDPWebcamStreamHandler : MonoBehaviour
{
	[TextArea]
	public string debugInfo;

	private int currentFrame = -1;
	private int frameProgress = 0;
	public int port = 8080;
	public int inputWidth = 4096;
	public int inputHeight = 2048;

	public RenderTexture outputTexture;
	public int outWidth = 4096;
	public int outHeight = 2048;
	private Texture2D bufferTexture;
	private int xOffset;
	private int yOffset;
	private bool needsBuffer;

	private byte[] frameData;


	private bool readyToDraw = false;
	private bool testImageDrawn = false;
	UdpClient socket;

	Texture2D tex;
	public float sampleFrequency = 2f;
	private float lastSample = 0;
	private int fps = 10;
	private float pl = 2.5f;
	private int dropRate = 0;
	private int debDropCount = 0;
	private int debFrameCount = 0;
	void updateDebugInfo()
    {
		if (Time.time - lastSample >= sampleFrequency)
        {
			float deltaT = Time.time - lastSample;

			fps = (int)(debFrameCount / deltaT);
			dropRate = (((debDropCount) + debFrameCount)!=0)?(int)(((debDropCount) / (float)((debDropCount) + debFrameCount))*100):0;
			lastSample = Time.time;
			debFrameCount = 0;
			debDropCount = 0;

		}
		debugInfo = "FPS: " + fps + "\n" +
					"DR: " + dropRate + "%\n" +
					"Frame: " + currentFrame;
    }
	void Start()
	{
		socket = new UdpClient(port); // `new UdpClient()` to auto-pick port
									  // schedule the first receive operation:
		socket.BeginReceive(new AsyncCallback(OnUdpData), socket);


		// sending data (for the sake of simplicity, back to ourselves):
		//IPEndPoint target = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 5394);
		// send a couple of sample messages:

		//Console.ReadKey();
		tex = new Texture2D(inputWidth, inputHeight, TextureFormat.RGB24, false);
		bufferTexture = new Texture2D(outWidth, outHeight, TextureFormat.RGB24, false);
		//Only use buffer texture if resolutions dont match
		needsBuffer = !(inputWidth == outWidth && inputHeight == outHeight);

		//Place image in the center of the output texture
		if (needsBuffer)
		{
			xOffset = (outWidth - inputWidth) / 2;
			yOffset = (outHeight - inputHeight) / 2;
		}

	}

	// Update is called once per frame
	void Update()
	{
		if (readyToDraw == true)
		{
			readyToDraw = false;
			FrameRecived(frameData);
			socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
			debFrameCount++;
		}
		updateDebugInfo();
	}

	void FrameRecived(byte[] data)
	{
		//Debug.Log(data.Length);
		if (!testImageDrawn)
		{
			File.WriteAllBytes("outputtest.jpg", data);
			testImageDrawn = true;
		}
		tex.LoadImage(data);
		tex.Apply();

		if (needsBuffer)
		{
			if (tex.width > outWidth)
			{
				xOffset = (tex.width - outWidth) / 2;
				yOffset = (outHeight - tex.height) / 2;
				Graphics.CopyTexture(tex, 0, 0, xOffset, 0, tex.width - (xOffset * 2), tex.height, bufferTexture, 0, 0, 0, yOffset);
				Graphics.Blit(bufferTexture, outputTexture);
			}
			else
			{
				xOffset = (outWidth - tex.width) / 2;
				yOffset = (outHeight - tex.height) / 2;
				Graphics.CopyTexture(tex, 0, 0, 0, 0, tex.width, tex.height, bufferTexture, 0, 0, xOffset, yOffset);
				Graphics.Blit(bufferTexture, outputTexture);
				//Graphics.CopyTexture(tex, 0, 0, 0, 0, tex.width, tex.height, bufferTexture, 0, 0, xOffset, yOffset);
			}


		}
		else
		{
			Graphics.Blit(tex, outputTexture);
		}

		//Graphics.Blit(tex, rt);
	}

	void OnUdpData(System.IAsyncResult result)
	{
		
		// this is what had been passed into BeginReceive as the second parameter:
		//UdpClient socket = result.AsyncState as UdpClient;
		// points towards whoever had sent the message:
		IPEndPoint source = new IPEndPoint(0, 0);
		// get the actual message and fill out the source:
		byte[] message = socket.EndReceive(result, ref source);
		// do what you'd like with `message` here:
		

		int nullInd = 0;
		while (nullInd < message.Length)
		{
			if (message[nullInd] == 0)
			{
				break;
			}
			nullInd++;
		}
		byte[] header = new byte[nullInd];
		for (int i = 0; i < nullInd; i++)
		{
			header[i] = message[i];
		}
		string headerString = Encoding.Default.GetString(header);
		string[] args = headerString.Split('_');
		//Debug.Log("Got " + message.Length + " : " + headerString);

		int[] argsInt = new int[args.Length];
		for (int i = 0; i < args.Length; i++)
		{
			argsInt[i] = Int32.Parse(args[i]);
		}
		//Debug.Log("Put " + argsInt[4] + " bytes into dest at " + argsInt[3]);
		
		if (argsInt[0] == this.currentFrame)
		{
			this.frameProgress++;
			Buffer.BlockCopy(message, nullInd + 1, frameData, argsInt[3], argsInt[4]);
			//Debug.Log("Put " + argsInt[4] + " bytes into dest at " + argsInt[3]);
			if (this.frameProgress == argsInt[2])
			{
				//Debug.Log("Frame Finished: " + this.currentFrame);
				readyToDraw = true;
				//FrameRecived(frameData);
				this.frameProgress = 0;
				//this.currentFrame++;
			}
			else
			{
				socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
			}
		}
		else if (argsInt[0] > this.currentFrame || this.currentFrame - argsInt[0] > 60) //If more than 60 frames behind assume a restart occured
		{
			if (this.frameProgress != 0)
            {
				debDropCount++;
			}
			
			this.currentFrame = argsInt[0];
			this.frameProgress = 1;

			frameData = new byte[argsInt[5]];
			Buffer.BlockCopy(message, nullInd + 1, frameData, argsInt[3], argsInt[4]);
			//Debug.Log("Put " + argsInt[4] + " bytes into dest at " + argsInt[3]);
			socket.BeginReceive(new AsyncCallback(OnUdpData), socket);


			//socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
		} else
        {
			
			//Oboslete packet
			socket.BeginReceive(new AsyncCallback(OnUdpData), socket);
		}
		// schedule the next receive operation once reading is done:



	}

}