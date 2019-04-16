import HZMsg.Frame as frame
import HZMsg.Scalar4 as scalar4
import zmq, flatbuffers, time

context = zmq.Context()
sock = context.socket(zmq.PUB)
sock.connect('tcp://127.0.0.1:5000')
N = 10
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
    time.sleep(1)
    print('.', end='', sep='')

