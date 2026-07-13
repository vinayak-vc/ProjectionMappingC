#include "PMSDK/Core/Version.h"

// PMSDK_VERSION_* are injected by CMake from the project() version so the
// build system remains the single source of truth.
static_assert(pmsdk::kHeaderVersion.major == PMSDK_VERSION_MAJOR &&
                  pmsdk::kHeaderVersion.minor == PMSDK_VERSION_MINOR &&
                  pmsdk::kHeaderVersion.patch == PMSDK_VERSION_PATCH,
              "PMSDK/Core/Version.h kHeaderVersion is out of sync with the CMake project version");

namespace pmsdk {

Version GetVersion() noexcept {
    return Version{PMSDK_VERSION_MAJOR, PMSDK_VERSION_MINOR, PMSDK_VERSION_PATCH};
}

std::string_view GetVersionString() noexcept {
    return "0.1.0";
}

} // namespace pmsdk::Core

// MSVC 17.10+ ABI mismatch workaround for OpenCV static linkage
extern "C" {
    unsigned long long __std_find_first_not_of_trivial_pos_1(const char* const, unsigned long long, const char* const, unsigned long long) {
        return (unsigned long long)-1;
    }
    unsigned long long __std_find_last_not_of_trivial_pos_1(const char* const, unsigned long long, const char* const, unsigned long long) {
        return (unsigned long long)-1;
    }
}
