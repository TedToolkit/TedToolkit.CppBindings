#include "ted_toolkit_occt_v1.h"

#include <Geom2d_CartesianPoint.hxx>
#include <Standard_ConstructionError.hxx>
#include <Standard_DimensionError.hxx>
#include <Standard_DimensionMismatch.hxx>
#include <Standard_DomainError.hxx>
#include <Standard_Failure.hxx>
#include <Standard_NoSuchObject.hxx>
#include <Standard_NullObject.hxx>
#include <Standard_NullValue.hxx>
#include <Standard_OutOfMemory.hxx>
#include <Standard_OutOfRange.hxx>
#include <Standard_Overflow.hxx>
#include <Standard_ProgramError.hxx>
#include <Standard_RangeError.hxx>
#include <Standard_Underflow.hxx>
#include <TCollection_AsciiString.hxx>
#include <gp_Pnt2d.hxx>

#include <cstddef>
#include <cstring>
#include <limits>
#include <new>
#include <stdexcept>
#include <utility>

#if defined(TED_OCCT_V1_BUILD_TESTING)
#include "ted_toolkit_occt_v1_test.h"
#endif

namespace
{
#if defined(TED_OCCT_V1_BUILD_TESTING)
thread_local int diagnostic_allocation_countdown = -1;
#endif

ted_occt_v1_error empty_error() noexcept
{
    return {TED_OCCT_V1_ERROR_NONE, nullptr, nullptr, nullptr};
}

char* copy_diagnostic(const char* value) noexcept
{
#if defined(TED_OCCT_V1_BUILD_TESTING)
    if (diagnostic_allocation_countdown == 0)
    {
        return nullptr;
    }

    if (diagnostic_allocation_countdown > 0)
    {
        --diagnostic_allocation_countdown;
    }
#endif

    const char* source = value == nullptr ? "" : value;
    const std::size_t length = std::strlen(source);
    char* copy = new (std::nothrow) char[length + 1];
    if (copy == nullptr)
    {
        return nullptr;
    }

    std::memcpy(copy, source, length + 1);
    return copy;
}

ted_occt_v1_error make_error(
    ted_occt_v1_error_kind kind,
    const char* type_name,
    const char* message,
    const char* stack_trace) noexcept
{
    ted_occt_v1_error error{kind, nullptr, nullptr, nullptr};
    char* owned_type_name = copy_diagnostic(type_name);
    char* owned_message = copy_diagnostic(message);
    char* owned_stack_trace = copy_diagnostic(stack_trace);
    if (owned_type_name == nullptr || owned_message == nullptr || owned_stack_trace == nullptr)
    {
        delete[] owned_type_name;
        delete[] owned_message;
        delete[] owned_stack_trace;
        return error;
    }

    error.type_name = owned_type_name;
    error.message = owned_message;
    error.stack_trace = owned_stack_trace;
    return error;
}

ted_occt_v1_error make_occt_error(ted_occt_v1_error_kind kind, const Standard_Failure& error) noexcept
{
    const char* stack_trace = "";
    try
    {
        stack_trace = error.GetStackString();
    }
    catch (...)
    {
    }

    return make_error(kind, error.ExceptionType(), error.what(), stack_trace);
}

template <typename TAction>
ted_occt_v1_error invoke(TAction&& action) noexcept
{
    try
    {
        std::forward<TAction>(action)();
        return empty_error();
    }
    catch (const Standard_OutOfMemory& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_OUT_OF_MEMORY, error);
    }
    catch (const Standard_NullObject& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_NULL_OBJECT, error);
    }
    catch (const Standard_NullValue& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_NULL_OBJECT, error);
    }
    catch (const Standard_NoSuchObject& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_INVALID_OPERATION, error);
    }
    catch (const Standard_ProgramError& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_INVALID_OPERATION, error);
    }
    catch (const Standard_OutOfRange& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE, error);
    }
    catch (const Standard_RangeError& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE, error);
    }
    catch (const Standard_Overflow& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_OVERFLOW, error);
    }
    catch (const Standard_Underflow& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARITHMETIC, error);
    }
    catch (const Standard_ConstructionError& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT, error);
    }
    catch (const Standard_DimensionMismatch& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT, error);
    }
    catch (const Standard_DimensionError& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT, error);
    }
    catch (const Standard_DomainError& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_ARGUMENT, error);
    }
    catch (const Standard_Failure& error)
    {
        return make_occt_error(TED_OCCT_V1_ERROR_OCCT_FAILURE, error);
    }
    catch (const std::bad_alloc& error)
    {
        return make_error(TED_OCCT_V1_ERROR_OUT_OF_MEMORY, "std::bad_alloc", error.what(), "");
    }
    catch (const std::out_of_range& error)
    {
        return make_error(TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE, "std::out_of_range", error.what(), "");
    }
    catch (const std::overflow_error& error)
    {
        return make_error(TED_OCCT_V1_ERROR_OVERFLOW, "std::overflow_error", error.what(), "");
    }
    catch (const std::underflow_error& error)
    {
        return make_error(TED_OCCT_V1_ERROR_ARITHMETIC, "std::underflow_error", error.what(), "");
    }
    catch (const std::invalid_argument& error)
    {
        return make_error(TED_OCCT_V1_ERROR_ARGUMENT, "std::invalid_argument", error.what(), "");
    }
    catch (const std::domain_error& error)
    {
        return make_error(TED_OCCT_V1_ERROR_ARGUMENT, "std::domain_error", error.what(), "");
    }
    catch (const std::logic_error& error)
    {
        return make_error(TED_OCCT_V1_ERROR_INVALID_OPERATION, "std::logic_error", error.what(), "");
    }
    catch (const std::exception& error)
    {
        return make_error(TED_OCCT_V1_ERROR_STD_EXCEPTION, "std::exception", error.what(), "");
    }
    catch (...)
    {
        return make_error(TED_OCCT_V1_ERROR_UNKNOWN, "unknown", "Unknown native exception", "");
    }
}

