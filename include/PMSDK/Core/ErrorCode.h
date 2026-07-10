#pragma once

#include <cstdint>
#include <string_view>

namespace pmsdk {

/// Error categories returned by every fallible SDK operation.
/// Stable numeric values: these back the C API status codes, so existing
/// values must never be renumbered — only appended to.
enum class ErrorCode : std::int32_t {
    Ok = 0,
    Unknown = 1,
    InvalidArgument = 2,
    OutOfRange = 3,
    NotFound = 4,
    AlreadyExists = 5,
    InvalidHandle = 6,
    OutOfMemory = 7,
    IoError = 8,
    ParseError = 9,
    VersionMismatch = 10,
    NotImplemented = 11,
    OperationFailed = 12,
    InvalidState = 13,
};

/// Human-readable name of an error code. Never returns an empty view;
/// unrecognized values yield "UnrecognizedErrorCode".
[[nodiscard]] constexpr std::string_view ToString(ErrorCode code) noexcept {
    switch (code) {
    case ErrorCode::Ok:
        return "Ok";
    case ErrorCode::Unknown:
        return "Unknown";
    case ErrorCode::InvalidArgument:
        return "InvalidArgument";
    case ErrorCode::OutOfRange:
        return "OutOfRange";
    case ErrorCode::NotFound:
        return "NotFound";
    case ErrorCode::AlreadyExists:
        return "AlreadyExists";
    case ErrorCode::InvalidHandle:
        return "InvalidHandle";
    case ErrorCode::OutOfMemory:
        return "OutOfMemory";
    case ErrorCode::IoError:
        return "IoError";
    case ErrorCode::ParseError:
        return "ParseError";
    case ErrorCode::VersionMismatch:
        return "VersionMismatch";
    case ErrorCode::NotImplemented:
        return "NotImplemented";
    case ErrorCode::OperationFailed:
        return "OperationFailed";
    case ErrorCode::InvalidState:
        return "InvalidState";
    }
    return "UnrecognizedErrorCode";
}

} // namespace pmsdk
