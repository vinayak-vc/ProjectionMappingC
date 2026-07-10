#include <PMSDK/Core/Context.h>

#include <gtest/gtest.h>

#include <string>
#include <vector>

namespace {

struct Capture {
    std::vector<std::string> messages;

    static void Callback(pmsdk::LogLevel, std::string_view message, void* userData) {
        static_cast<Capture*>(userData)->messages.emplace_back(message);
    }
};

TEST(ContextTest, DefaultConstruction) {
    pmsdk::Context context;
    EXPECT_EQ(context.GetName(), "pmsdk");
    EXPECT_EQ(context.GetLogger().GetLevel(), pmsdk::LogLevel::Info);
    EXPECT_EQ(context.GetConfig().Size(), 0u);
}

TEST(ContextTest, DescWiresNameLevelAndCallback) {
    Capture capture;
    pmsdk::ContextDesc desc;
    desc.name = "show-controller";
    desc.logLevel = pmsdk::LogLevel::Debug;
    desc.logCallback = &Capture::Callback;
    desc.logUserData = &capture;

    pmsdk::Context context{desc};

    EXPECT_EQ(context.GetName(), "show-controller");
    EXPECT_EQ(context.GetLogger().GetLevel(), pmsdk::LogLevel::Debug);
    // The construction message itself must have reached the host sink.
    ASSERT_FALSE(capture.messages.empty());
    EXPECT_EQ(capture.messages.front(), "PMSDK context created");
}

TEST(ContextTest, ContextsAreIsolated) {
    pmsdk::Context a;
    pmsdk::Context b;

    a.GetConfig().SetInt("projectors", 3);
    a.GetLogger().SetLevel(pmsdk::LogLevel::Error);

    EXPECT_FALSE(b.GetConfig().Contains("projectors"));
    EXPECT_EQ(b.GetLogger().GetLevel(), pmsdk::LogLevel::Info);
}

TEST(ContextTest, ConfigIsWritableThroughContext) {
    pmsdk::Context context;
    context.GetConfig().SetString("output", "dome");
    EXPECT_EQ(context.GetConfig().GetString("output", ""), "dome");
}

} // namespace