ted_occt_v1_error argument_error(const char* message) noexcept
{
    return make_error(TED_OCCT_V1_ERROR_ARGUMENT, "ted_occt_v1_argument", message, "");
}

Geom2d_CartesianPoint* unwrap(ted_occt_v1_geom2d_cartesian_point* value) noexcept
{
    return reinterpret_cast<Geom2d_CartesianPoint*>(value);
}

const Geom2d_CartesianPoint* unwrap(const ted_occt_v1_geom2d_cartesian_point* value) noexcept
{
    return reinterpret_cast<const Geom2d_CartesianPoint*>(value);
}

ted_occt_v1_geom2d_cartesian_point* wrap(Geom2d_CartesianPoint* value) noexcept
{
    return reinterpret_cast<ted_occt_v1_geom2d_cartesian_point*>(value);
}
}

uint32_t TED_OCCT_V1_CALL ted_occt_v1_abi_version(void)
{
    return TED_OCCT_V1_ABI_VERSION;
}

void TED_OCCT_V1_CALL ted_occt_v1_error_clear(ted_occt_v1_error* error)
{
    if (error == nullptr)
    {
        return;
    }

    delete[] error->type_name;
    delete[] error->message;
    delete[] error->stack_trace;
    *error = empty_error();
}

void TED_OCCT_V1_CALL ted_occt_v1_owned_bytes_clear(ted_occt_v1_owned_bytes* buffer)
{
    if (buffer == nullptr)
    {
        return;
    }

    delete[] buffer->data;
    buffer->data = nullptr;
    buffer->length = 0;
}

ted_occt_v1_error TED_OCCT_V1_CALL ted_occt_v1_pnt2d_create__fc15364701135459bd24b26dfc9ccce3(
    double x,
    double y,
    ted_occt_v1_pnt2d* result)
{
    if (result == nullptr)
    {
        return argument_error("result is required");
    }

    return invoke([&]() { *result = {gp_Pnt2d(x, y).X(), gp_Pnt2d(x, y).Y()}; });
}

ted_occt_v1_error TED_OCCT_V1_CALL ted_occt_v1_pnt2d_get_x__f43aaa7be0191777857affe8fef827ea(
    ted_occt_v1_pnt2d point,
    double* result)
{
    if (result == nullptr)
    {
        return argument_error("result is required");
    }

    return invoke([&]() { *result = gp_Pnt2d(point.x, point.y).X(); });
}

ted_occt_v1_error TED_OCCT_V1_CALL ted_occt_v1_pnt2d_get_y__6a8d60668e3f5de2ff324ce5fb730335(
    ted_occt_v1_pnt2d point,
    double* result)
{
    if (result == nullptr)
    {
        return argument_error("result is required");
    }

    return invoke([&]() { *result = gp_Pnt2d(point.x, point.y).Y(); });
}

