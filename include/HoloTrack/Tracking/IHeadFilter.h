/**
 * @file IHeadFilter.h
 * @brief Abstract smoothing filter for the head position (spec §5).
 */
#pragma once

#include "PMSDK/Math/Vector3.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/**
 * @brief Interface for a head-position smoothing filter.
 *
 * Implementations are constant-time, allocation-free, and stateful. Selection is runtime
 * (see @ref FilterType and @ref MakeFilter); the tracker owns one behind this interface so the
 * filter choice never leaks into the pipeline above it.
 */
class IHeadFilter {
public:
    virtual ~IHeadFilter() = default;

    /**
     * @brief Feed one measurement and return the smoothed estimate.
     * @param measurement Raw head position for this frame (camera space, metres).
     * @param dt Seconds since the previous update; implementations guard dt <= 0.
     * @return Smoothed head position.
     */
    virtual Vector3 Update(const Vector3& measurement, float dt) = 0;

    /** @brief Discard all state so the next @ref Update re-initialises from its measurement. */
    virtual void Reset() = 0;
};

} // namespace holotrack
