#include <PMSDK/Core/Status.h>

#include <gtest/gtest.h>

#include <memory>
#include <string>
#include <utility>

namespace {

TEST(StatusTest, DefaultConstructedIsOk) {
    const pmsdk::Status status;
    EXPECT_TRUE(status.ok());
    EXPECT_TRUE(static_cast<bool>(status));
    EXPECT_EQ(status.code(), pmsdk::ErrorCode::Ok);
    EXPECT_TRUE(status.message().empty());
}

TEST(StatusTest, OkFactoryEqualsDefault) {
    EXPECT_EQ(pmsdk::Status::Ok(), pmsdk::Status{});
}

TEST(StatusTest, ErrorCarriesCodeAndMessage) {
    const pmsdk::Status status{pmsdk::ErrorCode::NotFound, "mesh 'wall' does not exist"};
    EXPECT_FALSE(status.ok());
    EXPECT_FALSE(static_cast<bool>(status));
    EXPECT_EQ(status.code(), pmsdk::ErrorCode::NotFound);
    EXPECT_EQ(status.message(), "mesh 'wall' does not exist");
}

TEST(ResultTest, HoldsValueOnSuccess) {
    const pmsdk::Result<int> result = 42;
    ASSERT_TRUE(result.ok());
    EXPECT_TRUE(static_cast<bool>(result));
    EXPECT_EQ(result.value(), 42);
    EXPECT_TRUE(result.status().ok());
}

TEST(ResultTest, HoldsStatusOnFailure) {
    const pmsdk::Result<int> result = pmsdk::Status{pmsdk::ErrorCode::ParseError, "bad json"};
    ASSERT_FALSE(result.ok());
    EXPECT_FALSE(static_cast<bool>(result));
    EXPECT_EQ(result.status().code(), pmsdk::ErrorCode::ParseError);
    EXPECT_EQ(result.status().message(), "bad json");
}

TEST(ResultTest, MoveExtractsValue) {
    pmsdk::Result<std::string> result = std::string("payload");
    ASSERT_TRUE(result.ok());
    const std::string moved = std::move(result).value();
    EXPECT_EQ(moved, "payload");
}

TEST(ResultTest, WorksWithMoveOnlyTypes) {
    pmsdk::Result<std::unique_ptr<int>> result = std::make_unique<int>(7);
    ASSERT_TRUE(result.ok());
    const std::unique_ptr<int> owned = std::move(result).value();
    ASSERT_NE(owned, nullptr);
    EXPECT_EQ(*owned, 7);
}

} // namespace