ted_occt_v1_error TED_OCCT_V1_CALL
ted_occt_v1_geom2d_cartesian_point_create__96bdbb3c300bc0f459f28aa149790add(
    ted_occt_v1_pnt2d point,
    ted_occt_v1_geom2d_cartesian_point** result)
{
    if (result == nullptr || *result != nullptr)
    {
        return argument_error("result must be a non-null empty owner slot");
    }

    return invoke([&]() {
        Geom2d_CartesianPoint* value = new Geom2d_CartesianPoint(gp_Pnt2d(point.x, point.y));
        value->IncrementRefCounter();
        *result = wrap(value);
    });
}

ted_occt_v1_error TED_OCCT_V1_CALL
ted_occt_v1_geom2d_cartesian_point_get_point__ce411b720fa9fa6ca2e34af05e524aab(
    const ted_occt_v1_geom2d_cartesian_point* self,
    ted_occt_v1_pnt2d* result)
{
    if (self == nullptr || result == nullptr)
    {
        return argument_error("self and result are required");
    }

    return invoke([&]() {
        const gp_Pnt2d point = unwrap(self)->Pnt2d();
        *result = {point.X(), point.Y()};
    });
}

ted_occt_v1_error TED_OCCT_V1_CALL
ted_occt_v1_geom2d_cartesian_point_set_point__51b12e0878a48055b7fc5a289590f736(
    ted_occt_v1_geom2d_cartesian_point* self,
    ted_occt_v1_pnt2d point)
{
    if (self == nullptr)
    {
        return argument_error("self is required");
    }

    return invoke([&]() { unwrap(self)->SetPnt2d(gp_Pnt2d(point.x, point.y)); });
}

ted_occt_v1_error TED_OCCT_V1_CALL
ted_occt_v1_geom2d_cartesian_point_retain__165d4366c9435411dd1a9d645652ade5(
    const ted_occt_v1_geom2d_cartesian_point* self,
    ted_occt_v1_geom2d_cartesian_point** result)
{
    if (self == nullptr || result == nullptr || *result != nullptr)
    {
        return argument_error("self and a non-null empty result owner slot are required");
    }

    Geom2d_CartesianPoint* value = const_cast<Geom2d_CartesianPoint*>(unwrap(self));
    value->IncrementRefCounter();
    *result = wrap(value);
    return empty_error();
}

ted_occt_v1_error TED_OCCT_V1_CALL
ted_occt_v1_geom2d_cartesian_point_release__f907996aa988462cfbd704eb26419694(
    ted_occt_v1_geom2d_cartesian_point** owner)
{
    if (owner == nullptr)
    {
        return argument_error("owner slot is required");
    }

    Geom2d_CartesianPoint* value = unwrap(*owner);
    *owner = nullptr;
    if (value != nullptr && value->DecrementRefCounter() == 0)
    {
        value->Delete();
    }

    return empty_error();
}

ted_occt_v1_error TED_OCCT_V1_CALL ted_occt_v1_ascii_string_copy_utf8__91cb86f8be22465dbb714f301f325044(
    ted_occt_v1_bytes_view text,
    ted_occt_v1_owned_bytes* result)
{
    if (result == nullptr || result->data != nullptr || result->length != 0)
    {
        return argument_error("result must be a non-null empty owner slot");
    }

    if (text.data == nullptr && text.length != 0)
    {
        return argument_error("a non-empty input requires data");
    }

    if (text.length > static_cast<uint64_t>((std::numeric_limits<int>::max)()))
    {
        return make_error(TED_OCCT_V1_ERROR_ARGUMENT_OUT_OF_RANGE, "ted_occt_v1_length",
            "text length exceeds the OCCT string range", "");
    }

    return invoke([&]() {
        if (text.length == 0)
        {
            return;
        }

        TCollection_AsciiString value(
            reinterpret_cast<const char*>(text.data),
            static_cast<int>(text.length));
        const uint64_t length = static_cast<uint64_t>(value.Length());
        uint8_t* copy = new uint8_t[static_cast<std::size_t>(length)];
        std::memcpy(copy, value.ToCString(), static_cast<std::size_t>(length));
        result->data = copy;
        result->length = length;
    });
}

