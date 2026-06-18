#ifndef CSHARP_INTEROP_H
#define CSHARP_INTEROP_H

#ifndef WRAP_CALL_CUSTOM_CATCH
#define WRAP_CALL_CUSTOM_CATCH
#include <Standard_Failure.hxx>
#endif

#include <cstring>
#include <exception>
#include <typeinfo>

#ifdef _WIN32
#define API_EXPORT extern "C" __declspec(dllexport)
#else
#define API_EXPORT extern "C" __attribute__((visibility("default")))
#endif

using interop_char_t = char;

struct interop_error
{
    interop_char_t* type_name;
    interop_char_t* message;
    interop_char_t* stack_trace;
};

inline interop_char_t* copy_to_heap(const char* text) noexcept
{
    if (text == nullptr)
    {
        return nullptr;
    }

    const size_t length = std::strlen(text);
    auto* buffer = new interop_char_t[length + 1];
    std::memcpy(buffer, text, length + 1);
    return buffer;
}

inline interop_error make_error(
    const char* type_name,
    const char* message,
    const char* stack_trace = nullptr) noexcept
{
    return {
        copy_to_heap(type_name),
        copy_to_heap(message),
        copy_to_heap(stack_trace)
    };
}

template <class TAction>
interop_error wrap_call(TAction&& action) noexcept
{
    try
    {
        action();
        return { nullptr, nullptr, nullptr };
    }
    catch (const Standard_Failure& ex)
    {
        return make_error(typeid(ex).name(), ex.what(), ex.GetStackString());
    }
    catch (const std::exception& ex)
    {
        return make_error(typeid(ex).name(), ex.what());
    }
    catch (...)
    {
        return make_error("unknown", "Unknown error occurred.");
    }
}

// ReSharper disable once CppPassValueParameterByConstReference
API_EXPORT void free_error(const interop_error error) noexcept
{
    delete[] error.type_name;
    delete[] error.message;
    delete[] error.stack_trace;
}

#define CSHARP_WRAPPER(FUNC_DECL, BODY) \
API_EXPORT void FUNC_DECL noexcept { \
    [&]() BODY(); \
}

#define CSHARP_WRAPPER_TRY(FUNC_DECL, BODY) \
API_EXPORT interop_error FUNC_DECL noexcept { \
    return wrap_call([&]() BODY); \
}

#endif //CSHARP_INTEROP_H
