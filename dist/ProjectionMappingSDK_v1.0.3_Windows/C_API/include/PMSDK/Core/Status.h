#pragma once

#include <string>
#include <type_traits>
#include <utility>
#include <variant>

#include "PMSDK/Core/ErrorCode.h"

namespace pmsdk {

/// Outcome of a fallible operation: an ErrorCode plus an optional detail message.
/// Header-only value type — safe to use in public headers; no exceptions cross
/// the SDK boundary, errors are always reported through Status/Result.
class Status {
public:
    /// Constructs a success status.
    Status() noexcept = default;

    /// Constructs a status with an error code and optional human-readable detail.
    explicit Status(ErrorCode code, std::string message = {})
        : code_(code), message_(std::move(message)) {}

    /// Success singleton-style factory, for readability at call sites.
    [[nodiscard]] static Status Ok() noexcept { return Status{}; }

    /// True when the operation succeeded.
    [[nodiscard]] bool ok() const noexcept { return code_ == ErrorCode::Ok; }

    /// The error category. ErrorCode::Ok on success.
    [[nodiscard]] ErrorCode code() const noexcept { return code_; }

    /// Detail message; may be empty. Always empty for success statuses.
    [[nodiscard]] const std::string& message() const noexcept { return message_; }

    /// Allows `if (status)` style checks.
    [[nodiscard]] explicit operator bool() const noexcept { return ok(); }

    [[nodiscard]] friend bool operator==(const Status& lhs, const Status& rhs) noexcept {
        return lhs.code_ == rhs.code_ && lhs.message_ == rhs.message_;
    }

private:
    ErrorCode code_ = ErrorCode::Ok;
    std::string message_;
};

/// Result of a fallible operation that produces a value: holds either a T or an
/// error Status. Header-only; the SDK never throws across its boundary.
///
/// Preconditions: value() may only be called when ok() is true, and a Result
/// must never be constructed from a success Status (an Ok status carries no value).
template <typename T>
class Result {
    static_assert(!std::is_same_v<std::decay_t<T>, Status>, "Result<Status> is not meaningful");

public:
    /// Constructs a successful result. Intentionally implicit so functions can
    /// `return value;`.
    Result(T value) : storage_(std::move(value)) {}

    /// Constructs a failed result. Intentionally implicit so functions can
    /// `return Status{...};`. The status must not be Ok.
    Result(Status status) : storage_(std::move(status)) {}

    /// True when a value is present.
    [[nodiscard]] bool ok() const noexcept { return std::holds_alternative<T>(storage_); }

    /// The contained value. Precondition: ok().
    [[nodiscard]] const T& value() const& { return std::get<T>(storage_); }

    /// Moves the contained value out. Precondition: ok().
    [[nodiscard]] T&& value() && { return std::get<T>(std::move(storage_)); }

    /// The error status, or a success Status when ok().
    [[nodiscard]] Status status() const {
        if (ok()) {
            return Status::Ok();
        }
        return std::get<Status>(storage_);
    }

    /// Allows `if (result)` style checks.
    [[nodiscard]] explicit operator bool() const noexcept { return ok(); }

private:
    std::variant<T, Status> storage_;
};

} // namespace pmsdk