#if defined(TED_OCCT_V1_BUILD_TESTING)
void TED_OCCT_V1_CALL ted_occt_v1_test_fail_diagnostic_allocation_after(int32_t successful_allocations)
{
    diagnostic_allocation_countdown = successful_allocations;
}

ted_occt_v1_error TED_OCCT_V1_CALL ted_occt_v1_test_error(int32_t scenario)
{
    if (scenario == TED_OCCT_V1_TEST_ERROR_RESERVED)
    {
        return {static_cast<ted_occt_v1_error_kind>(42), nullptr, nullptr, nullptr};
    }

    return invoke([&]() {
        switch (scenario)
        {
        case TED_OCCT_V1_TEST_ERROR_ARGUMENT:
            throw Standard_DomainError("argument");
        case TED_OCCT_V1_TEST_ERROR_ARGUMENT_OUT_OF_RANGE:
            throw Standard_OutOfRange("range");
        case TED_OCCT_V1_TEST_ERROR_ARITHMETIC:
            throw Standard_Underflow("underflow");
        case TED_OCCT_V1_TEST_ERROR_INVALID_OPERATION:
            throw Standard_ProgramError("operation");
        case TED_OCCT_V1_TEST_ERROR_NULL_OBJECT:
            throw Standard_NullObject("null");
        case TED_OCCT_V1_TEST_ERROR_OUT_OF_MEMORY:
            throw std::bad_alloc();
        case TED_OCCT_V1_TEST_ERROR_OVERFLOW:
            throw Standard_Overflow("overflow");
        case TED_OCCT_V1_TEST_ERROR_OCCT_FAILURE:
            throw Standard_Failure("occt");
        case TED_OCCT_V1_TEST_ERROR_STD_EXCEPTION:
            throw std::runtime_error("standard");
        case TED_OCCT_V1_TEST_ERROR_UNKNOWN:
            throw 42;
        case TED_OCCT_V1_TEST_ERROR_DERIVED_PRECEDENCE:
            throw Standard_OutOfMemory("derived precedence");
        default:
            throw std::invalid_argument("unknown test scenario");
        }
    });
}

uint64_t TED_OCCT_V1_CALL ted_occt_v1_test_layout(int32_t query)
{
    switch (query)
    {
    case TED_OCCT_V1_TEST_LAYOUT_ERROR_SIZE:
        return sizeof(ted_occt_v1_error);
    case TED_OCCT_V1_TEST_LAYOUT_ERROR_KIND:
        return offsetof(ted_occt_v1_error, kind);
    case TED_OCCT_V1_TEST_LAYOUT_ERROR_TYPE_NAME:
        return offsetof(ted_occt_v1_error, type_name);
    case TED_OCCT_V1_TEST_LAYOUT_ERROR_MESSAGE:
        return offsetof(ted_occt_v1_error, message);
    case TED_OCCT_V1_TEST_LAYOUT_ERROR_STACK_TRACE:
        return offsetof(ted_occt_v1_error, stack_trace);
    case TED_OCCT_V1_TEST_LAYOUT_PNT2D_SIZE:
        return sizeof(ted_occt_v1_pnt2d);
    case TED_OCCT_V1_TEST_LAYOUT_PNT2D_X:
        return offsetof(ted_occt_v1_pnt2d, x);
    case TED_OCCT_V1_TEST_LAYOUT_PNT2D_Y:
        return offsetof(ted_occt_v1_pnt2d, y);
    case TED_OCCT_V1_TEST_LAYOUT_BYTES_VIEW_SIZE:
        return sizeof(ted_occt_v1_bytes_view);
    case TED_OCCT_V1_TEST_LAYOUT_BYTES_VIEW_DATA:
        return offsetof(ted_occt_v1_bytes_view, data);
    case TED_OCCT_V1_TEST_LAYOUT_BYTES_VIEW_LENGTH:
        return offsetof(ted_occt_v1_bytes_view, length);
    case TED_OCCT_V1_TEST_LAYOUT_OWNED_BYTES_SIZE:
        return sizeof(ted_occt_v1_owned_bytes);
    case TED_OCCT_V1_TEST_LAYOUT_OWNED_BYTES_DATA:
        return offsetof(ted_occt_v1_owned_bytes, data);
    case TED_OCCT_V1_TEST_LAYOUT_OWNED_BYTES_LENGTH:
        return offsetof(ted_occt_v1_owned_bytes, length);
    default:
        return (std::numeric_limits<uint64_t>::max)();
    }
}
#endif
