#include <PMSDK/Core/Log.h>

#include <gtest/gtest.h>

#include <mutex>
#include <stdexcept>
#include <string>
#include <thread>
#include <utility>
#include <vector>

namespace {

struct Capture {
    std::mutex mutex;
    std::vector<std::pair<pmsdk::LogLevel, std::string>> entries;

    static void Callback(pmsdk::LogLevel level, std::string_view message, void* userData) {
        auto* self = static_cast<Capture*>(userData);
        const std::scoped_lock lock(self->mutex);
        self->entries.emplace_back(level, std::string(message));
    }

    std::size_t Count() {
        const std::scoped_lock lock(mutex);
        return entries.size();
    }
};

TEST(LoggerTest, SilentWithoutCallback) {
    pmsdk::Logger logger("test");
    logger.Info("dropped on the floor"); // must not crash
    SUCCEED();
}

TEST(LoggerTest, CallbackReceivesMessageAndLevel) {
    pmsdk::Logger logger("test");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);

    logger.Warn("projector 2 offline");

    ASSERT_EQ(capture.Count(), 1u);
    EXPECT_EQ(capture.entries[0].first, pmsdk::LogLevel::Warn);
    EXPECT_EQ(capture.entries[0].second, "projector 2 offline");
}

TEST(LoggerTest, LevelFilterDropsLowerSeverities) {
    pmsdk::Logger logger("test");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);
    logger.SetLevel(pmsdk::LogLevel::Warn);

    logger.Trace("t");
    logger.Debug("d");
    logger.Info("i");
    logger.Warn("w");
    logger.Error("e");
    logger.Critical("c");

    ASSERT_EQ(capture.Count(), 3u);
    EXPECT_EQ(capture.entries[0].first, pmsdk::LogLevel::Warn);
    EXPECT_EQ(capture.entries[1].first, pmsdk::LogLevel::Error);
    EXPECT_EQ(capture.entries[2].first, pmsdk::LogLevel::Critical);
}

TEST(LoggerTest, OffLevelSilencesEverything) {
    pmsdk::Logger logger("test");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);
    logger.SetLevel(pmsdk::LogLevel::Off);

    logger.Critical("even critical is dropped");

    EXPECT_EQ(capture.Count(), 0u);
}

TEST(LoggerTest, OffIsNotAValidMessageSeverity) {
    pmsdk::Logger logger("test");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);
    logger.SetLevel(pmsdk::LogLevel::Trace);

    logger.Log(pmsdk::LogLevel::Off, "must be ignored");

    EXPECT_EQ(capture.Count(), 0u);
}

TEST(LoggerTest, NullCallbackSilencesLogger) {
    pmsdk::Logger logger("test");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);
    logger.Info("first");
    logger.SetCallback(nullptr);
    logger.Info("second");

    ASSERT_EQ(capture.Count(), 1u);
    EXPECT_EQ(capture.entries[0].second, "first");
}

TEST(LoggerTest, ThrowingCallbackIsContained) {
    pmsdk::Logger logger("test");
    logger.SetCallback(
        [](pmsdk::LogLevel, std::string_view, void*) { throw std::runtime_error("host bug"); });

    logger.Info("must not terminate"); // Log is noexcept; the throw is swallowed
    SUCCEED();
}

TEST(LoggerTest, GetNameAndLevelRoundTrip) {
    pmsdk::Logger logger("stage-a");
    EXPECT_EQ(logger.GetName(), "stage-a");
    logger.SetLevel(pmsdk::LogLevel::Debug);
    EXPECT_EQ(logger.GetLevel(), pmsdk::LogLevel::Debug);
}

TEST(LoggerTest, ConcurrentLoggingDoesNotLoseMessages) {
    pmsdk::Logger logger("mt");
    Capture capture;
    logger.SetCallback(&Capture::Callback, &capture);

    constexpr int kThreads = 8;
    constexpr int kMessagesPerThread = 250;
    std::vector<std::thread> threads;
    threads.reserve(kThreads);
    for (int t = 0; t < kThreads; ++t) {
        threads.emplace_back([&logger] {
            for (int i = 0; i < kMessagesPerThread; ++i) {
                logger.Info("m");
            }
        });
    }
    for (std::thread& thread : threads) {
        thread.join();
    }

    EXPECT_EQ(capture.Count(), static_cast<std::size_t>(kThreads * kMessagesPerThread));
}

} // namespace
