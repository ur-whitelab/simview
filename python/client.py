import zmq, time
import HZMsg.Frame as frame

context = zmq.Context()
sock = context.socket(zmq.SUB)
sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:5000')

while True:

    msg = sock.recv_multipart()[1]
    buf = bytearray(msg)
    f = frame.Frame.GetRootAsFrame(buf, 0)
    print('message: ', f.N(), f.I())
    t = time.time()
    for i in range(1000):
        msg = sock.recv_multipart()
    print((time.time() - t) * f.N())

