#include <atomic>
#include <cstdlib>
#include <cstring>

struct NativeError
{
    int Kind;
    char* TypeName;
    char* Message;
    char* StackTrace;
};

struct NativeResult
{
    int Tag;
    double AX;
    double AY;
    double BX;
    double BY;
};

namespace
{
std::atomic<int> diagnosticClearCount{};
std::atomic<int> containerCreateCount{};
std::atomic<int> containerDestroyCount{};
std::atomic<int> alternativeTransferCount{};

char* CopyText(const char* value)
{
    if (value == nullptr)
    {
        return nullptr;
    }

    const auto size = std::strlen(value) + 1;
    auto* copy = static_cast<char*>(std::malloc(size));
    if (copy != nullptr)
    {
        std::memcpy(copy, value, size);
    }

    return copy;
}

struct TrackedContainer
{
    TrackedContainer()
    {
        ++containerCreateCount;
    }

    ~TrackedContainer()
    {
        ++containerDestroyCount;
    }
};
}

#define CGAL_RUNTIME_EXPORT extern "C" __declspec(dllexport)

CGAL_RUNTIME_EXPORT void CgalRuntime_ResetCounters() noexcept
{
    diagnosticClearCount = 0;
    containerCreateCount = 0;
    containerDestroyCount = 0;
    alternativeTransferCount = 0;
}

CGAL_RUNTIME_EXPORT int CgalRuntime_GetDiagnosticClearCount() noexcept
{
    return diagnosticClearCount;
}

CGAL_RUNTIME_EXPORT int CgalRuntime_GetContainerCreateCount() noexcept
{
    return containerCreateCount;
}

CGAL_RUNTIME_EXPORT int CgalRuntime_GetContainerDestroyCount() noexcept
{
    return containerDestroyCount;
}

CGAL_RUNTIME_EXPORT int CgalRuntime_GetAlternativeTransferCount() noexcept
{
    return alternativeTransferCount;
}

CGAL_RUNTIME_EXPORT void CgalRuntime_CreateError(
    int kind,
    int malformedUtf8,
    NativeError* error) noexcept
{
    if (error == nullptr)
    {
        return;
    }

    static constexpr char MalformedUtf8[] = {static_cast<char>(0xC3), '(', '\0'};
    error->Kind = kind;
    error->TypeName = CopyText("fixture::native_failure");
    error->Message = CopyText(malformedUtf8 == 0 ? "fixture failure" : MalformedUtf8);
    error->StackTrace = CopyText("fixture.cpp:42");
}

CGAL_RUNTIME_EXPORT void CgalRuntime_ClearError(NativeError* error) noexcept
{
    if (error == nullptr)
    {
        return;
    }

    ++diagnosticClearCount;
    std::free(error->TypeName);
    std::free(error->Message);
    std::free(error->StackTrace);
    *error = {};
}

CGAL_RUNTIME_EXPORT void CgalRuntime_CreateResult(int scenario, NativeResult* result) noexcept
{
    if (result == nullptr)
    {
        return;
    }

    TrackedContainer container;
    switch (scenario)
    {
        case 0:
            *result = {};
            return;
        case 1:
            ++alternativeTransferCount;
            *result = {1, 1.0, 2.0, 0.0, 0.0};
            return;
        case 2:
            ++alternativeTransferCount;
            *result = {2, 1.0, 2.0, 3.0, 4.0};
            return;
        case 3:
            *result = {42, 0.0, 0.0, 0.0, 0.0};
            return;
        default:
            *result = {255, 0.0, 0.0, 0.0, 0.0};
            return;
    }
}
