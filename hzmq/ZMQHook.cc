// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White

#include "ZMQHook.h"
#include <iostream>
#include <stdexcept>
#include <utility>

ZMQHook::ZMQHook(pybind11::object& pyself, std::shared_ptr<SystemDefinition> sysdef, unsigned int period, const char* uri, unsigned int message_size) :
 m_pyself(pyself), m_context(1), m_socket(m_context, ZMQ_PAIR),
m_pdata(sysdef->getParticleData()), m_bdata(sysdef->getBondData()),
m_exec_conf(sysdef->getParticleData()->getExecConf()), m_fbb(NULL), m_period(period), m_N(0) {

    m_socket.bind(uri);
      m_exec_conf->msg->notice(2)
      << "Bound ZMQ Socket on " << uri
      << std::endl;

       m_socket.send(zmq::message_t("hoomd-startup", 13));

    // check a few things
    assert(FLATBUFFERS_LITTLEENDIAN);
    if(sizeof(Scalar4) != sizeof(HZMsg::Scalar4)) {
      m_exec_conf->msg->error() << "Data types are not aligned! FB: " <<
      sizeof(HZMsg::Scalar4) << ", hoomd: " << sizeof(Scalar4) << std::endl;
       throw std::runtime_error("Recompile hoomd or buffer");
    }

    updateSize(message_size);

    sendInitInfo();

}

