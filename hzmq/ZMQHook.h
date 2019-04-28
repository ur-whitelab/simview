// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White


#ifndef _ZMQ_HOOK_H_
#define _ZMQ_HOOK_H_


#include <hoomd/HalfStepHook.h>
#include "flatbuffers/flatbuffers.h"
#include "frame_generated.h"
#include "zmq.hpp"
#include "zmq_addon.hpp"

// pybind11 is used to create the python bindings to the C++ object,
// but not if we are compiling GPU kernels
#ifndef NVCC
#include <hoomd/extern/pybind/include/pybind11/pybind11.h>
#include <hoomd/extern/pybind/include/pybind11/stl.h>
#include <hoomd/extern/pybind/include/pybind11/stl_bind.h>
#endif



class ZMQHook : public HalfStepHook {

public:
  ZMQHook(std::shared_ptr<SystemDefinition> sysdef, unsigned int period, const char* uri, unsigned int message_size);

  void update(unsigned int timestep) override;
  void setSystemDefinition(std::shared_ptr<SystemDefinition> sysdef);

private:

  void updateSize(unsigned int N);

  zmq::context_t m_context;
  zmq::socket_t m_socket;
  zmq::message_t* m_message;
  std::shared_ptr<const ParticleData> m_pdata;
  std::shared_ptr<const ExecutionConfiguration> m_exec_conf;
  flatbuffers::FlatBufferBuilder* m_fbb;
  unsigned int m_period;
  unsigned int m_N;
};

void export_ZMQHook(pybind11::module& m);

#endif  // _ZMQ_HOOK_H_
