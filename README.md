# Hoomd ZMQ Plugin

This plugin makes hoomd simulation data available for communication in zmq.


## Plan

Use Flatbuffers and zmq. Ideas:

* Could use TF and add communicate via flatbuffers as another option. Not great option though, doesn't save much
* Just use FB with memcpys (check endianness). This will be easiest.
* Do we need to send an overview packet? Yes, we can do that.
* Why not just send the snapshot? The format already created for this? No, because it's not serializable and contains everything.

## Steps

1. Write schema
2. Write python script that emits random data
3. Create unity scene with particles(?) Maybe do instanced objects
4. Write C# script that reads data and sets particle positions (version control this in repo)
5. Write py compute that sends system info (# of particles)
6. Write C++ code that sends particle positions
7. Modify Unity to use system info sent