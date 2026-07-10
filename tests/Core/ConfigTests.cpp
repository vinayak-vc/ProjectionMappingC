#include <PMSDK/Core/Config.h>

#include <gtest/gtest.h>

namespace {

TEST(ConfigTest, StartsEmpty) {
    const pmsdk::Config config;
    EXPECT_EQ(config.Size(), 0u);
    EXPECT_FALSE(config.Contains("anything"));
}

TEST(ConfigTest, RoundTripsEveryType) {
    pmsdk::Config config;
    config.SetBool("flag", true);
    config.SetInt("count", 42);
    config.SetDouble("gamma", 2.2);
    config.SetString("name", "projector-1");

    EXPECT_EQ(config.GetBool("flag", false), true);
    EXPECT_EQ(config.GetInt("count", 0), 42);
    EXPECT_DOUBLE_EQ(config.GetDouble("gamma", 0.0), 2.2);
    EXPECT_EQ(config.GetString("name", ""), "projector-1");
    EXPECT_EQ(config.Size(), 4u);
}

TEST(ConfigTest, MissingKeyReturnsDefault) {
    const pmsdk::Config config;
    EXPECT_EQ(config.GetBool("missing", true), true);
    EXPECT_EQ(config.GetInt("missing", -5), -5);
    EXPECT_DOUBLE_EQ(config.GetDouble("missing", 1.5), 1.5);
    EXPECT_EQ(config.GetString("missing", "fallback"), "fallback");
}

TEST(ConfigTest, TypeMismatchReturnsDefault) {
    pmsdk::Config config;
    config.SetInt("value", 7);
    EXPECT_EQ(config.GetBool("value", false), false);
    EXPECT_DOUBLE_EQ(config.GetDouble("value", 3.5), 3.5);
    EXPECT_EQ(config.GetString("value", "d"), "d");
    // The stored type is still intact.
    EXPECT_EQ(config.GetInt("value", 0), 7);
}

TEST(ConfigTest, SetOverwritesIncludingType) {
    pmsdk::Config config;
    config.SetInt("key", 1);
    config.SetString("key", "now a string");
    EXPECT_EQ(config.Size(), 1u);
    EXPECT_EQ(config.GetInt("key", -1), -1);
    EXPECT_EQ(config.GetString("key", ""), "now a string");
}

TEST(ConfigTest, RemoveAndClear) {
    pmsdk::Config config;
    config.SetBool("a", true);
    config.SetBool("b", false);

    EXPECT_TRUE(config.Remove("a"));
    EXPECT_FALSE(config.Remove("a"));
    EXPECT_FALSE(config.Contains("a"));
    EXPECT_EQ(config.Size(), 1u);

    config.Clear();
    EXPECT_EQ(config.Size(), 0u);
    EXPECT_FALSE(config.Contains("b"));
}

} // namespace
