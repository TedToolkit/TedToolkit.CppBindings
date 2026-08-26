#include <cstdint>

#if defined(_WIN32)
#define BENCH_EXPORT extern "C" __declspec(dllexport)
#define BENCH_NOINLINE __declspec(noinline)
#else
#define BENCH_EXPORT extern "C" __attribute__((visibility("default")))
#define BENCH_NOINLINE __attribute__((noinline))
#endif

namespace
{
struct Owner final
{
    std::uint64_t value;
};
}

BENCH_EXPORT BENCH_NOINLINE void release_probe(std::uint64_t* counter) noexcept
{
    ++(*counter);
}

BENCH_EXPORT BENCH_NOINLINE void* create_owner() noexcept
{
    return new Owner{42};
}

BENCH_EXPORT BENCH_NOINLINE void release_owner(void* owner) noexcept
{
    delete static_cast<Owner*>(owner);
}
