import zmq
import sys
import time
from random import randint, random
import json

from Simulation import SimulationChannel
from UnityClient import UnityClient


print(str(len(sys.argv) - 1) + ' simulations requested from the command line')

base_ip_address = 'tcp://localhost:'
initialized_simulations = 0

sim_type_list = []

if (len(sys.argv)== 1):
    print('No simulations requested! Defaulting just to A.')
    sim_type_list = ['A']
else:
    sim_type_list = sys.argv[1:]

active_channel = 0
active_channel_changed = False
channels = []
frames_since_last_update_per_channel = []
frame_count = 0
num_framecompletes_sent_to_clients = 0

context = zmq.Context()

publisher = context.socket(zmq.PUB)#unity downstream
publisher.bind('tcp://*:5572')

initialization_publisher = context.socket(zmq.PUB)
initialization_publisher.bind('tcp://*:5573')

active_channel_publisher = context.socket(zmq.PUB)
active_channel_publisher.bind('tcp://*:5574')

instructor_pipe = context.socket(zmq.PAIR)
instructor_pipe.bind('tcp://*:5575')

latest_state_update = {}

sim_ports = {
    'A': '5550',
    'B': '5551',
    'C': '5552',
    'D': '5553'
}

poller = zmq.Poller()

instructor_poller =  zmq.Poller()
instructor_poller.register(instructor_pipe, zmq.POLLIN)

print('reg poller')

for i in range(0, len(sim_type_list)):
    print('starting simulation channel ' + str(i) + ' of type ' + str(sim_type_list[i]))

    sim_ip = base_ip_address + sim_ports[sim_type_list[i]]

    sc_socket = context.socket(zmq.PAIR)
    sc_socket.connect(sim_ip)
    poller.register(sc_socket, zmq.POLLIN)

    sc = SimulationChannel(context, sim_ip, sc_socket, str(i))
    channels.append(sc)

    frames_since_last_update_per_channel.append(0)

for i in range (0, len(channels)):
    if (i == active_channel):
        print('active channel is ' + str(i))
    print(str(i) + ' channel is a sim of type ' + str(channels[i].simulation_type))
    print('it has an ip address of ' + str(channels[i].ip_address))

# def send_init_data_to_client(_id):

#     initialization_publisher.send_multipart([_id, b're-init'])

#     pnames_data = channels[active_channel].particle_name_messages
#     b_data = channels[active_channel].bond_messages

#     print('sending ' + str(len(pnames_data)) + ' particle names and ' + str(len(b_data)) + ' bonds to unity')
#     print('...from channel ' + str(active_channel) + ' of type ' + str(channels[active_channel].simulation_type))

#     for n_msg in pnames_data:
#         msg = [_id, n_msg[0], n_msg[1]]
#         initialization_publisher.send_multipart(msg)
#     print('sent names to client ' + str(_id))
#     initialization_publisher.send_multipart([_id, b'names-complete'])

#     print('sending bonds to client ' + str(_id))
#     for b_msg in b_data:
#         msg = [_id, b_msg[0], b_msg[1]]
#         initialization_publisher.send_multipart(msg)

#     client_dict[_id].active_channel = active_channel
#     client_dict[_id].initialized = True

#     initialization_publisher.send_multipart([_id, b'bonds-complete'])

def send_channel_data_to_all_clients():
    active_channel_string = str(active_channel)
    active_channel_message = bytes(active_channel_string, 'utf-8')

    active_channel_publisher.send_multipart([b'channel-update', active_channel_message])

def send_init_data_to_all_clients():

    str_ac = str(active_channel)
    channel_aware_message = bytes(str_ac, 'utf-8')

    #initialization_publisher.send_multipart([b're-init', channel_aware_message])
    initialization_publisher.send_multipart([b're-init', channel_aware_message])
    print("sent re-init message on frame " + str(frame_count))

    pnames_data = channels[active_channel].particle_name_messages
    b_data = channels[active_channel].bond_messages

    print('sending ' + str(len(pnames_data)) + ' particle name messages and ' + str(len(b_data)) + ' bond messages to unity')
    print('...from channel ' + str(active_channel) + ' of type ' + str(channels[active_channel].simulation_type))

    for n_msg in pnames_data:
        msg = [n_msg[0], n_msg[1]]
        initialization_publisher.send_multipart(msg)

    print('sent names to clients.')
    initialization_publisher.send_multipart([b'names-complete', channel_aware_message])

    print('sending bonds to clients.')
    for b_msg in b_data:
        msg = [b_msg[0], b_msg[1]]
        initialization_publisher.send_multipart(msg)

    initialization_publisher.send_multipart([b'bonds-complete', channel_aware_message])

