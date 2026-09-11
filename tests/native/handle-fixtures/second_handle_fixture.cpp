#include <cstdint>

#if defined(_WIN32)
#define TED_OCCT_EXPORT extern "C" __declspec(dllexport)
#define TED_OCCT_CDECL __cdecl
#else
#define TED_OCCT_EXPORT extern "C" __attribute__((visibility("default")))
#define TED_OCCT_CDECL
#endif

struct SecondTransient final
{
    std::int32_t value;
    std::int32_t* release_count;
};

TED_OCCT_EXPORT SecondTransient* TED_OCCT_CDECL second_transient_create(
    const std::int32_t value,
    std::int32_t* const release_count) noexcept
{
    return new SecondTransient{value, release_count};
}

TED_OCCT_EXPORT void TED_OCCT_CDECL second_transient_release(SecondTransient* const value) noexcept
{
    ++*value->release_count;
    delete value;
}
