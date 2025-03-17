using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System;
using Fusion;
using UnityEngine.XR;
using System.Net.NetworkInformation;

public class SensorDataStreamer : MonoBehaviour
{
    private float deltaTime = 0.0f; // used to calculate FPS

    // TCP-related fields for managing server-client communication
    private TcpListener tcpListener;
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread listenerThread;

    [SerializeField]
    private string localIP = "192.168.137.15"; // IP Address that will be used to start a TCP connection

    [SerializeField]
    private int port = 8888; // Make sure this port is different from audio port

    public Transform headsetTransform;  // Assign the headset (camera) transform here
    private NetworkRunner networkRunner;  // Fusion's NetworkRunner

    private bool isStreamingStarted = false;  // Ensure logging only starts after connection

    // Hand tracking objects
    private OVRHand leftHand;
    private OVRSkeleton leftHandSkeleton;
    private OVRHand rightHand;
    private OVRSkeleton rightHandSkeleton;

    // Variables to hold the sensor data
    private string headsetPositionData = "";
    private string batteryStatusData = "";
    private string eyeTrackingData = "";
    private string controllerTrackingData = "";
    private string leftHandTrackingData = "";
    private string rightHandTrackingData = "";

    [SerializeField]
    private float communicationFrequency = 1f;  // Frequency at which data is streamed over TCP

    [SerializeField]
    private float samplingFrequency = 1f; // Frequency the data is sampled

    private float nextStreamTime = 0f;  // Time for the next stream
    private float nextSampleTime = 0f;  // Time for next sampling

    private float fps; // Frames per second of the application
    private string timestamp; // Timestamp at which data was sampled

    // Start is called before the first frame update
    void Start()
    {
        // Assume the NetworkRunner is assigned or obtained dynamically in your scene
        networkRunner = FindObjectOfType<NetworkRunner>();
        if (networkRunner == null)
        {
            Debug.LogError("NetworkRunner not found in the scene.");
            return;
        }

        // Find the left hand and right OVRSkeleton and OVRHand components
        leftHand = GameObject.FindWithTag("LeftHand").GetComponent<OVRHand>();
        leftHandSkeleton = GameObject.FindWithTag("LeftHand").GetComponent<OVRSkeleton>();
        rightHand = GameObject.FindWithTag("RightHand").GetComponent<OVRHand>();
        rightHandSkeleton = GameObject.FindWithTag("RightHand").GetComponent<OVRSkeleton>();

        if (leftHand != null && leftHandSkeleton != null && rightHand != null && rightHandSkeleton != null)
        {
            Debug.Log("Found all OVRHand and OVRSkeleton components!");
        }
        else
        {
            Debug.Log("Missing an OVRHand or OVRSkeleton component!");
        }

        // Set up TCP listener on the specified IP and port
        tcpListener = new TcpListener(IPAddress.Parse(localIP), port);
        tcpListener.Start();
        Debug.Log("Server started and listening on IP: " + localIP + " Port: " + port);

        // Start listening for incoming connections on a separate thread
        listenerThread = new Thread(ListenForClients);
        listenerThread.Start();
    }

    // Update is called once per frame
    void Update()
    {
        deltaTime += (Time.deltaTime - deltaTime) * 0.1f; // Smooth out the FPS value

        // Start streaming once tcp client has connected
        if (tcpClient != null && tcpClient.Connected && !isStreamingStarted)
        {
            isStreamingStarted = true;
        }

        // If a client is connected, stream sensor data
        if (tcpClient != null && tcpClient.Connected && isStreamingStarted)
        {
            float time = Time.time;
            fps = 1.0f / deltaTime;
            timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            // Check if its time to sample data
            if (time >= nextSampleTime)
            {
                GetBatteryStatus();
                GetHeadsetPosition();
                GetEyeTrackingData();
                GetControllerTrackingData();
                GetLeftHandTrackingData();
                GetRightHandTrackingData();
                nextSampleTime = Time.time + samplingFrequency;
            }

            // Check if its time to stream data
            if (time >= nextStreamTime)
            {
                StreamSensorData();
                ClearData();
                nextStreamTime = Time.time + communicationFrequency;
            }
        }
    }

