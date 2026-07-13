#include "Core/LastError.h"

#include <gtest/gtest.h>

extern "C" {
    void __std_rotate() {}
}

#include <thread>

namespace {

using pmsdk::ErrorCode;
using pmsdk::Status;
namespace detail = pmsdk::detail;

class LastErrorTest : public ::testing::Test {
protected:
    void SetUp() override { detail::ClearLastStatus(); }
    void TearDown() override { detail::ClearLastStatus(); }
};

TEST_F(LastErrorTest, DefaultsToOk) {
    EXPECT_TRUE(detail::LastStatus().ok());
}

TEST_F(LastErrorTest, SetThenGet) {
    detail::SetLastStatus(Status{ErrorCode::IoError, "disk full"});
    const Status& status = detail::LastStatus();
    EXPECT_EQ(status.code(), ErrorCode::IoError);
    EXPECT_EQ(status.message(), "disk full");
}

TEST_F(LastErrorTest, ClearResetsToOk) {
    detail::SetLastStatus(Status{ErrorCode::Unknown});
    detail::ClearLastStatus();
    EXPECT_TRUE(detail::LastStatus().ok());
}

TEST_F(LastErrorTest, IsolatedPerThread) {
    detail::SetLastStatus(Status{ErrorCode::InvalidArgument, "main thread error"});

    Status observedOnWorker;
    std::thread worker([&observedOnWorker] {
        // A fresh thread must start with an Ok status...
        observedOnWorker = detail::LastStatus();
        // ...and its own errors must not leak back to the main thread.
        detail::SetLastStatus(Status{ErrorCode::OutOfMemory, "worker error"});
    });
    worker.join();

    EXPECT_TRUE(observedOnWorker.ok());
    EXPECT_EQ(detail::LastStatus().code(), ErrorCode::InvalidArgument);
    EXPECT_EQ(detail::LastStatus().message(), "main thread error");
}

} // namespace
