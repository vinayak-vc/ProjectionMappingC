#pragma once

#include <memory>
#include <string_view>

#include "PMSDK/Core/Config.h"
#include "PMSDK/Core/Export.h"
#include "PMSDK/Core/Log.h"

namespace pmsdk {

/// Construction parameters for a Context. All fields are copied; the desc may
/// be a temporary.
struct ContextDesc {
    /// Identifies this context in log output.
    std::string_view name = "pmsdk";
    /// Initial minimum log severity.
    LogLevel logLevel = LogLevel::Info;
    /// Optional host log sink, registered before any SDK message is emitted.
    LogCallback logCallback = nullptr;
    /// Forwarded verbatim to `logCallback`.
    void* logUserData = nullptr;
};

/// Root lifecycle object of the SDK. Owns the logger and configuration; every
/// higher-level SDK object is created from (and scoped to) a Context. The SDK
/// keeps no global state — hosts may create any number of independent contexts,
/// each fully isolated. A Context must outlive every object created from it.
class Context {
public:
    /// Creates a context. May allocate; on allocation failure the constructor
    /// throws std::bad_alloc (the only place the C++ convenience layer throws —
    /// the C API wraps this into a status code).
    PMSDK_API explicit Context(const ContextDesc& desc = {});
    PMSDK_API ~Context();

    Context(const Context&) = delete;
    Context& operator=(const Context&) = delete;
    Context(Context&&) = delete;
    Context& operator=(Context&&) = delete;

    /// The context's logger. Valid for the lifetime of the context.
    [[nodiscard]] PMSDK_API Logger& GetLogger() noexcept;
    [[nodiscard]] PMSDK_API const Logger& GetLogger() const noexcept;

    /// The context's configuration store. Valid for the lifetime of the context.
    [[nodiscard]] PMSDK_API Config& GetConfig() noexcept;
    [[nodiscard]] PMSDK_API const Config& GetConfig() const noexcept;

    /// The context name given at construction.
    [[nodiscard]] PMSDK_API const std::string& GetName() const noexcept;

private:
    struct Impl;
    std::unique_ptr<Impl> impl_;
};

} // namespace pmsdk
