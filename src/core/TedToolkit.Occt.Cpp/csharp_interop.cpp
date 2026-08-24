#include "csharp_interop.h"

// ReSharper disable once CppPassValueParameterByConstReference
API_EXPORT void free_error(interop_error error) noexcept
{
    delete[] error.type_name;
    delete[] error.message;
    delete[] error.stack_trace;
}
