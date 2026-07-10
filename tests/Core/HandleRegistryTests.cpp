#include "Core/HandleRegistry.h"

#include <gtest/gtest.h>

#include <memory>
#include <thread>
#include <vector>

namespace {

using Registry = pmsdk::detail::HandleRegistry<int>;

TEST(HandleRegistryTest, InsertThenGet) {
    Registry registry;
    const auto handle = registry.Insert(std::make_shared<int>(42));
    ASSERT_NE(handle, Registry::kInvalidHandle);

    const auto object = registry.Get(handle);
    ASSERT_NE(object, nullptr);
    EXPECT_EQ(*object, 42);
    EXPECT_TRUE(registry.Contains(handle));
    EXPECT_EQ(registry.Size(), 1u);
}

TEST(HandleRegistryTest, NullObjectRejected) {
    Registry registry;
    EXPECT_EQ(registry.Insert(nullptr), Registry::kInvalidHandle);
    EXPECT_EQ(registry.Size(), 0u);
}

TEST(HandleRegistryTest, InvalidHandleFailsLookup) {
    Registry registry;
    EXPECT_EQ(registry.Get(Registry::kInvalidHandle), nullptr);
    EXPECT_EQ(registry.Get(0xDEADBEEFull), nullptr);
    EXPECT_FALSE(registry.Remove(Registry::kInvalidHandle));
}

TEST(HandleRegistryTest, RemoveInvalidatesHandle) {
    Registry registry;
    const auto handle = registry.Insert(std::make_shared<int>(1));

    EXPECT_TRUE(registry.Remove(handle));
    EXPECT_EQ(registry.Get(handle), nullptr);
    EXPECT_FALSE(registry.Contains(handle));
    EXPECT_FALSE(registry.Remove(handle)); // double-remove is safe and reports failure
    EXPECT_EQ(registry.Size(), 0u);
}

TEST(HandleRegistryTest, StaleHandleDoesNotAliasReusedSlot) {
    Registry registry;
    const auto first = registry.Insert(std::make_shared<int>(1));
    ASSERT_TRUE(registry.Remove(first));

    // Slot is reused, but the generation differs — the old handle must not
    // resolve to the new object.
    const auto second = registry.Insert(std::make_shared<int>(2));
    ASSERT_NE(second, first);
    EXPECT_EQ(registry.Get(first), nullptr);
    ASSERT_NE(registry.Get(second), nullptr);
    EXPECT_EQ(*registry.Get(second), 2);
}

TEST(HandleRegistryTest, OutstandingSharedPtrSurvivesRemove) {
    Registry registry;
    const auto handle = registry.Insert(std::make_shared<int>(99));
    const auto object = registry.Get(handle);

    ASSERT_TRUE(registry.Remove(handle));
    ASSERT_NE(object, nullptr); // the caller's reference keeps the object alive
    EXPECT_EQ(*object, 99);
}

TEST(HandleRegistryTest, DistinctHandlesForDistinctObjects) {
    Registry registry;
    const auto a = registry.Insert(std::make_shared<int>(1));
    const auto b = registry.Insert(std::make_shared<int>(2));
    EXPECT_NE(a, b);
    EXPECT_EQ(*registry.Get(a), 1);
    EXPECT_EQ(*registry.Get(b), 2);
    EXPECT_EQ(registry.Size(), 2u);
}

TEST(HandleRegistryTest, ConcurrentInsertGetRemove) {
    Registry registry;
    constexpr int kThreads = 8;
    constexpr int kOpsPerThread = 200;

    std::vector<std::thread> threads;
    threads.reserve(kThreads);
    for (int t = 0; t < kThreads; ++t) {
        threads.emplace_back([&registry, t] {
            for (int i = 0; i < kOpsPerThread; ++i) {
                const auto handle = registry.Insert(std::make_shared<int>(t));
                const auto object = registry.Get(handle);
                ASSERT_NE(object, nullptr);
                ASSERT_EQ(*object, t);
                ASSERT_TRUE(registry.Remove(handle));
            }
        });
    }
    for (std::thread& thread : threads) {
        thread.join();
    }

    EXPECT_EQ(registry.Size(), 0u);
}

} // namespace
