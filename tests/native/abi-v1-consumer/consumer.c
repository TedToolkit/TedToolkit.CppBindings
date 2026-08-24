#include "ted_toolkit_occt_v1.h"
#include "ted_toolkit_occt_v1_test.h"

#include <stdint.h>
#include <stdio.h>
#include <string.h>

#define CHECK(condition)                                                                          \
    do                                                                                            \
    {                                                                                             \
        if (!(condition))                                                                         \
        {                                                                                         \
            fprintf(stderr, "check failed at line %d: %s\n", __LINE__, #condition);              \
            return 1;                                                                             \
        }                                                                                         \
    } while (0)

static int check_error_kind(int32_t scenario, ted_occt_v1_error_kind expected)
{
    ted_occt_v1_error error = ted_occt_v1_test_error(scenario);
    CHECK(error.kind == expected);
    if (expected != TED_OCCT_V1_ERROR_NONE && expected != (ted_occt_v1_error_kind)42)
    {
        CHECK(error.type_name != NULL);
        CHECK(error.message != NULL);
        CHECK(error.stack_trace != NULL);
    }

    ted_occt_v1_error_clear(&error);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(error.type_name == NULL);
    CHECK(error.message == NULL);
    CHECK(error.stack_trace == NULL);
    ted_occt_v1_error_clear(&error);
    return 0;
}

static int check_error_mapping(void)
{
    const int32_t scenarios[] = {
        TED_OCCT_V1_TEST_ERROR_ARGUMENT,
        TED_OCCT_V1_TEST_ERROR_ARGUMENT_OUT_OF_RANGE,
        TED_OCCT_V1_TEST_ERROR_ARITHMETIC,
        TED_OCCT_V1_TEST_ERROR_INVALID_OPERATION,
        TED_OCCT_V1_TEST_ERROR_NULL_OBJECT,
        TED_OCCT_V1_TEST_ERROR_OUT_OF_MEMORY,
        TED_OCCT_V1_TEST_ERROR_OVERFLOW,
        TED_OCCT_V1_TEST_ERROR_OCCT_FAILURE,
        TED_OCCT_V1_TEST_ERROR_STD_EXCEPTION,
        TED_OCCT_V1_TEST_ERROR_UNKNOWN,
        TED_OCCT_V1_TEST_ERROR_RESERVED,
        TED_OCCT_V1_TEST_ERROR_DERIVED_PRECEDENCE,
    };
    const ted_occt_v1_error_kind expected[] = {
        TED_OCCT_V1_ERROR_ARGUMENT,
        TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE,
        TED_OCCT_V1_ERROR_ARITHMETIC,
        TED_OCCT_V1_ERROR_INVALID_OPERATION,
        TED_OCCT_V1_ERROR_NULL_OBJECT,
        TED_OCCT_V1_ERROR_OUT_OF_MEMORY,
        TED_OCCT_V1_ERROR_OVERFLOW,
        TED_OCCT_V1_ERROR_OCCT_FAILURE,
        TED_OCCT_V1_ERROR_STD_EXCEPTION,
        TED_OCCT_V1_ERROR_UNKNOWN,
        (ted_occt_v1_error_kind)42,
        TED_OCCT_V1_ERROR_OUT_OF_MEMORY,
    };
    size_t index;

    for (index = 0; index < sizeof(scenarios) / sizeof(scenarios[0]); ++index)
    {
        CHECK(check_error_kind(scenarios[index], expected[index]) == 0);
    }

    for (index = 0; index < 3; ++index)
    {
        ted_occt_v1_test_fail_diagnostic_allocation_after((int32_t)index);
        ted_occt_v1_error error = ted_occt_v1_test_error(TED_OCCT_V1_TEST_ERROR_STD_EXCEPTION);
        CHECK(error.kind == TED_OCCT_V1_ERROR_STD_EXCEPTION);
        CHECK(error.type_name == NULL);
        CHECK(error.message == NULL);
        CHECK(error.stack_trace == NULL);
        ted_occt_v1_error_clear(&error);
    }

    ted_occt_v1_test_fail_diagnostic_allocation_after(-1);
    return 0;
}

static int check_point_and_transient(void)
{
    ted_occt_v1_pnt2d point = {0.0, 0.0};
    double coordinate = 0.0;
    ted_occt_v1_geom2d_cartesian_point* owner = NULL;
    ted_occt_v1_geom2d_cartesian_point* retained = NULL;
    ted_occt_v1_error error;

    error = ted_occt_v1_pnt2d_create__fc15364701135459bd24b26dfc9ccce3(1.25, -2.5, &point);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    error = ted_occt_v1_pnt2d_get_x__f43aaa7be0191777857affe8fef827ea(point, &coordinate);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(coordinate == 1.25);
    error = ted_occt_v1_pnt2d_get_y__6a8d60668e3f5de2ff324ce5fb730335(point, &coordinate);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(coordinate == -2.5);

    error = ted_occt_v1_geom2d_cartesian_point_create__96bdbb3c300bc0f459f28aa149790add(point, &owner);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(owner != NULL);
    error = ted_occt_v1_geom2d_cartesian_point_retain__165d4366c9435411dd1a9d645652ade5(owner, &retained);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(retained == owner);

    point.x = 9.0;
    point.y = 8.0;
    error = ted_occt_v1_geom2d_cartesian_point_set_point__51b12e0878a48055b7fc5a289590f736(owner, point);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    error = ted_occt_v1_geom2d_cartesian_point_release__f907996aa988462cfbd704eb26419694(&owner);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(owner == NULL);

    point.x = 0.0;
    point.y = 0.0;
    error = ted_occt_v1_geom2d_cartesian_point_get_point__ce411b720fa9fa6ca2e34af05e524aab(retained, &point);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(point.x == 9.0);
    CHECK(point.y == 8.0);
    error = ted_occt_v1_geom2d_cartesian_point_release__f907996aa988462cfbd704eb26419694(&retained);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(retained == NULL);
    error = ted_occt_v1_geom2d_cartesian_point_release__f907996aa988462cfbd704eb26419694(&retained);
    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    return 0;
}

static int check_utf8(void)
{
    const uint8_t input[] = {0x54, 0x65, 0x64, 0x20, 0xe4, 0xb8, 0xad, 0xe6, 0x96, 0x87};
    const ted_occt_v1_bytes_view view = {input, (uint64_t)sizeof(input)};
    ted_occt_v1_owned_bytes output = {NULL, 0};
    ted_occt_v1_error error =
        ted_occt_v1_ascii_string_copy_utf8__91cb86f8be22465dbb714f301f325044(view, &output);

    CHECK(error.kind == TED_OCCT_V1_ERROR_NONE);
    CHECK(output.length == (uint64_t)sizeof(input));
    CHECK(output.data != NULL);
    CHECK(memcmp(output.data, input, sizeof(input)) == 0);
    ted_occt_v1_owned_bytes_clear(&output);
    CHECK(output.data == NULL);
    CHECK(output.length == 0);
    ted_occt_v1_owned_bytes_clear(&output);
    return 0;
}

int main(void)
{
    CHECK(ted_occt_v1_abi_version() == TED_OCCT_V1_ABI_VERSION);
    CHECK(check_error_mapping() == 0);
    CHECK(check_point_and_transient() == 0);
    CHECK(check_utf8() == 0);
    return 0;
}
