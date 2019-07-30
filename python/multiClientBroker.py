import zmq
import sys
import time
from random import randint, random
import json

from Simulation import SimulationChannel
from UnityClient import UnityClient

print(str(len(sys.argv) - 1) + " simulations requested from the command line")

base_ip_address = "tcp://localhost:"

sim_type_list = []

print("sa1: " + str(sys.argv[1]))

if (len(sys.argv)== 1):
    print("No simulations requested! Defaulting just to A.")
    sim_type_list = ["A"]
else:
    sim_type_list = sys.argv[1:]

active_channel = 0
next_active_channel = 0
channels = []

context = zmq.Context()

frontend = context.socket(zmq.ROUTER)#unity upstream
frontend.bind('tcp://*:5571')

publisher = context.socket(zmq.PUB)#unity downstream
publisher.bind('tcp://*:5572')

instructor = context.socket(zmq.PAIR)#instructor scene
instructor.bind('tcp://*:5570')
isInstructorConnected = False
instructor_client_id = ''

frame_count = 0

# backend = context.socket(zmq.PAIR)#hoomd
# backend.connect('tcp://localhost:5570')

poller = zmq.Poller()
poller.register(frontend, zmq.POLLIN)
poller.register(instructor, zmq.POLLIN)
#poller.register(backend, zmq.POLLIN)

sim_ports = {
    "A": "5550",
    "B": "5551",
    "C": "5552",
    "D": "5553"
}

print(sim_type_list)

for i in range(0, len(sim_type_list)):
    print("starting simulation channel " + str(i) + " of type " + str(sim_type_list[i]))

    sim_ip = base_ip_address + sim_ports[sim_type_list[i]]

    sc_socket = context.socket(zmq.PAIR)
    sc_socket.connect(sim_ip)
    poller.register(sc_socket, zmq.POLLIN)

    sc = SimulationChannel(context, sim_ip, sc_socket, str(i))
    channels.append(sc)

for i in range (0, len(channels)):
    if (i == active_channel):
        print("active channel is " + str(i))
    print(str(i) + " channel is a sim of type " + str(channels[i].simulation_type))
    print("it has an ip address of " + str(channels[i].ip_address))

client_dict = {}

expecting_state_update = False

default_state_update = {
    "temperature": "0.15",
    "pressure": "1.0",
    "box": "1"
}
default_state_update = json.dumps(default_state_update)
last_state_update_msg = ["state-update", default_state_update]

def send_init_data_to_client(_id):

    pnames_data = channels[active_channel].particle_name_messages
    b_data = channels[active_channel].bond_messages

    print("sending " + str(len(pnames_data)) + " particle names and " + str(len(b_data)) + " bonds to unity")
    print("...from channel " + str(active_channel) + " of type " + str(channels[active_channel].simulation_type))

    for n_msg in pnames_data:
        msg = [_id, n_msg[0], n_msg[1]]
        frontend.send_multipart(msg)
    print("sent names to client " + str(_id))
    frontend.send_multipart([_id, b"names-complete"])

    print("sending bonds to client " + str(_id))
    for b_msg in b_data:
        msg = [_id, b_msg[0], b_msg[1]]
        frontend.send_multipart(msg)

    client_dict[_id].active_channel = active_channel
    client_dict[_id].initialized = True

    frontend.send_multipart([_id, b"bonds-complete"])

def send_init_data_to_all_clients():
    for _id in client_dict:
        send_init_data_to_client(_id)

initialized_simulations = 0

