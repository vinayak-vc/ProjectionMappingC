/**
 * @file Filters.h
 * @brief Head-position smoothing filters and a factory (spec §5).
 *
 * Header-only, allocation-free value logic (no HOLOTRACK_API needed — used inside the DLL and
 * compiled directly into the test binary). Each filter smooths the three axes independently.
 */
#pragma once

#include <cmath>
#include <memory>

#include "HoloTrack/Tracking/IHeadFilter.h"
#include "HoloTrack/Tracking/Config.h"

namespace holotrack {

/** @brief Pass-through "filter" — returns the measurement unchanged (@ref FilterType::None). */
class PassThroughFilter final : public IHeadFilter {
public:
    Vector3 Update(const Vector3& measurement, float /*dt*/) override { return measurement; }
    void Reset() override {}
};

/**
 * @brief One-pole exponential moving average (@ref FilterType::Exponential).
 *
 * `y = alpha * x + (1 - alpha) * yPrev`. Larger @p alpha = more responsive, less smooth.
 */
class ExponentialFilter final : public IHeadFilter {
public:
    explicit ExponentialFilter(float alpha) : alpha_(Clamp01(alpha)) {}

    Vector3 Update(const Vector3& measurement, float /*dt*/) override {
        if (!initialised_) {
            value_ = measurement;
            initialised_ = true;
            return value_;
        }
        value_ = measurement * alpha_ + value_ * (1.0f - alpha_);
        return value_;
    }

    void Reset() override { initialised_ = false; }

private:
    static float Clamp01(float v) { return v < 0.0f ? 0.0f : (v > 1.0f ? 1.0f : v); }

    float alpha_{0.5f};
    bool initialised_{false};
    Vector3 value_{};
};

/**
 * @brief Adaptive 1€ filter (@ref FilterType::OneEuro), Casiez et al. 2012.
 *
 * Low-passes each axis with a cutoff that rises with the signal's speed: near-still input is
 * heavily smoothed (kills jitter) while fast motion passes with little lag. Best default for
 * head tracking.
 */
class OneEuroFilter final : public IHeadFilter {
public:
    OneEuroFilter(float minCutoff, float beta, float dCutoff)
        : minCutoff_(minCutoff), beta_(beta), dCutoff_(dCutoff) {}

    Vector3 Update(const Vector3& measurement, float dt) override {
        if (dt <= 0.0f) {
            // No time elapsed — return the last output (or the measurement if uninitialised).
            return initialised_ ? xHat_ : measurement;
        }
        Vector3 result;
        result.x = FilterAxis(0, measurement.x, dt);
        result.y = FilterAxis(1, measurement.y, dt);
        result.z = FilterAxis(2, measurement.z, dt);
        initialised_ = true;
        xHat_ = result;
        return result;
    }

    void Reset() override { initialised_ = false; }

private:
    static float Alpha(float cutoff, float dt) {
        const float tau = 1.0f / (2.0f * 3.14159265358979323846f * cutoff);
        return 1.0f / (1.0f + tau / dt);
    }

    float FilterAxis(int i, float x, float dt) {
        if (!initialised_) {
            xPrev_[i] = x;
            dxPrev_[i] = 0.0f;
            return x;
        }
        const float dx = (x - xPrev_[i]) / dt;
        const float edx = dxPrev_[i] + Alpha(dCutoff_, dt) * (dx - dxPrev_[i]);
        const float cutoff = minCutoff_ + beta_ * std::fabs(edx);
        const float a = Alpha(cutoff, dt);
        const float y = xPrevHat_[i] + a * (x - xPrevHat_[i]);
        xPrev_[i] = x;
        xPrevHat_[i] = y;
        dxPrev_[i] = edx;
        return y;
    }

