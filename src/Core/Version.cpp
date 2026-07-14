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
    return PMSDK_VERSION_STRING;
}

} // namespace pmsdk::Core

