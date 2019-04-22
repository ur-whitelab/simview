// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White

#include "ZMQHook.h"
#include <iostream>
#include <stdexcept>

ZMQHook::ZMQHook(std::shared_ptr<SystemDefinition> sysdef, unsigned int period, const char* uri) :
 m_context(1), m_socket(m_context, ZMQ_PUB),
m_pdata(sysdef->getParticleData()),
m_exec_conf(sysdef->getParticleData()->getExecConf()), m_fbb(NULL), m_period(period), m_N(0) {

    m_socket.bind(uri);
      m_exec_conf->msg->notice(2)
      << "Bound ZMQ Socket on " << uri
      << std::endl;

    // check a few things
    assert(FLATBUFFERS_LITTLEENDIAN);
    if(sizeof(Scalar4) != sizeof(HZMsg::Scalar4)) {
      m_exec_conf->msg->error() << "Data types are not aligned! FB: " <<
      sizeof(HZMsg::Scalar4) << ", hoomd: " << sizeof(Scalar4) << std::endl;
       throw std::runtime_error("Recompile hoomd or buffer");
    }

}

void ZMQHook::updateSize(unsigned int N) {
  // change our sizes
  if(m_fbb)
    delete m_fbb; // this should dellocate our fbb

  // build our buffer
  m_fbb = new flatbuffers::FlatBufferBuilder();
  HZMsg::Scalar4 fbb_scalar4s[N];
  auto fbb_positions = m_fbb->CreateVectorOfStructs(fbb_scalar4s, N);
  auto frame = HZMsg::CreateFrame(*m_fbb, N, fbb_positions);
  HZMsg::FinishFrameBuffer(*m_fbb, frame);

  // update our N
  m_N = N;
}

void ZMQHook::setSystemDefinition(std::shared_ptr<SystemDefinition> sysdef) {
  //pass
}

inline void my_free(void* data, void* hint) {
  // fake it because fbb is freed elsewhere!
}


void ZMQHook::update(unsigned int timestep)  {
  if(timestep % m_period == 0) {
      auto positions = m_pdata->getPositions();
      ArrayHandle<Scalar4> positions_data(positions, access_location::host,
                           access_mode::read);
      size_t N = positions.getNumElements();
      if(N != m_N)
        updateSize(N);

      // now we just copy to buffer
      auto frame = HZMsg::GetMutableFrame(m_fbb->GetBufferPointer());
      // I'm too lazy to figure out how to get the offset with non-const
      // the addition is because vectors start with size
      memcpy(frame->mutable_positions(), positions_data.data, N * sizeof(Scalar4));

      // set up message
      zmq::message_t msg(m_fbb->GetBufferPointer(), m_fbb->GetSize(), my_free);

      // send over wire
      // message should already refer to flatbuffer pointer
      m_socket.send(msg);
  }
}

/* Export the CPU Compute to be visible in the python module
 */
void export_ZMQHook(pybind11::module& m)
    {

      //need to export halfstephook, since it's not exported anywhere else
    pybind11::class_<HalfStepHook, std::shared_ptr<HalfStepHook> >(m, "HalfStepHook");
    pybind11::class_<ZMQHook, std::shared_ptr<ZMQHook >, HalfStepHook>(m, "ZMQHook")
      .def(pybind11::init<std::shared_ptr<SystemDefinition>, unsigned int, const char*>())
    ;
    }