    void ListenForClients()
    {
        // Wait for the Network Runner to be running and valid before listening for clients
        while (!(networkRunner.IsRunning && networkRunner.SessionInfo.IsValid))
        {
            // Do nothing
        }

        while (true)
        {
            try
            {
                // Wait for a client to connect
                tcpClient = tcpListener.AcceptTcpClient();
                Debug.Log("Client connected!");


                // Continuously send data to the client
                while (tcpClient.Connected)
                {
                    Thread.Sleep(500); // Small sleep to avoid busy-waiting
                }

                // Close the connection when done
                tcpClient.Close();
                tcpClient = null;  // Ensure the previous client is cleared
                Debug.Log("Client disconnected.");
            }
            catch (Exception ex)
            {
                Debug.LogError("Error while listening for client: " + ex.Message);
            }
        }
    }

    // Gets the current headset battery status and stores it in the corresponding instance variable
    private void GetBatteryStatus()
    {
        // Store data
        string battery_status = $"Battery Status - {SystemInfo.batteryLevel * 100}%";

        batteryStatusData += $"{timestamp}, FPS = {fps}: {battery_status}\n";
    }

    // Gets the current headset position and stores it in the corresponding instance variable
    private void GetHeadsetPosition()
    {
        if (headsetTransform != null)
        {
            Vector3 position = headsetTransform.position;

            // Store data
            string headset_position = $"Headset Position - X: {position.x}, Y: {position.y}, Z: {position.z}";

            headsetPositionData += $"{timestamp}, FPS = {fps}: {headset_position}\n";

        }
        else
        {
            Debug.LogWarning("Headset Transform is not assigned!");
        }
    }

    // Gets the current controller tracking data (position, rotation) and stores it in the corresponding instance variable
    private void GetControllerTrackingData()
    {
        // Get hand tracking data from XR Input Subsystem
        Vector3 leftHandPosition = Vector3.zero;
        Vector3 rightHandPosition = Vector3.zero;
        Quaternion leftHandRotation = Quaternion.identity;
        Quaternion rightHandRotation = Quaternion.identity;

        // Get positions and rotations for both hands
        var nodeStates = new System.Collections.Generic.List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (var nodeState in nodeStates)
        {
            if (nodeState.nodeType == XRNode.LeftHand)
            {
                nodeState.TryGetPosition(out leftHandPosition);
                nodeState.TryGetRotation(out leftHandRotation);
            }
            if (nodeState.nodeType == XRNode.RightHand)
            {
                nodeState.TryGetPosition(out rightHandPosition);
                nodeState.TryGetRotation(out rightHandRotation);
            }
        }

        // Store data
        string controllerData = $"Left Controller Position - X: {leftHandPosition.x}, Y: {leftHandPosition.y}, Z: {leftHandPosition.z}, Rotation: {leftHandRotation.eulerAngles} / " +
                          $"Right Controller Position - X: {rightHandPosition.x}, Y: {rightHandPosition.y}, Z: {rightHandPosition.z}, Rotation: {rightHandRotation.eulerAngles}";

        controllerTrackingData += $"{timestamp}, FPS = {fps}: {controllerData}\n";
    }

