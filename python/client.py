import zmq, time
import HZMsg.Frame as frame

context = zmq.Context()
sock = context.socket(zmq.SUB)
sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:8888')

while True:

    msg = sock.recv_multipart()[1]
    buf = bytearray(msg)
    f = frame.Frame.GetRootAsFrame(buf, 0)
    print('message: ', f.N(), f.I(), f.Positions(0).X(),
    f.Positions(0).Y(), f.Positions(0).Z(), f.Positions(0).W())
    t = time.time()
    for i in range(1):
        msg = sock.recv_multipart()
    print((time.time() - t) * f.N(), 'ms')

