import zmq, time
import HZMsg.Frame as frame

context = zmq.Context()
sock = context.socket(zmq.PAIR)
#sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:5000')

while True:
    t = time.time()
    mtime = None
    count = 0
    while True:
        msg = sock.recv_multipart()
        count += 1
        buf = bytearray(msg[1])
        f = frame.Frame.GetRootAsFrame(buf, 0)
        if mtime is None:
            mtime = f.Time()
        elif mtime != f.Time():
            break
    print((time.time() - t) * f.N() / count, ' ms', count, 'parts')