void ZMQHook::updateSize(unsigned int N) {
  if(N == 0)
    N = 340; // to avoid infinite loops
  // change our sizes
  // if(m_fbb)
  //   delete m_fbb; // this should dellocate our fbb

  // // build our buffer
  // m_fbb = new flatbuffers::FlatBufferBuilder();

  // HZMsg::Scalar4 fbb_scalar4s[N];
  // auto fbb_positions = m_fbb->CreateVectorOfStructs(fbb_scalar4s, N);
  // // why 1? Because if you use default, it assumes you don't want that position
  // // so you cannot mutate it later
  // auto frame = HZMsg::CreateFrame(*m_fbb, N, 1, 1, fbb_positions);
  // HZMsg::FinishFrameBuffer(*m_fbb, frame);

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
      //auto frame = HZMsg::GetMutableFrame(m_fbb->GetBufferPointer());
      int Ni = 0;
      int count = 0;
     // std::cout << " N: " << N << std::endl;
      for(unsigned int i = 0; i < N; i += m_N) {
        // our message will be either m_N or long enough to complete sending the positions
        // why doesn't std::min work here?
        Ni = m_N <= N - (i + m_N) ? m_N : N - i;

        flatbuffers::FlatBufferBuilder builder(4644);
        HZMsg::Scalar4 fbb_scalar4s[N];
        for (int s = 0; s < Ni; s++)
        {
          fbb_scalar4s[s] = HZMsg::Scalar4(positions_data.data[i+s].x, positions_data.data[i+s].y, positions_data.data[i+s].z, positions_data.data[i+s].w); 
        }
        //std::vector<HZMsg::Scalar4>
        auto fbb_positions = builder.CreateVectorOfStructs(fbb_scalar4s, Ni);

        //memcpy(&fbb_positions, &positions_data.data[i], Ni * sizeof(Scalar4));

        auto _f = CreateFrame(builder, Ni, i, timestep, fbb_positions);
        builder.Finish(_f);

        uint8_t *buf = builder.GetBufferPointer();
        int buf_size = builder.GetSize();
       
        zmq::message_t msg(buf_size);
        memcpy((void *)msg.data(), builder.GetBufferPointer(), buf_size);


        // frame->mutate_I(i);
        // frame->mutate_N(Ni);
        // frame->mutate_time(timestep);

        zmq::multipart_t multipart;

        //memcpy over the positions
        
        // set up message
        
        // we will copy the framebuffer (this ctor does it).
        // Otherwise we can have repeated messages!

        // move to multipart
        multipart.addstr("frame-update");
        multipart.add(std::move(msg));

        // send over wire
        // message should already refer to flatbuffer pointer
        multipart.send(m_socket, ZMQ_NOBLOCK);
        count += 1;
      }

//      std::cout << " num messages sent: " << count << std::endl;
 
      m_socket.send(zmq::message_t("frame-complete", 14), ZMQ_NOBLOCK);

      // now send simulation state
      // set up message
      // if( (timestep / m_period) % 10 == 0) {
      //   zmq::multipart_t multipart;
      //   pybind11::object pystring = m_pyself.attr("get_state_msg")();
      //   std::string s = pystring.cast<std::string>();
      //   zmq::message_t msg(s.data(), s.length()); // the length should exclude the null terminator
      //   multipart.addstr("state-update");
      //   multipart.add(std::move(msg));
      //   multipart.send(m_socket);
  
      //   // now receive response
      //   multipart.recv(m_socket);
      //   // assume it's correct name
      //   multipart.pop();
      //   zmq::message_t reply = multipart.pop();
      //   char* data = static_cast<char*>(reply.data());
      //   m_pyself.attr("set_state_msg")(std::string(data, reply.size()));
      // }
  }
}
//send bond data and particle names.
void ZMQHook::sendInitInfo() {

      //bond/names message size. Since we can't assume bonds.size() != particles.size(), a msg size that works for
      //positions may not work for bonds.
      int m_b_N = 100; 
      std::cout << "sending particle names..." << std::endl;
      size_t pN = m_pdata->getN();
      std::cout << " num particles: " << pN << std::endl;
      auto positions = m_pdata->getPositions();
      size_t ppN = positions.getNumElements();
      ArrayHandle<Scalar4> positions_data(positions, access_location::host,
                           access_mode::read);
      std::cout << " num particles PN: " << pN << " ppN: " << ppN << std::endl;
  //   for (int i = 0; i< pN; i++)
  // {
  //   std::cout << " pd x: " << positions_data.data[i].x << " pd y: " << positions_data.data[i].y << " pd z: " << positions_data.data[i].z << " pd w: " << positions_data.data[i].w << std::endl; 
  //   std::cout << "pd name: " << m_pdata->getNameByType(positions_data.data[i].w) << std::endl;

  //   unsigned int _t = m_pdata->getType(i);
  //   std::cout << " type of particle fixed j" << i << ": " << _t << " name(_t): " << m_pdata->getNameByType(_t) << std::endl;
  // }
      //index,name
      for (unsigned int i = 0; i < pN; i+= m_b_N)
      {
        std::string msg_str = ""; 
        for (int j = 0; j < m_b_N; j++)
        {
          unsigned int idx = i+j;
          std::string _name = m_pdata->getNameByType(m_pdata->getType(idx));
          std::string str_builder = "";
          if (j == 0)
          {
            str_builder = std::to_string(idx) + "," + _name;
          } else {
            str_builder = "/" + std::to_string(idx) + "," + _name;
          }
          msg_str.append(str_builder);
        }
        msg_str.append("\n");
        zmq::multipart_t multipart;
        multipart.addstr("names-update");
        multipart.addstr(msg_str);
        multipart.send(m_socket);
      }

      m_socket.send(zmq::message_t("names-complete", 14));
      std::cout << "...done sending particle names" << std::endl;

      // int num_particle_types = m_pdata->getNTypes();
      // std::cout << " num_particle_types: " << num_particle_types << std::endl;
      // auto nametest = m_pdata->getNameByType(0);
      // std::cout << "nametest: " << nametest << std::endl;

      std::cout << "sending bond info..." << std::endl;
      std::vector<std::pair<int,int>> bond_pairs;
      std::vector<int> bond_types;
      std::vector<std::vector<int>> mols_data = findMolecules(bond_pairs, bond_types);
      size_t bN = bond_pairs.size();
      std::cout << " num bonds: " << bN << std::endl;

      for(unsigned int i = 0; i < bN; i += m_b_N)
      {
        std::string msg_str = "";
        for (int j = 0; j < m_b_N; j++)
        {
          std::pair<int,int> _pair = bond_pairs[i+j];
          int _type = bond_types[i+j];
          std::string str_builder = "";
          if (j == 0)
          {
            str_builder = std::to_string(_pair.first) + "," +  std::to_string(_pair.second) + "," + std::to_string(_type);
          } else 
          {
            str_builder = "/" + std::to_string(_pair.first) + "," +  std::to_string(_pair.second) + "," + std::to_string(_type);
          }
          msg_str.append(str_builder);
        }
        msg_str.append("\n");
        zmq::multipart_t multipart;
        multipart.addstr("bonds-update");
        multipart.addstr(msg_str);
        multipart.send(m_socket);
      }

      m_socket.send(zmq::message_t("bonds-complete", 14));

      std::cout << "...complete" << std::endl;
}

