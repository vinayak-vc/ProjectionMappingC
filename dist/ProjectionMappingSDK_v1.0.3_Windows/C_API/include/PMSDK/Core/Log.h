#pragma once

#include <cstdint>
#include <memory>
#include <string>
#include <string_view>

#include "PMSDK/Core/Export.h"

namespace pmsdk {

/// Severity of a log message. Numeric order defines filtering: a logger set to
/// level L delivers messages with severity >= L. Values are stable for the C API.
enum class LogLevel : std::int32_t {
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warn = 3,
    Error = 4,
    Critical = 5,
    Off = 6, ///< Filter level only — messages cannot be logged at Off.
};

/// Human-readable name of a log level.
[[nodiscard]] constexpr std::string_view ToString(LogLevel level) noexcept {
    switch (level) {
    case LogLevel::Trace:
        return "Trace";
    case LogLevel::Debug:
        return "Debug";
    case LogLevel::Info:
        return "Info";
    case LogLevel::Warn:
        return "Warn";
    case LogLevel::Error:
        return "Error";
    case LogLevel::Critical:
        return "Critical";
    case LogLevel::Off:
        return "Off";
    }
    return "UnrecognizedLogLevel";
}

/// Host-provided sink for SDK log output. Invoked synchronously on the thread
/// that logged. `message` is only valid for the duration of the call — copy it
/// if you need to keep it. The callback must not throw and must not call back
/// into the Logger that invoked it.
using LogCallback = void (*)(LogLevel level, std::string_view message, void* userData);

/// Thread-safe logging facade. The SDK produces messages; the host application
/// decides where they go by registering a LogCallback. Without a callback the
/// logger is silent — the SDK never writes to stdout/stderr or files on its own.
class Logger {
public:
    /// Creates a logger. `name` identifies the source (e.g. the owning context)
    /// and is copied.
    PMSDK_API explicit Logger(std::string_view name);
    PMSDK_API ~Logger();

    Logger(const Logger&) = delete;
    Logger& operator=(const Logger&) = delete;
    Logger(Logger&&) = delete;
    Logger& operator=(Logger&&) = delete;

    /// Registers the host sink. Pass nullptr to silence the logger. `userData`
    /// is forwarded verbatim to every callback invocation. Thread-safe.
    PMSDK_API void SetCallback(LogCallback callback, void* userData = nullptr) noexcept;

    /// Sets the minimum severity that will be delivered. Thread-safe.
    PMSDK_API void SetLevel(LogLevel level) noexcept;

    /// The current minimum severity. Thread-safe.
    [[nodiscard]] PMSDK_API LogLevel GetLevel() const noexcept;

    /// The logger name given at construction.
    [[nodiscard]] PMSDK_API const std::string& GetName() const noexcept;

    /// Delivers `message` to the host callback if `level` passes the filter.
    /// Never throws; exceptions escaping the host callback are swallowed.
    PMSDK_API void Log(LogLevel level, std::string_view message) noexcept;

    /// Severity-named conveniences for Log().
    void Trace(std::string_view message) noexcept { Log(LogLevel::Trace, message); }
    void Debug(std::string_view message) noexcept { Log(LogLevel::Debug, message); }
    void Info(std::string_view message) noexcept { Log(LogLevel::Info, message); }
    void Warn(std::string_view message) noexcept { Log(LogLevel::Warn, message); }
    void Error(std::string_view message) noexcept { Log(LogLevel::Error, message); }
    void Critical(std::string_view message) noexcept { Log(LogLevel::Critical, message); }

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace pmsdk
