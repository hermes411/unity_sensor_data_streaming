import matplotlib.pyplot as plt
import os

CHANGING_SAMP_FREQ_DIR = ['unity_logs_1_1', 'unity_logs_2_2', 'unity_logs_5_5', 'unity_logs_10_10']
CHANGING_COMM_FREQ_DIR = ['unity_logs_1_1', 'unity_logs_1_2', 'unity_logs_1_5', 'unity_logs_1_10']

FILENAME = 'battery_status_log.txt'

def generate_graph(title, ylabel, xlabel, list_of_dir, filename, savename):
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


if __name__ == '__main__':
    generate_graph('Utility (FPS) vs Sampling Frequency (sec)', 'Utility (FPS)', 'Sampling Frequency (sec)', CHANGING_SAMP_FREQ_DIR, FILENAME, 'utility_vs_sampling_frequency')
    generate_graph('Utility (FPS) vs Communication Frequency (sec)', 'Utility (FPS)', 'Communication Frequency (sec)', CHANGING_COMM_FREQ_DIR, FILENAME, 'utility_vs_communication_frequency')