    float minCutoff_{1.0f};
    float beta_{0.007f};
    float dCutoff_{1.0f};
    bool initialised_{false};
    Vector3 xHat_{};
    float xPrev_[3]{0.0f, 0.0f, 0.0f};
    float xPrevHat_[3]{0.0f, 0.0f, 0.0f};
    float dxPrev_[3]{0.0f, 0.0f, 0.0f};
};

/**
 * @brief Per-axis constant-velocity Kalman filter (@ref FilterType::Kalman).
 *
 * State per axis is [position, velocity]. Predicts with a constant-velocity model and corrects
 * with the position measurement. Smoother than the 1€ filter under steady noise; slightly more
 * lag on sharp direction changes.
 */
class KalmanFilter final : public IHeadFilter {
public:
    KalmanFilter(float processVar, float measVar) : q_(processVar), r_(measVar) {}

    Vector3 Update(const Vector3& measurement, float dt) override {
        if (!initialised_) {
            const float mv[3] = {measurement.x, measurement.y, measurement.z};
            for (int i = 0; i < 3; ++i) {
                p_[i] = mv[i];
                v_[i] = 0.0f;
                p00_[i] = 1.0f; p01_[i] = 0.0f; p10_[i] = 0.0f; p11_[i] = 1.0f;
            }
            initialised_ = true;
            return measurement;
        }
        const float d = dt > 0.0f ? dt : 1e-3f;
        Vector3 out;
        out.x = FilterAxis(0, measurement.x, d);
        out.y = FilterAxis(1, measurement.y, d);
        out.z = FilterAxis(2, measurement.z, d);
        return out;
    }

    void Reset() override { initialised_ = false; }

private:
    float FilterAxis(int i, float z, float dt) {
        // --- Predict: x = F x, P = F P F^T + Q, with F = [[1,dt],[0,1]]. ---
        p_[i] += v_[i] * dt;
        const float p00 = p00_[i] + dt * (p10_[i] + p01_[i]) + dt * dt * p11_[i];
        const float p01 = p01_[i] + dt * p11_[i];
        const float p10 = p10_[i] + dt * p11_[i];
        const float p11 = p11_[i];
        const float qPos = q_ * dt;
        p00_[i] = p00 + qPos;
        p01_[i] = p01;
        p10_[i] = p10;
        p11_[i] = p11 + q_;
        // --- Update with measurement z of position (H = [1,0]). ---
        const float s = p00_[i] + r_;
        const float k0 = p00_[i] / s;
        const float k1 = p10_[i] / s;
        const float y = z - p_[i];
        p_[i] += k0 * y;
        v_[i] += k1 * y;
        const float np00 = (1.0f - k0) * p00_[i];
        const float np01 = (1.0f - k0) * p01_[i];
        const float np10 = p10_[i] - k1 * p00_[i];
        const float np11 = p11_[i] - k1 * p01_[i];
        p00_[i] = np00; p01_[i] = np01; p10_[i] = np10; p11_[i] = np11;
        return p_[i];
    }

    float q_{1e-2f};
    float r_{1e-1f};
    bool initialised_{false};
    float p_[3]{0.0f, 0.0f, 0.0f};   // position estimate per axis
    float v_[3]{0.0f, 0.0f, 0.0f};   // velocity estimate per axis
    float p00_[3]{}, p01_[3]{}, p10_[3]{}, p11_[3]{}; // covariance per axis
};

/**
 * @brief Construct the filter selected by @p cfg.
 * @return Owning pointer to the filter; never null (unknown types fall back to pass-through).
 */
inline std::unique_ptr<IHeadFilter> MakeFilter(const TrackerConfig& cfg) {
    switch (cfg.filterType) {
        case FilterType::Exponential:
            return std::make_unique<ExponentialFilter>(cfg.expAlpha);
        case FilterType::OneEuro:
            return std::make_unique<OneEuroFilter>(cfg.oneEuroMinCutoff, cfg.oneEuroBeta, cfg.oneEuroDCutoff);
        case FilterType::Kalman:
            return std::make_unique<KalmanFilter>(cfg.kalmanProcessVar, cfg.kalmanMeasVar);
        case FilterType::None:
        default:
            return std::make_unique<PassThroughFilter>();
    }
}

} // namespace holotrack
