/**
 * @file ViewerSelector.h
 * @brief Active-viewer selection with a stable id and switch hysteresis (spec §3).
 *
 * Chooses one active viewer among the frame's detections by nearest depth or largest box, keeps
 * a persistent identity across frames by positional association, and resists flip-flopping: a
 * challenger must beat the incumbent's score by a margin for a sustained number of frames before
 * the identity switches. While the incumbent is momentarily unmeasured, selection reports "no
 * measurement" (index -1) rather than hopping to another person — the state machine then
 * predicts through the gap and only a full Reset() (after Lost) frees the slot for a new viewer.
 *
 * Header-only, allocation-free; operates over a caller-owned detection span.
 */
#pragma once

#include <cstddef>

#include "PMSDK/Math/Vector3.h"
#include "HoloTrack/Tracking/Detection.h"
#include "HoloTrack/Tracking/Config.h"

namespace holotrack {

using pmsdk::Math::Vector3;

/** @brief Outcome of a selection pass. */
struct SelectionResult {
    int index{-1}; /**< Index into the detection span, or -1 for "no active measurement". */
    int id{-1};    /**< Current viewer identity (-1 when none held). */
};

/** @brief Stateful active-viewer selector. */
class ViewerSelector {
public:
    /**
     * @brief Select the active viewer for this frame.
     * @param dets Detection span (may be empty).
     * @param count Number of detections.
     * @param cfg Configuration (selection mode, depth gate, hysteresis, association distance).
     */
    SelectionResult Select(const Detection* dets, std::size_t count, const TrackerConfig& cfg) {
        // Best-scoring depth-gated candidate overall.
        int bestIdx = -1;
        float bestScore = -1.0f;
        for (std::size_t i = 0; i < count; ++i) {
            if (!PassesDepthGate(dets[i], cfg)) {
                continue;
            }
            const float s = Score(dets[i], cfg.selection);
            if (s > bestScore) {
                bestScore = s;
                bestIdx = static_cast<int>(i);
            }
        }

        if (bestIdx < 0) {
            // No usable detection this frame — keep the incumbent id for prediction/association.
            return {-1, hasIncumbent_ ? incumbentId_ : -1};
        }

        if (!hasIncumbent_) {
            AdoptNew(dets[static_cast<std::size_t>(bestIdx)].spatial);
            return {bestIdx, incumbentId_};
        }

        // Associate the incumbent to the nearest candidate within the gate.
        int incIdx = -1;
        float incDist = cfg.associationMaxDistance;
        for (std::size_t i = 0; i < count; ++i) {
            if (!PassesDepthGate(dets[i], cfg)) {
                continue;
            }
            const float dist = (dets[i].spatial - incumbentPos_).Length();
            if (dist <= incDist) {
                incDist = dist;
                incIdx = static_cast<int>(i);
            }
        }

        if (incIdx < 0) {
            // Incumbent not visible this frame — do not hop; report unmeasured.
            return {-1, incumbentId_};
        }

        const float incScore = Score(dets[static_cast<std::size_t>(incIdx)], cfg.selection);
        const bool challengerBeats =
            (bestIdx != incIdx) && (bestScore > incScore * (1.0f + cfg.hysteresisMargin));

        if (challengerBeats) {
            const Vector3 challengerPos = dets[static_cast<std::size_t>(bestIdx)].spatial;
            if (hasChallenger_ && (challengerPos - challengerPos_).Length() <= cfg.associationMaxDistance) {
                ++challengerFrames_;
            } else {
                hasChallenger_ = true;
                challengerFrames_ = 1;
            }
            challengerPos_ = challengerPos;

            if (challengerFrames_ >= cfg.hysteresisFrames) {
                AdoptNew(challengerPos);
                return {bestIdx, incumbentId_};
            }
            // Hysteresis not yet satisfied — hold the incumbent.
            incumbentPos_ = dets[static_cast<std::size_t>(incIdx)].spatial;
            return {incIdx, incumbentId_};
        }

        // No sustained challenger — hold the incumbent and clear any pending challenge.
        hasChallenger_ = false;
        challengerFrames_ = 0;
        incumbentPos_ = dets[static_cast<std::size_t>(incIdx)].spatial;
        return {incIdx, incumbentId_};
    }

    /** @brief Drop the current viewer so the next frame adopts a fresh identity. */
    void Reset() {
        hasIncumbent_ = false;
        hasChallenger_ = false;
        challengerFrames_ = 0;
    }

    /** @brief Current viewer identity, or -1 when none is held. */
    int CurrentId() const { return hasIncumbent_ ? incumbentId_ : -1; }

private:
    static bool PassesDepthGate(const Detection& d, const TrackerConfig& cfg) {
        return d.spatial.z >= cfg.minDepth && d.spatial.z <= cfg.maxDepth;
    }

    static float Score(const Detection& d, SelectionMode mode) {
        if (mode == SelectionMode::LargestBox) {
            return d.BoxArea();
        }
        // NearestZ: closer is better; positive so the hysteresis margin behaves.
        const float z = d.spatial.z > 1e-3f ? d.spatial.z : 1e-3f;
        return 1.0f / z;
    }

    void AdoptNew(const Vector3& pos) {
        incumbentId_ = nextId_++;
        incumbentPos_ = pos;
        hasIncumbent_ = true;
        hasChallenger_ = false;
        challengerFrames_ = 0;
    }

    bool hasIncumbent_{false};
    Vector3 incumbentPos_{};
    int incumbentId_{-1};
    int nextId_{1};

    bool hasChallenger_{false};
    Vector3 challengerPos_{};
    int challengerFrames_{0};
};

} // namespace holotrack
