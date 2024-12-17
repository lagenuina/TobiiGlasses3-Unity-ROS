# Tobii Glasses 3 Unity - ROS Integration

This Unity project receives data from Tobii Glasses 3. 
It displays **gaze information** *(pixel coordinates, eyes origin, direction and pupils diameter)* and **camera feed** with gaze fixation.
This data is sent to **ROS** with [ROS-TCP-Connector](https://github.com/Unity-Technologies/ROS-TCP-Connector).

------

## Instructions
1. Download Unity 2021.3.42
2. Clone the [Tobii repository](https://github.com/lagenuina/TobiiGlasses/tree/main?tab=readme-ov-file) and follow the instructions to set up the project. This repository is **required**: it uses the Tobii Glasses 3 Python library to handle communication with the Glasses3 WebSocket API. It acts as a server for streaming real-time data from Tobii Glasses 3 to Unity.
3. Clone this repository:
   ```
   git clone https://github.com/lagenuina/UnityTobiiGlasses3.git
   ```
4. In Unity, open the project in the Unity folder. Navigate to Scenes and select "Tobii Glasses"
5. Insert the IP address of your ROS machine under *Robotics -> ROS Settings -> ROS IP Address*
6. Play the Unity project
7. Follow instructions [here](https://github.com/lagenuina/TobiiGlasses/tree/main?tab=readme-ov-file) to run the *sendgazedata.py* script.
