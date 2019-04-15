// Copyright (c) 2018 Andrew White at the University of Rochester
//  This file is part of the Hoomd-Tensorflow plugin developed by Andrew White

#include "ZMQHook.h"
#include <iostream>

/* Export the CPU Compute to be visible in the python module
 */
void hoomd_tf::export_TensorflowCompute(pybind11::module& m)
    {

      //need to export halfstephook, since it's not exported anywhere else
    pybind11::class_<HalfStepHook, std::shared_ptr<HalfStepHook> >(m, "HalfStepHook");

    }

