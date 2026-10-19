# mx_alive - ReXGlue Recompiled Project
#
# SDK boilerplate lives in generated/rexglue.cmake.

cmake_minimum_required(VERSION 3.25)
project(mx_alive LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 23)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

include(generated/rexglue.cmake)

# Sources
set(MXALIVE_SOURCES
    src/main.cpp
    src/midasm_hooks.cpp
)

if(WIN32)
    add_executable(mx_alive WIN32 ${MXALIVE_SOURCES})
else()
    add_executable(mx_alive ${MXALIVE_SOURCES})
endif()

rexglue_setup_target(mx_alive GPU_PLUGINS xenos)

if(WIN32)
    target_link_options(mx_alive PRIVATE
        "LINKER:/MAP:${CMAKE_CURRENT_SOURCE_DIR}/out/mx_alive.map")
endif()

# Performance: target the host CPU and allow cross-translation-unit inlining in
# the generated code. The recompiled guest loop is the frame-rate bottleneck.
if(NOT MSVC)
    target_compile_options(mx_alive PRIVATE -march=native)
    set_property(TARGET mx_alive PROPERTY INTERPROCEDURAL_OPTIMIZATION TRUE)
    if(TARGET mx_alive_recomp)
        target_compile_options(mx_alive_recomp PRIVATE -march=native)
        set_property(TARGET mx_alive_recomp PROPERTY INTERPROCEDURAL_OPTIMIZATION TRUE)
    endif()
endif()
