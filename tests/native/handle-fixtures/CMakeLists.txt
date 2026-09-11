cmake_minimum_required(VERSION 3.28)

project(ted_occt_handle_fixtures LANGUAGES CXX)

add_library(ted_occt_first_handle_fixture SHARED first_handle_fixture.cpp)
add_library(ted_occt_second_handle_fixture SHARED second_handle_fixture.cpp)

target_compile_features(ted_occt_first_handle_fixture PRIVATE cxx_std_20)
target_compile_features(ted_occt_second_handle_fixture PRIVATE cxx_std_20)

if(MSVC OR CMAKE_CXX_SIMULATE_ID STREQUAL "MSVC")
    target_compile_options(ted_occt_first_handle_fixture PRIVATE /EHsc /W4 /WX)
    target_compile_options(ted_occt_second_handle_fixture PRIVATE /EHsc /W4 /WX)
else()
    target_compile_options(ted_occt_first_handle_fixture PRIVATE -Wall -Wextra -Werror)
    target_compile_options(ted_occt_second_handle_fixture PRIVATE -Wall -Wextra -Werror)
endif()

enable_testing()
add_test(
    NAME ted_occt_managed_handle_native_integration
    COMMAND dotnet
        "${CMAKE_CURRENT_SOURCE_DIR}/../../TedToolkit.CppBindings.Runtime.NativeIntegration/bin/Release/net8.0/TedToolkit.CppBindings.Runtime.NativeIntegration.dll"
        "$<TARGET_FILE:ted_occt_first_handle_fixture>"
        "$<TARGET_FILE:ted_occt_second_handle_fixture>")
