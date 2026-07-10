#include "PMSDK/Core/Log.h"

#include <atomic>
#include <mutex>
#include <shared_mutex>

namespace pmsdk {

struct Logger::Impl {
    std::string name;
    std::atomic<LogLevel> level{LogLevel::Info};
    // Guards the callback/userData pair so they swap atomically together.
    mutable std::shared_mutex callbackMutex;
    LogCallback callback = nullptr;
    void* userData = nullptr;
};

Logger::Logger(std::string_view name) : impl_(std::make_unique<Impl>()) {
    impl_->name = std::string(name);
}

Logger::~Logger() = default;

void Logger::SetCallback(LogCallback callback, void* userData) noexcept {
    std::unique_lock lock(impl_->callbackMutex);
    impl_->callback = callback;
    impl_->userData = userData;
}

void Logger::SetLevel(LogLevel level) noexcept {
    impl_->level.store(level, std::memory_order_relaxed);
}

LogLevel Logger::GetLevel() const noexcept {
    return impl_->level.load(std::memory_order_relaxed);
}

const std::string& Logger::GetName() const noexcept {
    return impl_->name;
}

void Logger::Log(LogLevel level, std::string_view message) noexcept {
    if (level == LogLevel::Off) {
        return; // Off is a filter setting, not a message severity.
    }
    if (static_cast<std::int32_t>(level) <
        static_cast<std::int32_t>(impl_->level.load(std::memory_order_relaxed))) {
        return;
    }

    std::shared_lock lock(impl_->callbackMutex);
    if (impl_->callback == nullptr) {
        return;
    }
    try {
        impl_->callback(level, message, impl_->userData);
    } catch (...) {
        // The host callback contract forbids throwing; swallow to keep the
        // noexcept promise instead of terminating the process.
    }
}

} // namespace pmsdk
