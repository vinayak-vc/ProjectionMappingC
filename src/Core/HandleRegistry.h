#pragma once

#include <cstdint>
#include <memory>
#include <shared_mutex>
#include <vector>

// Internal, not exported from the SDK binary. Backs the opaque-handle C API
// (Milestone 10): every PM_* handle maps to an object owned by a registry.
namespace pmsdk::detail {

/// Thread-safe map from 64-bit opaque handles to shared objects.
///
/// Handle layout: high 32 bits = slot generation, low 32 bits = slot index.
/// A slot's generation increments on every Remove, so a stale handle (one
/// whose object was removed, even if the slot was reused) fails lookup instead
/// of silently aliasing a newer object. 0 is never a valid handle.
template <typename T>
class HandleRegistry {
public:
    using Handle = std::uint64_t;
    static constexpr Handle kInvalidHandle = 0;

    /// Stores `object` and returns its handle. Returns kInvalidHandle for null.
    [[nodiscard]] Handle Insert(std::shared_ptr<T> object) {
        if (object == nullptr) {
            return kInvalidHandle;
        }
        std::unique_lock lock(mutex_);
        std::uint32_t index = 0;
        if (!freeSlots_.empty()) {
            index = freeSlots_.back();
            freeSlots_.pop_back();
        } else {
            slots_.emplace_back();
            index = static_cast<std::uint32_t>(slots_.size() - 1);
        }
        slots_[index].object = std::move(object);
        ++count_;
        return MakeHandle(slots_[index].generation, index);
    }

    /// The object for `handle`, or nullptr when the handle is invalid or stale.
    /// The returned shared_ptr keeps the object alive even if it is removed
    /// concurrently.
    [[nodiscard]] std::shared_ptr<T> Get(Handle handle) const noexcept {
        std::shared_lock lock(mutex_);
        const Slot* slot = Resolve(handle);
        return slot != nullptr ? slot->object : nullptr;
    }

    /// True when `handle` currently resolves to a live object.
    [[nodiscard]] bool Contains(Handle handle) const noexcept {
        std::shared_lock lock(mutex_);
        return Resolve(handle) != nullptr;
    }

    /// Removes the object for `handle`. Returns false when the handle is
    /// invalid or stale. Outstanding shared_ptrs from Get() stay valid.
    bool Remove(Handle handle) noexcept {
        std::unique_lock lock(mutex_);
        const Slot* slot = Resolve(handle);
        if (slot == nullptr) {
            return false;
        }
        const std::uint32_t index = IndexOf(handle);
        slots_[index].object.reset();
        // Invalidate every outstanding handle to this slot; skip generation 0
        // so a valid handle can never equal kInvalidHandle.
        if (++slots_[index].generation == 0) {
            slots_[index].generation = 1;
        }
        freeSlots_.push_back(index);
        --count_;
        return true;
    }

    /// Number of live objects.
    [[nodiscard]] std::size_t Size() const noexcept {
        std::shared_lock lock(mutex_);
        return count_;
    }

private:
    struct Slot {
        std::shared_ptr<T> object;
        std::uint32_t generation = 1;
    };

    static constexpr std::uint32_t IndexOf(Handle handle) noexcept {
        return static_cast<std::uint32_t>(handle & 0xFFFFFFFFull);
    }

    static constexpr std::uint32_t GenerationOf(Handle handle) noexcept {
        return static_cast<std::uint32_t>(handle >> 32);
    }

    static constexpr Handle MakeHandle(std::uint32_t generation, std::uint32_t index) noexcept {
        return (static_cast<Handle>(generation) << 32) | static_cast<Handle>(index);
    }

    /// Returns the slot for `handle` iff it is live and generation-matched.
    /// Caller must hold the mutex.
    [[nodiscard]] const Slot* Resolve(Handle handle) const noexcept {
        const std::uint32_t index = IndexOf(handle);
        if (index >= slots_.size()) {
            return nullptr;
        }
        const Slot& slot = slots_[index];
        if (slot.generation != GenerationOf(handle) || slot.object == nullptr) {
            return nullptr;
        }
        return &slot;
    }

    mutable std::shared_mutex mutex_;
    std::vector<Slot> slots_;
    std::vector<std::uint32_t> freeSlots_;
    std::size_t count_ = 0;
};

} // namespace pmsdk::detail
