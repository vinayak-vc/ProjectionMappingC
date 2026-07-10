#include <PMSDK/Core/Version.h>

#include <gtest/gtest.h>

#include <string>

namespace {

TEST(VersionTest, RuntimeVersionMatchesHeaderVersion) {
    const pmsdk::Version runtime = pmsdk::GetVersion();
    EXPECT_EQ(runtime, pmsdk::kHeaderVersion);
}

TEST(VersionTest, VersionStringMatchesComponents) {
    const pmsdk::Version v = pmsdk::GetVersion();
    const std::string expected = std::to_string(v.major) + "." + std::to_string(v.minor) + "." +
                                 std::to_string(v.patch);
    EXPECT_EQ(std::string{pmsdk::GetVersionString()}, expected);
}

TEST(VersionTest, VersionIsNonZero) {
    const pmsdk::Version v = pmsdk::GetVersion();
    EXPECT_TRUE(v.major > 0 || v.minor > 0 || v.patch > 0);
}

} // namespace
