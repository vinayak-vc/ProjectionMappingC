#pragma once

#include <cstdint>
#include <string_view>

#include "PMSDK/Core/Export.h"

namespace pmsdk {

/// Semantic version of the SDK.
struct Version {
    std::uint32_t major = 0;
    std::uint32_t minor = 0;
    std::uint32_t patch = 0;

    [[nodiscard]] friend constexpr bool operator==(const Version&, const Version&) = default;
};

/// Compile-time version of the headers this translation unit was built against.
/// Kept in sync with the CMake project version (enforced by a static_assert in Version.cpp).
inline constexpr Version kHeaderVersion{0, 1, 0};

/// Runtime version of the linked PMSDK binary. Compare against kHeaderVersion
/// to detect header/binary mismatches across the DLL boundary.
[[nodiscard]] PMSDK_API Version GetVersion() noexcept;

/// Runtime version of the linked PMSDK binary as "major.minor.patch".
/// The returned view points at static storage inside the SDK binary.
[[nodiscard]] PMSDK_API std::string_view GetVersionString() noexcept;

} // namespace pmsdk
