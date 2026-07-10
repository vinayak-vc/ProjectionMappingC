#include <PMSDK/Core/ErrorCode.h>

#include <gtest/gtest.h>

namespace {

// ToString is constexpr — exercise it at compile time.
static_assert(pmsdk::ToString(pmsdk::ErrorCode::Ok) == "Ok");
static_assert(pmsdk::ToString(pmsdk::ErrorCode::InvalidHandle) == "InvalidHandle");

TEST(ErrorCodeTest, EveryCodeHasNonEmptyName) {
    using pmsdk::ErrorCode;
    constexpr ErrorCode codes[] = {
        ErrorCode::Ok,           ErrorCode::Unknown,        ErrorCode::InvalidArgument,
        ErrorCode::OutOfRange,   ErrorCode::NotFound,       ErrorCode::AlreadyExists,
        ErrorCode::InvalidHandle, ErrorCode::OutOfMemory,   ErrorCode::IoError,
        ErrorCode::ParseError,   ErrorCode::VersionMismatch, ErrorCode::NotImplemented,
        ErrorCode::OperationFailed, ErrorCode::InvalidState,
    };
    for (const ErrorCode code : codes) {
        EXPECT_FALSE(pmsdk::ToString(code).empty());
        EXPECT_NE(pmsdk::ToString(code), "UnrecognizedErrorCode");
    }
}

TEST(ErrorCodeTest, UnrecognizedValueYieldsFallbackName) {
    const auto bogus = static_cast<pmsdk::ErrorCode>(9999);
    EXPECT_EQ(pmsdk::ToString(bogus), "UnrecognizedErrorCode");
}

TEST(ErrorCodeTest, NumericValuesAreStable) {
    // These values back the C API — a failure here means an ABI break.
    EXPECT_EQ(static_cast<std::int32_t>(pmsdk::ErrorCode::Ok), 0);
    EXPECT_EQ(static_cast<std::int32_t>(pmsdk::ErrorCode::InvalidArgument), 2);
    EXPECT_EQ(static_cast<std::int32_t>(pmsdk::ErrorCode::InvalidHandle), 6);
    EXPECT_EQ(static_cast<std::int32_t>(pmsdk::ErrorCode::InvalidState), 13);
}

} // namespace
