import HZMsg.Frame as frame
import HZMsg.Scalar4 as scalar4
import zmq, flatbuffers, time
import sys
import pickle

port = int(sys.argv[1])
packet_save_file_path = 'playback_frames.p'

context = zmq.Context()
sock = context.socket(zmq.PAIR)
sock.bind('tcp://*:' + str(port))
sock.send_multipart([b'hoomd-startup', b'none'])
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
    
    buf = playback_dict[str(frame_idx)]
    
    f = frame.Frame.GetRootAsFrame(buf, 0)
    num_positions = f.PositionsLength()
    print('num positions: ' + str(num_positions))

    # builder = flatbuffers.Builder(0)
    # frame.FrameStartPositionsVector(builder, N)
    # for i in range(N):
    #     pos = f.Positions(i)
    #     scalar4.CreateScalar4(builder, pos.X(), pos.Y(), pos.Z(), pos.W())
    # positions = builder.EndVector(N)
    # frame.FrameStart(builder)
    # frame.FrameAddN(builder, N)
    # frame.FrameAddI(builder, j)
    # frame.FrameAddPositions(builder, positions)
    # builder.Finish(frame.FrameEnd(builder))
    # buffer = builder.Output()
    #sock.send_multipart(['frame-update'.encode(), buffer])

    _n = f.N()
    _i = f.I()

    if (frame_idx != 0 and f.I() == 0):
        sock.send_multipart([b'frame-complete', b'none'])
    else:
        sock.send_multipart([b'frame-update', buf])

    print('frame: ' + str(frame_idx))
    print(_n, _i)

    frame_idx += 1

    time.sleep(1)
    sys.stdout.flush()

    j += N
    j %= 10000

