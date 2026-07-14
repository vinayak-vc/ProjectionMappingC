#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <string_view>

#include "PMSDK/Core/Export.h"

namespace pmsdk {

/// Thread-safe key/value configuration store. Values are typed (bool, int64,
/// double, string); a getter whose type does not match the stored value returns
/// the caller-supplied default rather than converting.
class Config {
public:
    PMSDK_API Config();
    PMSDK_API ~Config();

    Config(const Config&) = delete;
    Config& operator=(const Config&) = delete;
    Config(Config&&) = delete;
    Config& operator=(Config&&) = delete;

    /// Sets or replaces the value stored under `key`.
    PMSDK_API void SetBool(std::string_view key, bool value);
    PMSDK_API void SetInt(std::string_view key, std::int64_t value);
    PMSDK_API void SetDouble(std::string_view key, double value);
    PMSDK_API void SetString(std::string_view key, std::string_view value);

    /// Returns the stored value, or `defaultValue` when the key is missing or
    /// holds a different type.
    [[nodiscard]] PMSDK_API bool GetBool(std::string_view key, bool defaultValue) const noexcept;
    [[nodiscard]] PMSDK_API std::int64_t GetInt(std::string_view key,
                                                std::int64_t defaultValue) const noexcept;
    [[nodiscard]] PMSDK_API double GetDouble(std::string_view key,
                                             double defaultValue) const noexcept;
    [[nodiscard]] PMSDK_API std::string GetString(std::string_view key,
                                                  std::string_view defaultValue) const;

    /// True when a value of any type is stored under `key`.
    [[nodiscard]] PMSDK_API bool Contains(std::string_view key) const noexcept;

    /// Removes `key`. Returns true when a value was actually removed.
    PMSDK_API bool Remove(std::string_view key) noexcept;

    /// Removes all values.
    PMSDK_API void Clear() noexcept;

    /// Number of stored values.
    [[nodiscard]] PMSDK_API std::size_t Size() const noexcept;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace pmsdk
