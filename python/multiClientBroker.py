import zmq
import sys
import time
from random import randint, random

context = zmq.Context()
frontend = context.socket(zmq.ROUTER)
frontend.bind('tcp://*:5556')

backend = context.socket(zmq.PAIR)
backend.bind('tcp://*:5570')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(backend, zmq.POLLIN)

client_ids = []

while True:
    socks = dict(poller.poll())

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #ident, msg_body = message
        #first element is client id, 2nd is message type.
        if message[1] == 'first-msg':
            client_ids.append(message[0])
            print("first-msg available from " + str(message[0]))
            print(str(len(client_ids)) + " client(s) connected")
        else:
            #should only be a 3 part state update
            backend.send_multipart([message[1],message[2]])

    if socks.get(backend) == zmq.POLLIN:
        msg_body = backend.recv_multipart()
        for _id in client_ids:
            fMsg = []
            #print("msg body: " + str(msg_body[0]))
            if (msg_body[0] == "frame-update"):
                fMsg = [_id, msg_body[0],msg_body[1]]
            elif (msg_body[0] == "frame-complete"):
                fMsg = [_id, msg_body[0]]
            else:
                fMsg = [_id, msg_body[0],msg_body[1]]
            frontend.send_multipart(fMsg)

frontend.close()
backend.close()
context.term()