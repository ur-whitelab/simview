import zmq
import sys
import time
from random import randint, random
import atexit

context = zmq.Context()

frontend = context.socket(zmq.ROUTER)#unity upstream
frontend.bind('tcp://*:5556')

publisher = context.socket(zmq.PUB)#unity downstream
publisher.bind('tcp://*:5559')

backend = context.socket(zmq.PAIR)#hoomd
backend.bind('tcp://*:5570')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(backend, zmq.POLLIN)

client_ids = []

while True:
    socks = dict(poller.poll())

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        client_id = message[0]
        msg_type = message[1]
        #First element is client id, 2nd is message type.
        #The 'first-msg' and 'last-msg' if blocks are not strictly necessary but they provide useful debug info.
        if msg_type == 'first-msg':
            client_ids.append(client_id)
            print(str(client_id) + "is connected.")
            print(str(len(client_ids)) + " client(s) connected")
        elif msg_type == 'last-msg':
            client_ids.remove(client_id)
            print(str(client_id) + " is disconnected.")
            print(str(len(client_ids)) + " client(s) connected")
        elif msg_type == 'simulation-update':
            backend.send_multipart([msg_type, message[2]])    

    if socks.get(backend) == zmq.POLLIN:
        message = backend.recv_multipart()
        publisher.send_multipart(message)
