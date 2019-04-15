// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White


#ifndef _ZMQ_HOOK_H_
#define _ZMQ_HOOK_H_


#include <hoomd/HalfStepHook.h>

// pybind11 is used to create the python bindings to the C++ object,
// but not if we are compiling GPU kernels
#ifndef NVCC
#include <hoomd/extern/pybind/include/pybind11/pybind11.h>
#include <hoomd/extern/pybind/include/pybind11/stl.h>
#include <hoomd/extern/pybind/include/pybind11/stl_bind.h>
#endif



class ZMQHook : public HalfStepHook {

  void update(unsigned int timestep) override {
    _f.computeForces(timestep);
  }

  // called for half step hook
  void setSystemDefinition(std::shared_ptr<SystemDefinition> sysdef) override {
    //pass
  }

};


#endif  // _ZMQ_HOOK_H_
