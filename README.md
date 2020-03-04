# SimView 

SimView allows 3D visualization of molecular simulations from HOOMD-blue in Unity. The Unity scenes can be built for virtual or augmented reality as an android app.

## Usage Instructions ##

### Installation: ###
``` 
module load git anaconda3/2018.12b cmake sqlite cudnn/9.0-7/ zmq/4.2.0/b1
conda create -n hoomd-zmq python=3.7
source activate hoomd-zmq
export CMAKE_PREFIX_PATH=/path/to/environment
git clone --recursive https://bitbucket.org/glotzer/hoomd-blue hoomd-blue
cd hoomd-blue && git checkout tags/v2.5.2
git clone https://github.com/ur-whitelab/hoomd-zmq
```
Once you've cloned the hoomd-zmq directory, switch to branch vr-unity and pull changes:
```
git checkout -b vr-unity
git pull origin vr-unity
```
Then continue with the compilation:
```
ln -s $HOME/hoomd-zmq/hzmq $HOME/hoomd-blue/hoomd
mkdir build && cd build
CXX=g++ CC=gcc cmake .. -DCMAKE_BUILD_TYPE=Release \
 -DENABLE_CUDA=ON -DENABLE_MPI=OFF\
 -DBUILD_HPMC=off -DBUILD_CGCMM=off -DBUILD_MD=on\
 -DBUILD_METAL=off -DBUILD_TESTING=off -DBUILD_DEPRECATED=off -DBUILD_MPCD=OFF \
 -DCMAKE_INSTALL_PREFIX=`python -c "import site; print(site.getsitepackages()[0])"`
make
make install
```
If you are using a **conda environment**, you may need to force cmake to find your python environment. This is rare, we only see it on our compute cluster which has multiple conflicting version of python and conda. The following additional flags can help with this:
```
export CMAKE_PREFIX_PATH=/path/to/environment
CXX=g++ CC=gcc cmake .. \
-DPYTHON_INCLUDE_DIR=$(python -c "from distutils.sysconfig import get_python_inc; print(get_python_inc())") \
-DPYTHON_LIBRARY=$(python -c "import distutils.sysconfig as sysconfig; print(sysconfig.get_config_var('LIBDIR'))") \
-DPYTHON_EXECUTABLE=$(which python) \
-DCMAKE_BUILD_TYPE=Release -DENABLE_CUDA=ON -DENABLE_MPI=OFF -DBUILD_HPMC=off -DBUILD_CGCMM=off -DBUILD_MD=on \
-DBUILD_METAL=off -DBUILD_TESTING=off -DBUILD_DEPRECATED=off -DBUILD_MPCD=OFF \
-DCMAKE_INSTALL_PREFIX=`python -c "import site; print(site.getsitepackages()[0])"`
```

### Running Unity Scene: ###

1. smarter_broker.py is the broker. Run this first on a machine with open ports and a static ip address.
	* `python python/smarter_broker.py A B` if you want two clients.
2. For live simulations:
	* `python python/launch_scripts/smiles_sim.py --smiles_string [string] --density [integer] --socket ["tcp://*:XXXX"]` to launch the simulation
	* ssh tunnel from Bluehive to the machine where the broker is running: `ssh -N -L 8081:bhc0001:8080 YourNetIDHere@bluehive.circ.rochester.edu`, where 8081 should be replaced by the port that the broker is expecting and 8080 should be replaced by the port you instantiated the simulation with on BlueHive.

3. For the client-side the only thing to make sure of is that the device is pointed at the correct ip address, namely the ip address for the machine where the broker is running. Currently this needs to be set in Unity in the BROKER_IP_ADDRESS variable for the FilterCommClient.cs (TODO: add support for changing the ip address in the client app itself).


### tunnel.bat file
This contains the putty command to start ssh with port forwarding. It uses a program called ‘plink’ and tunnels an RDP connection. The syntax of the port forward argument is the same as with the Linux version of ‘ssh’. The trouble with this is that the tunnel takes a minute to be established and it does not go away cleanly all the time. This is put into a .bat file on windows and then executed with a double click. The first line checks for an old connection and the last line is to wait until the tunnel comes up.