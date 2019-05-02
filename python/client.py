import zmq, time, json
import HZMsg.Frame as frame

context = zmq.Context()
sock = context.socket(zmq.PAIR)
#sock.setsockopt(zmq.SUBSCRIBE, b'')
sock.connect('tcp://localhost:8888')

while True:
    t = time.time()
    mtime = None
    count = 0
    while True:
        msg = sock.recv_multipart()
        count += 1
        mtype = msg[0].decode()
        if mtype == 'frame-complete':
            break
        buf = bytearray(msg[1])
        f = frame.Frame.GetRootAsFrame(buf, 0)
    print((time.time() - t) * f.N() / count, ' ms', count, 'parts')
    msg = sock.recv_multipart()
    mtype = msg[0].decode()
    assert mtype == 'state-update', 'got ' + mtype + ' instead of state-update'
    state_msg = msg[1].decode()
    state = json.loads(state_msg)
    print(state)
    sock.send_multipart(['state-set'.encode(), json.dumps({'temperature': 1.2}).encode()])