//construct vector of molecules (vector of ints) from a hoomd.system.
std::vector<std::vector<int>> ZMQHook::findMolecules(std::vector<std::pair<int,int>> &bond_pairs, std::vector<int> &bond_types){
  
  std::cout << "finding mols..." << std::endl;

  size_t pN = m_pdata->getN();

  //std::cout << " pN: " << pN << std::endl;

  const unsigned int num_bonds = (unsigned int)m_bdata->getN();

   //std::cout << " num_bonds: " << num_bonds << std::endl;

  int pi = 0;

  //std::vector<std::pair<int,int>> _bonds;
  std::vector<std::vector<int>> mapping;
  //std::vector<int> mapped;
  //std::vector<int> unmapped(pN);
  std::unordered_set<int> mapped;
  std::unordered_set<int> unmapped;
  for (int i = 0; i < pN; i++) { unmapped.insert(i); }

//  std::cout << " num bonds: " << num_bonds << " pN: " << pN << std::endl;

  //put bonds in a copy for performance purposes
  for (unsigned int i = 0; i < num_bonds; i++)
  {
    const BondData::members_t bond = m_bdata->getMembersByIndex(i);
    assert(bond.tag[0] < pN);
    assert(bond.tag[1] < pN);

    unsigned int bond_type = m_bdata->getTypeByIndex(i);

    bond_pairs.push_back(std::pair<int, int>(bond.tag[0], bond.tag[1]));
    bond_types.push_back(bond_type);

    //_bonds.push_back( std::pair<int, int>(bond.tag[0], bond.tag[1]) );

  }

  while (mapped.size() != pN)
  {
    if (mapped.size() % 1000 == 0)
    {
      std::cout << "num mapped atoms: " << mapped.size() << std::endl;
    }

    int rand_idx = std::rand() % unmapped.size();
    int num_its = 0;
    for (auto it = unmapped.begin(); it != unmapped.end(); ++it)
    {
      if (num_its == rand_idx)
      {
        pi = *it;
        break;
      }
      num_its ++;
    }

    mapped.insert(pi);
    std::vector<int> pi_tmp;
    pi_tmp.push_back(pi);
    mapping.push_back(pi_tmp);

    std::vector<int> to_consider;
    to_consider.push_back(pi);
    while (to_consider.size() > 0)
    {
      pi = to_consider.back();
      bool found_bond = false;
      for (int bi = 0; bi < bond_pairs.size(); bi++)
      {

        std::pair<int,int> bond = bond_pairs[bi];
        auto b0_it = unmapped.find(bond.first);
        auto b1_it = unmapped.find(bond.second);
        // auto b0_it = std::find(unmapped.begin(), unmapped.end(), bond.first);
        // auto b1_it = std::find(unmapped.begin(), unmapped.end(), bond.second);
        //see if bond contains pi and an unseen atom.
        if ((pi == bond.first && b1_it != unmapped.end()) || (pi == bond.second && b0_it != unmapped.end()))
        {
          int new_pi = 0;
          if (pi == bond.second)
          {
            new_pi = bond.first;
            unmapped.erase(b0_it);
          } else 
          {
            new_pi = bond.second;
            unmapped.erase(b1_it);
          }
          mapped.insert(new_pi);
          mapping[((int)mapping.size())-1].push_back(new_pi);
          to_consider.push_back(new_pi);

          found_bond = true;
          break;
        }
      }
      if (!found_bond)
      {
        auto tc_it = std::find(to_consider.begin(), to_consider.end(), pi);
        if (tc_it != to_consider.end())
        {
          to_consider.erase(tc_it);
        }
      }
    }

  }
  std::cout << "...finished finding molecues" << std::endl;

  //delete duplicates - not sure why there are any - need to debug later.
  for (int i = 0; i < mapping.size(); i++) { 
    std::sort(mapping[i].begin(), mapping[i].end());
    mapping[i].erase( std::unique(mapping[i].begin(), mapping[i].end()), mapping[i].end()); 
  }
  //sort by min atom index.
  std::sort(mapping.begin(), mapping.end(), [](const std::vector<int>& a, const std::vector<int>& b) 
  { 
    return a[0] < b[0];
  });

  return mapping;

}

/* Export the CPU Compute to be visible in the python module
 */
void export_ZMQHook(pybind11::module& m)
    {

      //need to export halfstephook, since it's not exported anywhere else
    pybind11::class_<HalfStepHook, std::shared_ptr<HalfStepHook> >(m, "HalfStepHook");
    pybind11::class_<ZMQHook, std::shared_ptr<ZMQHook >, HalfStepHook>(m, "ZMQHook")
      .def(pybind11::init< pybind11::object&, std::shared_ptr<SystemDefinition>, unsigned int, const char*, unsigned int>())
    ;
    }

