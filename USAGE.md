## Usage Instructions ##

### Installation: ###
1. Hoomd-blue and hoomd-zmq (vr-unity branch).
2. Bluehive modules needed to run the simulations live
	``` 
	module load git anaconda3/2018.12b cmake sqlite cudnn/9.0-7/ zmq/4.2.0/b1
	```

### Running Unity Scene: ###
1. smarter_broker.py is the broker. Run this first on a machine with open ports and a static ip address.
	call smarter_broker like: `python3 smarter_broker.py A B` if you want two.
2. For live simulations:
	* run `python smiles_sim.py --smiles_string [string] --density [integer] --socket ["tcp://*:XXXX"]` to launch the simulation
	* ssh tunnel from Bluehive to the machine where the broker is running. 
	* `ssh -N -L 8081:bhc0001:8080 YourNetIDHere@bluehive.circ.rochester.edu`, where 8081 should be replaced by the port that the broker is expecting and 8080 should be replaced by the port you instantiated the simulation with on BlueHive.

3. For the client-side the only thing to make sure of is that the device is pointed at the correct ip address, namely the ip address for the machine where the broker is running. Currently this needs to be set in Unity in the BROKER_IP_ADDRESS variable for the FilterCommClient.cs (TODO: add support for changing the ip address in the client app itself).
