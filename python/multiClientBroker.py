import zmq
import sys
import time
from random import randint, random
import pickle

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

expecting_state_update = False
last_state_update = pickle.load(open("default_state_update.p", "rb"))

while True:
    socks = dict(poller.poll())

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #First element is client id, 2nd is message type.
        client_id = message[0]
        msg_type = message[1]
        
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
            expecting_state_update = False #Unity obliged Hoomd's state-update request

        #if this trips then Hoomd is expecting a state-update and Unity hasn't sent one
        if expecting_state_update and last_state_update != []:
            backend.send_multipart(last_state_update)
            expecting_state_update = False

    if socks.get(backend) == zmq.POLLIN:
        message = backend.recv_multipart()
        msg_type = message[0]
        if msg_type == 'state-update':
            expecting_state_update = True
        publisher.send_multipart(message)
