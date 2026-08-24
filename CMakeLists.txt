cmake_minimum_required(VERSION 3.28)

project(ted_occt_abi_v1_consumer LANGUAGES C CXX)

set(TED_OCCT_V1_GENERATED_DIR "${CMAKE_BINARY_DIR}/generated")
execute_process(
    COMMAND dotnet run
        --project "${CMAKE_SOURCE_DIR}/tests/TedToolkit.Occt.Console/TedToolkit.Occt.Console.csproj"
        --configuration Release
        --no-restore
        --no-build
        --
        --materialize-abi-v1
        "${TED_OCCT_V1_GENERATED_DIR}"
    WORKING_DIRECTORY "${CMAKE_SOURCE_DIR}"
    RESULT_VARIABLE TED_OCCT_V1_MATERIALIZE_RESULT
    COMMAND_ECHO STDOUT)

if(NOT TED_OCCT_V1_MATERIALIZE_RESULT EQUAL 0)
    message(FATAL_ERROR "ABI-major-1 native project materialization failed: ${TED_OCCT_V1_MATERIALIZE_RESULT}")
endif()

set(TED_OCCT_V1_BUILD_TESTING ON CACHE BOOL "Build ABI-major-1 test hooks" FORCE)
add_subdirectory("${TED_OCCT_V1_GENERATED_DIR}" abi-v1)

add_executable(ted_occt_v1_c_consumer tests/native/abi-v1-consumer/consumer.c)
target_compile_features(ted_occt_v1_c_consumer PRIVATE c_std_11)
target_link_libraries(ted_occt_v1_c_consumer PRIVATE ted_toolkit_occt_abi_v1)

if(MSVC OR CMAKE_C_SIMULATE_ID STREQUAL "MSVC")
    target_compile_options(ted_occt_v1_c_consumer PRIVATE /W4 /WX)
endif()

enable_testing()
add_test(NAME ted_occt_v1_c_consumer COMMAND ted_occt_v1_c_consumer)
set_tests_properties(ted_occt_v1_c_consumer PROPERTIES
    ENVIRONMENT_MODIFICATION
        "PATH=path_list_prepend:$<TARGET_FILE_DIR:ted_toolkit_occt_abi_v1>")
