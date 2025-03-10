import threading
import socket
import os
from datetime import datetime
import sys
import wave


# TCP Configuration Parameters
# IP_ADDRESSES = ['192.168.137.20', '192.168.137.181', '192.168.137.76', '192.168.137.78'] # For testing on multiple headsets
IP_ADDRESSES = ['127.0.0.1'] # For testing on a single headset
PORT = 8888         # Port must match the one used by SensorDataStreamer.cs and be different from audio port
AUDIO_PORT = 8889   # Port must match the one used by AudioDataStreamer.cs and be different from port
BUFFER_SIZE = 16834 # Amount of data in bytes the TCP client will receive

# Set up WAV file parameters
SAMPLE_RATE = 44100  # 44.1kHz, typical for audio
NUM_CHANNELS = 1     # Mono audio
SAMPLE_WIDTH = 2     # 16-bit samples

# Directory to store log files
LOG_DIR = "unity_logs"


def log_data(log_file, data):
    """Appends data to a log file."""
    with open(log_file, "a") as file:
        file.write(data)

def audio_tcp_streaming_client(ip_address, port, player_id, buffer_size, time_joined, sample_rate, num_channels, sample_width):
    '''TCP client for streaming audio.'''
    global LOG_DIR
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM) # create a socket object for TCP connection
    client_socket.connect((ip_address, port))   # connect to the Unity TCP Server
    print(f'Connected to {ip_address}:{port}')
    
    audio_filepath = os.path.join(LOG_DIR, player_id, time_joined, 'audio.wav')

    print(f"Being audio streaming on IP Address: {ip} and Port: {port}")

    # open the WAV file to write audio data
    with wave.open(audio_filepath, 'wb') as wav_file:
        # set the parameters for the WAV file
        wav_file.setnchannels(num_channels)
        wav_file.setsampwidth(sample_width)
        wav_file.setframerate(sample_rate)

        while True:
            # receive the data from the server
            data = client_socket.recv(buffer_size)

            # print(f"Received: {sys.getsizeof(data)} bytes")

            # break out of while loop when TCP connection has terminated
            if not data:
                break
            
            # ensure data is in 16-bit PCM format, here assuming it's already in the right format
            # write the received audio data to the WAV file
            wav_file.writeframes(data)

        client_socket.close() # close the connection when done
        print(f"Connection to IP Address: {ip} with Port: {port} is now closed.")

def tcp_streaming_client(ip_address, port, player_id, buffer_size, time_joined):
    '''TCP client for streaming the non-audio sensor data.'''
    global LOG_DIR
    client_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM) # create a socket object for TCP connection
    client_socket.connect((ip_address, port))   # connect to the Unity TCP Server
    print(f'Connected to {ip_address}:{port}')
    
    decoded_data = ''

    while True:
        data = client_socket.recv(buffer_size)

        # break out of while loop when TCP connection has terminated
        if not data:
            break

        # print(f"Received: {sys.getsizeof(data)} bytes from {player_id} with IP: {ip}")

        decoded_data += data.decode('utf-8')

        # print(decoded_data)

        # messages start with ^ and end with ^
        lindex = decoded_data.find('^')
        rindex = decoded_data.rfind('^')

        # if both ^ can be found, it means the complete message was received
        if lindex == 0 and rindex != lindex:
            decoded_data = decoded_data[lindex + 1:rindex]

            while decoded_data:
                # extract data type and sensor data
                data_type = decoded_data[:decoded_data.find(';')]
                sensor_data = decoded_data[decoded_data.find(';') + 1:decoded_data.find('@@')]
                decoded_data = decoded_data[decoded_data.find('@@') + 2:]
            
                if data_type == "HeadsetLocation":
                    # HeadsetLocation message: HeadsetLocation;headset location data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'headset_location_log.txt'), sensor_data)
                    print(f"Logged headset location data from {player_id}")

                elif data_type == "BatteryStatus":
                    # BatteryStatus message: BatteryStatus;battery level data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'battery_status_log.txt'), sensor_data)
                    print(f"Logged battery status data from {player_id}")

                elif data_type == "EyeTracking":
                    # EyeTracking message: EyeTracking;eye tracking data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'eye_tracking_log.txt'), sensor_data)
                    print(f"Logged eye tracking data from {player_id}")

                elif data_type == "ControllerTracking":
                    # ControllerTracking message: ControllerTracking;controller tracking data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'controller_tracking_log.txt'), sensor_data)
                    print(f"Logged controller tracking data from {player_id}")

                elif data_type == "LeftHandTracking":
                    # LeftHandTracking message: LeftHandTracking;left hand tracking data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'left_hand_tracking_log.txt'), sensor_data)
                    print(f"Logged left hand tracking data from {player_id}")

                elif data_type == "RightHandTracking":
                    # RightHandTracking message: RightHandTracking;timestamp;right hand tracking data
                    log_data(os.path.join(LOG_DIR, player_id, time_joined, 'right_hand_tracking_log.txt'), sensor_data)
                    print(f"Logged right hand tracking data from {player_id}")
            

        
    client_socket.close() # close the connection when done
    print(f"Connection to IP Address: {ip} with Port: {port} is now closed.")

    


if __name__ == "__main__":
    # create directory if it doesn't exist
    if not os.path.exists(LOG_DIR):
        os.makedirs(LOG_DIR)
    
    threads = []    # list to store all threads
    player_num = 1  # for creating Player ID's with unique numbers

    # initialize the threads
    for ip in IP_ADDRESSES:
        player_id = f'Player_{player_num}'  # create a unique Player ID
        player_num += 1
        time_joined = datetime.now().strftime("%Y-%m-%d_%H-%M-%S")  # use the time the thread is created

        # create a directory using the player ID and when the thread is created
        if not os.path.exists(os.path.join(LOG_DIR, player_id, time_joined)):
            os.makedirs(os.path.join(LOG_DIR, player_id, time_joined))
        
        # create thread for streaming non-audio sensor data
        thread = threading.Thread(target=tcp_streaming_client, args=(ip, PORT, player_id, BUFFER_SIZE, time_joined))
        
        # create thread for streaming audio data
        thread_audio = threading.Thread(target=audio_tcp_streaming_client, args=(ip, AUDIO_PORT, player_id, BUFFER_SIZE, time_joined, SAMPLE_RATE, NUM_CHANNELS, SAMPLE_WIDTH))
        
        threads.append(thread)
        threads.append(thread_audio)
    
    # start the threads
    for thread in threads:
        thread.start()

    # wait for threads to complete
    for thread in threads:
        thread.join()
    

    
