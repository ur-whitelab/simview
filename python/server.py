import HZMsg.Frame as frame
import HZMsg.Scalar4 as scalar4
import zmq, flatbuffers, time
import sys

context = zmq.Context()
sock = context.socket(zmq.PUB)
sock.bind('tcp://*:5000')
N = 20
print('Starting Loop')
while True:
    builder = flatbuffers.Builder(0)
    frame.FrameStartPositionsVector(builder, N)
    for i in range(N):
        scalar4.CreateScalar4(builder, i, i // 3, i // 3 // 3, i)
    positions = builder.EndVector(N)
    frame.FrameStart(builder)
    frame.FrameAddN(builder, N)
    frame.FrameAddPositions(builder, positions)
    builder.Finish(frame.FrameEnd(builder))

    buffer = builder.Output()
    sock.send_multipart(['frame-update'.encode(), buffer])
    print('.{}.'.format(len(buffer)), end='', sep='')	
    time.sleep(1)    
    sys.stdout.flush()
