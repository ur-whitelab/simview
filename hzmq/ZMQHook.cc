// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White

#include "ZMQHook.h"
#include <iostream>
#include <stdexcept>
#include <utility>

ZMQHook::ZMQHook(std::shared_ptr<SystemDefinition> sysdef, unsigned int period, const char* uri, unsigned int message_size) :
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

    updateSize(message_size);

}

void ZMQHook::updateSize(unsigned int N) {
  if(N == 0)
    N = 340; // to avoid infinite loops
  // change our sizes
  if(m_fbb)
    delete m_fbb; // this should dellocate our fbb

  // build our buffer
  m_fbb = new flatbuffers::FlatBufferBuilder();
  HZMsg::Scalar4 fbb_scalar4s[N];
  auto fbb_positions = m_fbb->CreateVectorOfStructs(fbb_scalar4s, N);
  // why 1? Because if you use default, it assumes you don't want that position
  // so you cannot mutate it later
  auto frame = HZMsg::CreateFrame(*m_fbb, N, 1, fbb_positions);
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

      //get mutable frame
      auto frame = HZMsg::GetMutableFrame(m_fbb->GetBufferPointer());
      int Ni = 0;
      for(unsigned int i = 0; i < N; i += m_N) {
        // our message will be either m_N or long enough to complete sending the positions
        // why doesn't std::min work here?
        Ni = m_N < N - (i + m_N) ? m_N : N - (i + m_N);
        frame->mutate_I(i);
        frame->mutate_N(Ni);
        //memcpy over the positions
        memcpy(frame->mutable_positions(), &positions_data.data[i], Ni * sizeof(Scalar4));

        // set up message
        zmq::multipart_t multipart;
        // we will copy the framebuffer. Otherwise we can have repeated messages!
        zmq::message_t msg(m_fbb->GetBufferPointer(), m_fbb->GetSize());
        // move to multipart
        multipart.push(std::move(msg));
        multipart.push(zmq::message_t("frame-update", 12));

        // send over wire
        // message should already refer to flatbuffer pointer
        multipart.send(m_socket);
      }
  }
}

/* Export the CPU Compute to be visible in the python module
 */
void export_ZMQHook(pybind11::module& m)
    {

      //need to export halfstephook, since it's not exported anywhere else
    pybind11::class_<HalfStepHook, std::shared_ptr<HalfStepHook> >(m, "HalfStepHook");
    pybind11::class_<ZMQHook, std::shared_ptr<ZMQHook >, HalfStepHook>(m, "ZMQHook")
      .def(pybind11::init<std::shared_ptr<SystemDefinition>, unsigned int, const char*, unsigned int>())
    ;
    }

