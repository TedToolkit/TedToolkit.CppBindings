cmake_minimum_required(VERSION 3.28)
project(CgalRuntimeFixture LANGUAGES CXX)

find_package(CGAL CONFIG REQUIRED)

file(
    WRITE "${CMAKE_BINARY_DIR}/compiler-identity.txt"
    "${CMAKE_CXX_COMPILER_ID}|${CMAKE_CXX_COMPILER_VERSION}\n")

add_library(cgal_runtime_fixture SHARED CgalRuntimeFixture.cpp)
target_compile_features(cgal_runtime_fixture PRIVATE cxx_std_20)
target_compile_definitions(cgal_runtime_fixture PRIVATE CGAL_DEBUG)
target_link_libraries(cgal_runtime_fixture PRIVATE CGAL::CGAL)

foreach(configuration Debug Release RelWithDebInfo MinSizeRel)
    string(TOUPPER "${configuration}" configuration_upper)
    set_target_properties(
        cgal_runtime_fixture
        PROPERTIES RUNTIME_OUTPUT_DIRECTORY_${configuration_upper} "${CMAKE_RUNTIME_OUTPUT_DIRECTORY}")
endforeach()