waitingToSend = False
start_time = time.time()

# send_init_data_to_all_clients()
# send_channel_data_to_all_clients()

while True:

    #check the pipe to the instructor client to see if there's data in there.
    #pollers don't block the thread


    instr_poll = dict(instructor_poller.poll(10))
    if instr_poll.get(instructor_pipe) == zmq.POLLIN:
        print("polling")
        instr_msg = instructor_pipe.recv_multipart()
        instr_msg_type = instr_msg[0]
        if instr_msg_type == b'sim-update':
           latest_state_update = instr_msg[1]
           #channels[active_channel].socket.send('')
        if instr_msg_type == b'channel-change':
            print('channel switched from ' + str(active_channel) + ' to ' + str(instr_msg[1]))
            active_channel = int(instr_msg[1])

    #end of instructor pipe check code.

    socks = dict(poller.poll(10))

    if not waitingToSend:
        start_time = time.time()
        waitingToSend = True

    for i in range(0, len(channels)):
        channel_socket = channels[i].socket
        if socks.get(channel_socket) == zmq.POLLIN:

            if (frame_count % 1000 == 0):
                print('Update from channel ' + str(i) + '; there have been ' + str(frames_since_last_update_per_channel[i]) + ' frames since the last update')

            frames_since_last_update_per_channel[i] = 0
            message = channel_socket.recv_multipart()
            msg_type = message[0]
            #handle various initialization method types
            if msg_type == b'names-update':
                channels[i].particle_name_messages.append(message)
            elif msg_type == b'bonds-update':
                channels[i].bond_messages.append(message)
            elif msg_type == b'bonds-complete':
                channels[i].initialized = True
                initialized_simulations += 1
                print('channel of type ' + str(channels[i].simulation_type) + ' is initialized')
                print('num of name messages in channel ' + str(i) + ' of type ' + str(channels[i].simulation_type) + ': ' + str(len(channels[i].particle_name_messages)))
                print('num of bond messages in channel ' + str(i) + ' of type ' + str(channels[i].simulation_type) + ': ' + str(len(channels[i].bond_messages)))
                print('number of initialized simulations: ' + str(initialized_simulations))
            #Should execute only for the activate simulation and only once the above init code has run.
            elif (i == active_channel and channels[i].initialized):
                # Look into adding this snippet
                # if msg_type == b'state-update':
                #   expecting_state_update = True

                fps = 1.0 / max((time.time() - start_time), 0.0001)
                # if msg_type == b'frame-complete':
                #     while (fps >= 90.0):
                #         fps = 1.0 / max((time.time() - start_time), 0.0001)

                waitingToSend = False
                str_ac = str(active_channel)
                channel_aware_message = [bytes(str_ac, 'utf-8')]
                #print(str(message) + "\n")
                for m in message:
                    channel_aware_message.append(m)
                    #print('cam: ' + str(channel_aware_message))
                if msg_type == b'frame-complete':
                    if (frame_count % 1000 == 0):
                        print('fps that publisher sent frame-complete: ' + str(fps))
                        print('number of frame-completes sent to clients: ' + str(num_framecompletes_sent_to_clients) + ' at frame ' + str(frame_count) )
                    num_framecompletes_sent_to_clients += 1
                publisher.send_multipart(channel_aware_message)#only send message from publisher if its from the active channel.

        else:
            frames_since_last_update_per_channel[i] += 1
        #----end socket pollin

    if active_channel_changed:
        send_channel_data_to_all_clients()
        active_channel_changed = False

    if frame_count % 1000 == 0:
        send_init_data_to_all_clients()

   

    # if frame_count % 20000 == 0 and frame_count != 0:
    #     print("switched channels")
    #     active_channel_changed = True
    #     active_channel += 1
    #     if active_channel > 1:
    #         active_channel = 0
    #     # if active_channel == 0:
    #     #     active_channel = 1
    #     # else:
    #     #     active_channel = 0


    frame_count += 1




