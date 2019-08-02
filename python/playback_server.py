import HZMsg.Frame as frame
import HZMsg.Scalar4 as scalar4
import zmq, flatbuffers, time
import sys
import pickle
import time

from argparse import ArgumentParser

parser = ArgumentParser()
parser.add_argument('-f', '--file', dest='filename',
                    help='write report to FILE', metavar='FILE')
parser.add_argument('-p', '--port', dest='port',
                    help='set port', metavar='PORT')
args = parser.parse_args()

#port = int(sys.argv[1])
#packet_save_file_path = 'playback_frames.p'
port = args.port
packet_save_file_path = args.filename

context = zmq.Context()
sock = context.socket(zmq.PAIR)
sock.bind('tcp://*:' + str(port))
N = 100
print('Starting Playback Loop')
j = 0
frame_idx = 0
num_particles = 0

playback_dict = pickle.load(open(packet_save_file_path, 'rb'))

print('number of names messages from simulation: ' + str(len(playback_dict['names'])))
for n_msg in playback_dict['names']:
    sock.send_multipart([b'names-update', n_msg])
    #time.sleep(0.01)
sock.send_multipart([b'names-complete', b'none'])

print('number of bonds messages from simulation: ' + str(len(playback_dict['bonds'])))
for b_msg in playback_dict['bonds']:
    sock.send_multipart([b'bonds-update', b_msg])
    #time.sleep(0.01)
sock.send_multipart([b'bonds-complete', b'none'])

while True:
    start_time = time.time()

    position_bufs = playback_dict[str(frame_idx)]
    for buf in position_bufs:
        f = frame.Frame.GetRootAsFrame(buf, 0)
        num_positions = f.PositionsLength()
        _n = f.N()
        _i = f.I()
        #print(_n, _i)
        sock.send_multipart([b'frame-update',buf])

    sock.send_multipart([b'frame-complete', b'none'])

    if (frame_idx % 10 == 0 and frame_idx != 0):
        su = playback_dict['state-updates'][str(frame_idx)]

    #print('frame: ' + str(frame_idx))
    frame_idx += 1

    #if we reach the end of the playback, loop it.
    if (playback_dict['num_frames'] == frame_idx):
        frame_idx = 0

    #time.sleep(0.1)\
    if (time.time() - start_time >= 0.0001):
        fps = 1.0 / (time.time() - start_time)
        while (fps > 80):
            fps = 1.0 / (time.time() - start_time)




    #sys.stdout.flush()


