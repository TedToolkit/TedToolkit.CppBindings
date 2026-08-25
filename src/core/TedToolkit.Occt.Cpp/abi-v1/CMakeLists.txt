cmake_minimum_required(VERSION 3.28)

project(ted_toolkit_occt_abi_v1 LANGUAGES CXX)

find_package(OpenCASCADE CONFIG REQUIRED)

add_library(ted_toolkit_occt_abi_v1 SHARED ted_toolkit_occt_v1.cpp)
set_target_properties(ted_toolkit_occt_abi_v1
    PROPERTIES OUTPUT_NAME "@TED_OCCT_V1_NATIVE_LIBRARY_BASENAME@")
target_compile_features(ted_toolkit_occt_abi_v1 PRIVATE cxx_std_20)
target_compile_definitions(ted_toolkit_occt_abi_v1 PRIVATE TED_OCCT_V1_BUILD)
target_include_directories(ted_toolkit_occt_abi_v1
    PUBLIC ${CMAKE_CURRENT_SOURCE_DIR}
    PRIVATE ${OpenCASCADE_INCLUDE_DIR})
target_link_libraries(ted_toolkit_occt_abi_v1 PRIVATE ${OpenCASCADE_LIBRARIES})

if(MSVC OR CMAKE_CXX_SIMULATE_ID STREQUAL "MSVC")
    target_compile_options(ted_toolkit_occt_abi_v1 PRIVATE /EHsc /W4 /WX)
endif()
