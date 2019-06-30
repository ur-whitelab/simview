import HZMsg.Frame as frame
import HZMsg.Scalar4 as scalar4
import zmq, flatbuffers, time
import sys


'''This is an example server for sending the encoded flatbuffer info from an ongoing HOOMD-blue
simulation. See client.py for how to parse it.'''

context = zmq.Context()
sock = context.socket(zmq.PAIR)
sock.bind('tcp://*:5559')
N = 100
print('Starting Loop')
j = 0
updates = 0
while True:
    builder = flatbuffers.Builder(0)
    frame.FrameStartPositionsVector(builder, N)
    for i in range(N):
        scalar4.CreateScalar4(builder, i, i, i, i // 10)
    positions = builder.EndVector(N)
    frame.FrameStart(builder)
    frame.FrameAddN(builder, N)
    frame.FrameAddI(builder, j)
    frame.FrameAddPositions(builder, positions)
    builder.Finish(frame.FrameEnd(builder))

    buffer = builder.Output()
    sock.send_multipart(['frame-update'.encode(), buffer])

    #print('.{}.'.format(len(buffer)), end='', sep='')
    print('.{}.'.format(len(buffer)))
    time.sleep(0.01)
    sys.stdout.flush()

    j += N
    j %= 10000

