// Copyright (c) 2019 Andrew White at the University of Rochester
// This file is part of the zmq plugin developed by Andrew White

// Include the defined classes that are to be exported to python
#include "ZMQHook.h"

#include <hoomd/extern/pybind/include/pybind11/pybind11.h>


// specify the python module. Note that the name must expliclty match the PROJECT() name provided in CMakeLists
// (with an underscore in front)
PYBIND11_PLUGIN(_zmq)
    {
    pybind11::module m("_zmq");
    export_ZMQHook(m);
    return m.ptr();
    }
