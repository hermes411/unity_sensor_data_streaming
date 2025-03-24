import matplotlib.pyplot as plt
import os

CHANGING_SAMP_FREQ_DIR = ['unity_logs_1_1', 'unity_logs_2_2', 'unity_logs_5_5', 'unity_logs_10_10']
CHANGING_COMM_FREQ_DIR = ['unity_logs_1_1', 'unity_logs_1_2', 'unity_logs_1_5', 'unity_logs_1_10']

FILENAME = 'battery_status_log.txt'

def generate_utility_graph(title, ylabel, xlabel, list_of_dir, filename, savename):
    '''Generate the graphs for utiltiy vs either sampling or communication frequency.'''
    x = []
    y = []  

    
    for directory in list_of_dir:
        # number after the last underscore in dir name is the x variable
        x.append(int(directory[directory.rfind('_') + 1:]))

        # find the file with the fps in the directory
        for subdir, dirs, files in os.walk(directory):
            for file in files:
                if os.path.join(subdir, file) == os.path.join(subdir, filename):
                    filepath = os.path.join(subdir, filename)
                    with open(filepath, 'r') as opened_file:
                        fps_list = []

                        # find the fps and average it
                        for line in opened_file:
                            line = line[line.find('FPS = ') + 6 :]
                            fps = line[:line.find(':')]
                            fps_list.append(float(fps))
                        
                        # if the file was not empty
                        if fps_list:
                            y.append(sum(fps_list) / len(fps_list))
    
    # generate graphs
    plt.clf()
    plt.plot(x, y)
    plt.title(title)
    plt.xlabel(xlabel)
    plt.ylabel(ylabel)
    plt.savefig(savename)

def generate_battery_graphs(title, ylabel, xlabel, list_of_dir, savename):
    '''Generate the graphs for utiltiy vs either sampling or communication frequency.'''
    
    # clear lingering graphs
    plt.clf()
    
    # make one plot for multiple curves
    fig, ax = plt.subplots()

    for directory in list_of_dir:
        # get the sampling and communication frequency
        sampling_frequency = int(directory[directory.find('logs_') + 5:directory.rfind('_')])
        communication_frequency = int(directory[directory.rfind('_') + 1:])

        # find the file with the fps in the directory
        for subdir, dirs, files in os.walk(directory):
            for file in files:
                if os.path.join(subdir, file) == os.path.join(subdir, 'battery_status_log.txt'):
                    y = []
                    filepath = os.path.join(subdir, 'battery_status_log.txt')
                    with open(filepath, 'r') as opened_file:
                        # find the battery status
                        for line in opened_file:
                            line = line[line.find('Battery Status - ') + 17 :]
                            battery_status = line[:line.find('%')]
                            y.append(int(battery_status))
                        
                    x = range(0, len(y) * sampling_frequency, sampling_frequency)
                    ax.plot(x, y, label = f'samp freq = {sampling_frequency},\n comm freq = {communication_frequency}')

    # set titles
    ax.set_title(title)
    ax.set_xlabel(xlabel)
    ax.set_ylabel(ylabel)

    # Add a legend
    pos = ax.get_position()
    ax.set_position([pos.x0, pos.y0, pos.width * 0.75, pos.height])
    ax.legend(loc='upper right', bbox_to_anchor=(1.5, 1.1))

    # save graph
    plt.savefig(savename)
    plt.clf()

    


if __name__ == '__main__':
    generate_utility_graph('Utility (FPS) vs Sampling Frequency (sec)', 'Utility (FPS)', 'Sampling Frequency (sec)', CHANGING_SAMP_FREQ_DIR, FILENAME, 'utility_vs_sampling_frequency')
    generate_utility_graph('Utility (FPS) vs Communication Frequency (sec)', 'Utility (FPS)', 'Communication Frequency (sec)', CHANGING_COMM_FREQ_DIR, FILENAME, 'utility_vs_communication_frequency')
    generate_battery_graphs('Battery Status (%) vs Time (sec)', 'Battery Status (%)', 'Time (sec)', CHANGING_SAMP_FREQ_DIR, 'battery_status_vs_time_changing_samp_freq')
    generate_battery_graphs('Battery Status (%) vs Time (sec)', 'Battery Status (%)', 'Time (sec)', CHANGING_COMM_FREQ_DIR, 'battery_status_vs_time_changing_comm_freq')
