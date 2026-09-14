cmake_minimum_required(VERSION 3.20)
project(ted_occt_release_benchmark LANGUAGES CXX)

add_library(ted_occt_release_benchmark SHARED release_benchmark.cpp)
target_compile_features(ted_occt_release_benchmark PRIVATE cxx_std_17)
set_target_properties(ted_occt_release_benchmark PROPERTIES
    CXX_VISIBILITY_PRESET hidden
    VISIBILITY_INLINES_HIDDEN YES)