while True:
    socks = dict(poller.poll())

    for i in range(0, len(channels)):
        channel_socket = channels[i].socket
        if socks.get(channel_socket) == zmq.POLLIN:
            message = channel_socket.recv_multipart()
            msg_type = message[0]
            #print(str(channels[i].simulation_type) + " --- " + str(msg_type))
            if msg_type == b'names-update':
                channels[i].particle_name_messages.append(message)
            elif msg_type == b'bonds-update':
                channels[i].bond_messages.append(message)
            elif msg_type == b'bonds-complete':
                channels[i].initialized = True
                initialized_simulations += 1
                print("channel of type " + str(channels[i].simulation_type) + " is initialized")  
                print("num of name messages in channel " + str(i) + " of type " + str(channels[i].simulation_type) + ": " + str(len(channels[i].particle_name_messages)))
                print("num of bond messages in channel " + str(i) + " of type " + str(channels[i].simulation_type) + ": " + str(len(channels[i].bond_messages)))
                print("number of initialized simulations: " + str(initialized_simulations))
            elif msg_type == b'hoomd-startup':
                channels[i].reset_init_data()
                if initialized_simulations > 0:
                    initialized_simulations -= 1
                #we only need to worry about unity-hoomd communication with the active channel
            elif (i == active_channel and channels[i].initialized):
                if msg_type == b'state-update':
                    expecting_state_update = True   
                publisher.send_multipart(message)

    #check that each client is aware of the current active channel
    for client_id in client_dict:
        uc = client_dict[client_id]
        if (uc.active_channel != active_channel or not uc.initialized) and channels[active_channel].initialized:
            print("client " + str(uc.client_id) + " thinks that the active_channel is " + str(uc.active_channel))
            send_init_data_to_client(uc.client_id)

    # if expecting_state_update:
    #     channels[active_channel].socket.send_multipart([b'simulation-update', bytes(default_state_update, 'utf-8')])
    #     #backend.send_multipart(default_state_update)
    #     expecting_state_update = False

    if socks.get(frontend) == zmq.POLLIN:
        message = frontend.recv_multipart()
        #First element is client id, 2nd is message type.
        client_id = message[0]
        msg_type = message[1]
        
        # if next_active_channel != active_channel:
        #     msg_type = 'hoomd-startup'

        if msg_type == b'first-msg':
            uc = UnityClient(client_id, active_channel)
            client_dict[client_id] = uc
            if (channels[active_channel].initialized):
                send_init_data_to_client(client_id)
                client_dict[client_id].initialized = True
            
            print(str(len(client_dict)) + " client(s) connected")

        elif msg_type == b'last-msg':
            if (client_id in client_id.keys()):
                del client_dict[client_id]
                print(str(client_id) + " is disconnected.")
                print(str(len(client_dict)) + " client(s) connected")
            else:
                print("Trying to disconnect client "str(client_id) + " which is already disconnected.")


        elif msg_type == b'simulation-update':
            channels[active_channel].socket.send_multipart([msg_type, message[2]])
            print("sim up in sim up: " + str(message[2]))
            #backend.send_multipart([msg_type, message[2]])
            expecting_state_update = False #Unity obliged Hoomd's state-update request
        #if this trips then Hoomd is expecting a state-update and Unity hasn't sent one
        if expecting_state_update:
            channels[active_channel].socket.send_multipart([b'simulation-update', bytes(default_state_update, 'utf-8')])
            #backend.send_multipart(default_state_update)
            expecting_state_update = False

    if socks.get(instructor) == zmq.POLLIN:
        message = instructor.recv_multipart()
        msg_id = message[0]
        if msg_id == b"first-msg-instructor":
            instructor_client_id = message[1]
            isInstructorConnected = True
        elif msg_id == b'last-msg-instructor':
            instructor_client_id = b''
            isInstructorConnected = False

        elif msg_id == b"ac-change":
            active_channel = int(message[1])
            publisher.send_multipart([b"hoomd-startup",b"tmp"])
            channels[active_channel].socket.send_multipart([b'simulation-update', bytes(default_state_update, 'utf-8')])
        

    #send debug info to the instructor.
    if frame_count % 10 == 0 and isInstructorConnected:
        string_of_simulations_list = ""
        for i in range(0, len(channels)):
            c_type = channels[i].simulation_type
            c_ip = channels[i].ip_address
            c_init = channels[i].initialized
            c_active = (i == active_channel)
            #idx, type, ip, initialized?, active?
            str_builder = ""
            if i == 0:
                str_builder = str(i) + "," + str(c_type) + "," + str(c_ip) + "," + str(c_init) + "," + str(c_active)
            else:
                str_builder = "|" + str(i) + "," + str(c_type) + "," + str(c_ip) + "," + str(c_init) + "," + str(c_active)
            string_of_simulations_list += str_builder
        string_of_clients_dict = ""
        _idx = 0
        for client_id in client_dict:
            #id, connected
            _uc = client_dict[client_id]
            if _idx == 0:
                str_builder = str(client_id) + "," + str(_uc.initialized)
            else:
                str_builder = "|" + str(client_id) + "," + str(_uc.initialized)
            string_of_clients_dict += str_builder
            _idx += 1
        full_message_string = string_of_simulations_list + "_" + string_of_clients_dict
        instructor.send_multipart([b'debug-string', bytes(full_message_string, 'utf-8')])

    frame_count += 1








                    
        