    // Gets the current eye tracking data (position, gaze direction) and stores it in the corresponding instance variable
    private void GetEyeTrackingData()
    {
        // Get eye-tracking data from the XR Input Subsystem
        Vector3 leftEyePosition = Vector3.zero;
        Vector3 rightEyePosition = Vector3.zero;
        Vector3 leftGazeDirection = Vector3.zero;
        Vector3 rightGazeDirection = Vector3.zero;

        // Get eye position and gaze direction for both eyes
        var nodeStates = new System.Collections.Generic.List<XRNodeState>();
        InputTracking.GetNodeStates(nodeStates);

        foreach (var nodeState in nodeStates)
        {
            if (nodeState.nodeType == XRNode.LeftEye)
            {
                nodeState.TryGetPosition(out leftEyePosition);
                nodeState.TryGetRotation(out Quaternion leftRotation);
                leftGazeDirection = leftRotation * Vector3.forward;
            }
            if (nodeState.nodeType == XRNode.RightEye)
            {
                nodeState.TryGetPosition(out rightEyePosition);
                nodeState.TryGetRotation(out Quaternion rightRotation);
                rightGazeDirection = rightRotation * Vector3.forward;
            }
        }

        // Store data
        string eyeData = $"Left Eye Position - X: {leftEyePosition.x}, Y: {leftEyePosition.y}, Z: {leftEyePosition.z}, Gaze: {leftGazeDirection} / " +
                         $"Right Eye Position - X: {rightEyePosition.x}, Y: {rightEyePosition.y}, Z: {rightEyePosition.z}, Gaze: {rightGazeDirection}";

        eyeTrackingData += $"{timestamp}, FPS = {fps}: {eyeData}\n";
    }

    // Get left hand tracking data and stores it in the corresponding instance variable 
    private void GetLeftHandTrackingData()
    {
        if (leftHand && leftHand.IsTracked && leftHandSkeleton)
        {
            string handData = "";

            foreach (var bone in leftHandSkeleton.Bones)
            {
                handData += $"{leftHandSkeleton.GetSkeletonType()}: boneId -> {bone.Id} pos -> {bone.Transform.position} rotation -> {bone.Transform.rotation} |";
            }

            // Store data
            leftHandTrackingData += $"{timestamp}, FPS = {fps}: {handData}\n";
        }
    }

    // Get right hand tracking data and stores it in the corresponding instance variable 
    private void GetRightHandTrackingData()
    {
        if (rightHand && rightHand.IsTracked && rightHandSkeleton)
        {
            string handData = "";

            foreach (var bone in rightHandSkeleton.Bones)
            {
                handData += $"{rightHandSkeleton.GetSkeletonType()}: boneId -> {bone.Id} pos -> {bone.Transform.position} rotation -> {bone.Transform.rotation} |";
            }

            // Store data
            rightHandTrackingData += $"{timestamp}, FPS = {fps}: {handData}\n";
        }
    }

    // Stream the sensor data over TCP
    private void StreamSensorData()
    {
        // Prepare data for TCP message
        string message = $"^HeadsetLocation;{headsetPositionData}@@BatteryStatus;{batteryStatusData}@@EyeTracking;{eyeTrackingData}@@ControllerTracking;{controllerTrackingData}@@LeftHandTracking;{leftHandTrackingData}@@RightHandTracking;{rightHandTrackingData}@@^";

        // Send data over TCP
        networkStream = tcpClient.GetStream();
        byte[] data = Encoding.UTF8.GetBytes(message);
        networkStream.Write(data, 0, data.Length);

        Debug.Log("bytes sent: " + data.Length);
    }

    // Clear the sensor data
    private void ClearData()
    {
        headsetPositionData = "";
        batteryStatusData = "";
        eyeTrackingData = "";
        controllerTrackingData = "";
        leftHandTrackingData = "";
        rightHandTrackingData = "";
    }

    void OnApplicationQuit()
    {
        // Close the TCP listener and any active connections
        Debug.Log("Application is quitting. Closing connections...");
        tcpListener.Stop();

        // Close the client connection if any
        if (tcpClient != null && tcpClient.Connected)
        {
            tcpClient.Close();
            tcpClient = null;
        }

        // Abort the listener thread
        if (listenerThread != null && listenerThread.IsAlive)
        {
            listenerThread.Abort();
        }
    }
}
