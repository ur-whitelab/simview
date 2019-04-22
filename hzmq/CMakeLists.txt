# Maintainer: Andrew White

if (NOT BUILD_MD)
    message(FATAL_ERROR "HZMQ package cannot be built without MD.")
endif(NOT BUILD_MD)

set(COMPONENT_NAME hzmq)

set(_${COMPONENT_NAME}_sources
    module.cc
    ZMQHook.cc
    )

# get zeromq
## load in pkg-config support 
set(ZeroMQ_INCLUDE_DIR "")
set(ZeroMQ_LIBRARY zmq)
find_package(PkgConfig)
if(PkgConfig_FOUND)
    message("Using pkgconfig to find ZeroMQ")
    ## use pkg-config to get hints for 0mq locations
    pkg_check_modules(PC_ZeroMQ zmq)
    if(PKG_CONFIG_FOUND)
        ## use the hint from above to find where 'zmq.hpp' is located
    	find_path(ZeroMQ_INCLUDE_DIR
		  NAMES zmq.hpp
		  PATHS ${PC_ZeroMQ_INCLUDE_DIRS})
    
    ## use the hint from about to find the location of libzmq
    find_library(ZeroMQ_LIBRARY
                 NAMES zmq
                 PATHS ${PC_ZeroMQ_LIBRARY_DIRS})
    else()
      message("Could find ZeroMQ. Will attempt to build anyway")      
    endif()
endif()

add_library (_${COMPONENT_NAME} SHARED ${_${COMPONENT_NAME}_sources})

# Need to define NO_IMPORT_ARRAY in every file but module.cc
set_source_files_properties(${_${COMPONENT_NAME}_sources} ${_${COMPONENT_NAME}_cu_sources} PROPERTIES COMPILE_DEFINITIONS NO_IMPORT_ARRAY)

target_include_directories(_${COMPONENT_NAME} PRIVATE ${ZeroMQ_INCLUDE_DIR})

# link the library to its dependencies
target_link_libraries(_${COMPONENT_NAME} _hoomd _md ${HOOMD_LIBRARIES} ${ZeroMQ_LIBRARY})

# if we are compiling with MPI support built in, set appropriate
# compiler/linker flags
if (ENABLE_MPI)
   if(MPI_COMPILE_FLAGS)
       set_target_properties(_${COMPONENT_NAME} PROPERTIES COMPILE_FLAGS "${MPI_CXX_COMPILE_FLAGS}")
   endif(MPI_COMPILE_FLAGS)
   if(MPI_LINK_FLAGS)
       set_target_properties(_${COMPONENT_NAME} PROPERTIES LINK_FLAGS "${MPI_CXX_LINK_FLAGS}")
   endif(MPI_LINK_FLAGS)
endif(ENABLE_MPI)

##################################
# change the name of the library to be a valid python module
# tweak the properties of the output to make a functional python module
set_target_properties(_${COMPONENT_NAME} PROPERTIES PREFIX "" OUTPUT_NAME "_${COMPONENT_NAME}")

# .dylib is not recognized as a python module by python on Mac OS X
if(APPLE)
    set_target_properties(_${COMPONENT_NAME} PROPERTIES SUFFIX ".so")
endif(APPLE)
if(ENABLE_CUDA)
fix_cudart_rpath(_${COMPONENT_NAME})
endif(ENABLE_CUDA)

# install the library
install(TARGETS _${COMPONENT_NAME}
        LIBRARY DESTINATION ${PYTHON_MODULE_BASE_DIR}/${COMPONENT_NAME}
        )

################ Python only modules
# copy python modules to the build directory to make it a working python package
MACRO(copy_file file)
    add_custom_command (
        OUTPUT ${file}
        POST_BUILD
        COMMAND    ${CMAKE_COMMAND}
        ARGS       -E copy ${CMAKE_CURRENT_SOURCE_DIR}/${file} ${CMAKE_CURRENT_BINARY_DIR}/${file}
        COMMENT    "Copy hoomd/${COMPONENT_NAME}/${file}"
    )
ENDMACRO(copy_file)

set(files
    __init__.py
    zmqhook.py
    )

install(FILES ${files}
        DESTINATION ${PYTHON_MODULE_BASE_DIR}/${COMPONENT_NAME}
       )

foreach(file ${files})
    copy_file(${file})
endforeach()

add_custom_target(copy_${COMPONENT_NAME} ALL DEPENDS ${files})

if (BUILD_TESTING)
    add_subdirectory(test-py)
endif()

