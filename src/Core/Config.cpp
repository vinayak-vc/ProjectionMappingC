#include "PMSDK/Core/Config.h"

#include <mutex>
#include <shared_mutex>
#include <unordered_map>
#include <variant>

namespace pmsdk {

namespace {
using ConfigValue = std::variant<bool, std::int64_t, double, std::string>;
}

struct Config::Impl {
    mutable std::shared_mutex mutex;
    std::unordered_map<std::string, ConfigValue> values;

    void Set(std::string_view key, ConfigValue value) {
        std::unique_lock lock(mutex);
        values.insert_or_assign(std::string(key), std::move(value));
    }

    template <typename T, typename Default>
    T Get(std::string_view key, Default defaultValue) const {
        std::shared_lock lock(mutex);
        const auto it = values.find(std::string(key));
        if (it == values.end() || !std::holds_alternative<T>(it->second)) {
            return T(defaultValue);
        }
        return std::get<T>(it->second);
    }
};

Config::Config() : impl_(std::make_unique<Impl>()) {}

Config::~Config() = default;

void Config::SetBool(std::string_view key, bool value) {
    impl_->Set(key, ConfigValue(value));
}

void Config::SetInt(std::string_view key, std::int64_t value) {
    impl_->Set(key, ConfigValue(value));
}

void Config::SetDouble(std::string_view key, double value) {
    impl_->Set(key, ConfigValue(value));
}

void Config::SetString(std::string_view key, std::string_view value) {
    impl_->Set(key, ConfigValue(std::string(value)));
}

bool Config::GetBool(std::string_view key, bool defaultValue) const noexcept {
    return impl_->Get<bool>(key, defaultValue);
}

std::int64_t Config::GetInt(std::string_view key, std::int64_t defaultValue) const noexcept {
    return impl_->Get<std::int64_t>(key, defaultValue);
}

double Config::GetDouble(std::string_view key, double defaultValue) const noexcept {
    return impl_->Get<double>(key, defaultValue);
}

std::string Config::GetString(std::string_view key, std::string_view defaultValue) const {
    return impl_->Get<std::string>(key, defaultValue);
}

bool Config::Contains(std::string_view key) const noexcept {
    std::shared_lock lock(impl_->mutex);
    return impl_->values.find(std::string(key)) != impl_->values.end();
}

bool Config::Remove(std::string_view key) noexcept {
    std::unique_lock lock(impl_->mutex);
    return impl_->values.erase(std::string(key)) > 0;
}

void Config::Clear() noexcept {
    std::unique_lock lock(impl_->mutex);
    impl_->values.clear();
}

std::size_t Config::Size() const noexcept {
    std::shared_lock lock(impl_->mutex);
    return impl_->values.size();
}

} // namespace pmsdk
