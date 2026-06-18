cmake_minimum_required(VERSION 3.28)
project(KitchenSink_Geometry_Occt_Cpp)

set(CMAKE_CXX_STANDARD 20)

set(CPP_INTEROP_GEN ${CMAKE_CURRENT_LIST_DIR}/../../../externals/include)
include_directories(${CPP_INTEROP_GEN})

find_package(OpenCASCADE CONFIG REQUIRED)

add_library(KitchenSink_Geometry_Occt_Cpp SHARED
        D3/gp_Pnt_C.cpp
        D3/gp_XYZ_C.cpp
        D3/gp_Lin_C.cpp
        D3/gp_Ax1_C.cpp
        D3/gp_Dir_C.cpp
        export_macro.h
        D3/Geom_BSplineCurve_C.cpp
        NCollectionArray1/NCollection_Array1_gp_Pnt2d_C.cpp
        NCollectionArray1/NCollection_Array1_gp_Pnt_C.cpp
        NCollectionArray1/NCollection_Array1_double_C.cpp
        NCollectionArray1/NCollection_Array1_int_C.cpp
        gp_XY_C.cpp
        D3/Bnd_Box_C.cpp
        D2/Bnd_Box2d_C.cpp
)

target_link_libraries(KitchenSink_Geometry_Occt_Cpp PRIVATE
        TKG2d
        TKGeomAlgo
        TKernel
        TKMath
        TKG3d
        TKGeomBase
)

set(OUTPUT_DIR "${CMAKE_CURRENT_LIST_DIR}/bin")

set_target_properties(KitchenSink_Geometry_Occt_Cpp PROPERTIES
        LIBRARY_OUTPUT_DIRECTORY ${OUTPUT_DIR}
        RUNTIME_OUTPUT_DIRECTORY ${OUTPUT_DIR}
)