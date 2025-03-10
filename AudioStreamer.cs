using System;
using System.IO;
using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using Fusion;
using System.Net.NetworkInformation;

public class AudioStreamer : MonoBehaviour
{
    // TCP-related fields for managing server-client communication
    private TcpListener tcpListener;
    private TcpClient tcpClient;
    private NetworkStream networkStream;
    private Thread listenerThread;

    private Queue<float[]> audioDataQueue = new Queue<float[]>();   // Hold audio data before streaming over TCP

    AudioClip mic;
    int lastPos, pos; // Keep track of the position of the current sample in the AudioClip
    int channels = 1; // Number of channels of the audio

    [SerializeField]
    private string localIP = "192.168.137.15"; // IP Address that will be used to start a TCP connection

    [SerializeField]
    private int port = 8889; // Make sure this port is different from the one used to stream non-audio sensor data

    private NetworkRunner networkRunner;  // Fusion's NetworkRunner

    void Start()
    {
        // Assume the NetworkRunner is assigned or obtained dynamically in your scene
        networkRunner = FindObjectOfType<NetworkRunner>();
        if (networkRunner == null)
        {
            Debug.LogError("NetworkRunner not found in the scene.");
            return;
        }

        mic = Microphone.Start(null, true, 1, 44100); // Initialize the microphone

        // Set up TCP listener on the specified IP and port
        tcpListener = new TcpListener(IPAddress.Parse(localIP), port);
        tcpListener.Start();
        Debug.Log("Server started and listening on IP: " + localIP + " Port: " + port);

        // Start listening for incoming connections on a separate thread
        listenerThread = new Thread(ListenForClients);
        listenerThread.Start();
    }

    void Update()
    {
        if ((pos = Microphone.GetPosition(null)) > 0)
        {
            // Handle wrap-around of microphone data
            if (lastPos > pos)
            {
                lastPos = 0;
            }

            if (pos - lastPos > 0)
            {
                // Allocate the space for the new sample
                int len = (pos - lastPos) * channels;
                float[] samples = new float[len];
                mic.GetData(samples, lastPos);

                // Add audio data to queue (from main thread)
                lock (audioDataQueue)
                {
                    audioDataQueue.Enqueue(samples);
                }
                lastPos = pos;
            }
        }

        // Process audio data from queue (from main thread)
        if (audioDataQueue.Count > 0)
        {
            float[] samples;
            lock (audioDataQueue)
            {
                samples = audioDataQueue.Dequeue();
            }

            StreamAudioData(samples);
        }
    }

    void ListenForClients()
    {
        // Wait for the Network Runner to be running and valid before listening for clients
        while (!(networkRunner.IsRunning && networkRunner.SessionInfo.IsValid))
        {
            // do nothing
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

    void StreamAudioData(float[] samples)
    {
        if (samples == null || samples.Length == 0)
        {
            return;
        }

        byte[] pcmData = ConvertToPCM(samples);
        // Debug.Log("PCM data length: " + pcmData.Length);

        // If there's a TCP client that is connected, stream the audio data over TCP
        if (tcpClient != null && tcpClient.Connected)
        {
            networkStream = tcpClient.GetStream();
            networkStream.Write(pcmData, 0, pcmData.Length);
        }
    }

    // Convert the float[] audio data to 16-bit PCM byte array
    private byte[] ConvertToPCM(float[] samples)
    {
        MemoryStream stream = new MemoryStream();

        // Convert each sample to a 16-bit signed integer
        for (int i = 0; i < samples.Length; i++)
        {
            short intSample = (short)(samples[i] * short.MaxValue);  // Convert to 16-bit PCM
            byte[] byteArr = BitConverter.GetBytes(intSample);
            stream.Write(byteArr, 0, byteArr.Length);  // Write the byte data to the stream
        }

        return stream.ToArray();  // Return the final byte array
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
