namespace JumpNowBro.Util
{
    /// The recorded, single-source v1.5 client-prediction constants (#83). These were locked on a clean LAN
    /// (127.0.0.1 + ParrelSync); adverse-network re-verification/re-tuning is tracked separately for the
    /// hardening milestone (#108). VisualSmoothing and ClientPrediction reference these so the values live in
    /// exactly one place instead of as scattered magic numbers.
    ///
    /// Rationale for the locked values:
    /// - SmoothingDecayPerTick 0.30 → render offset is ~99% gone in ~13 ticks (~140 ms at 60 Hz): fast enough to
    ///   feel responsive, slow enough that a correction reads as a glide rather than a snap.
    /// - SnapThreshold 1.5 u → about ¾ of the body height. Below it a correction is plausibly "drift worth
    ///   easing"; above it the prediction was wrong enough that easing would look like the character sliding, so
    ///   we cut instantly. (Respawn/level teleports always cut regardless, via Clear().)
    /// - ReplayCap 64 ticks (~1.07 s) → longer than any expected LAN RTT + brief stall, but bounded so a
    ///   post-stall catch-up storm hard-snaps instead of replaying hundreds of ticks (and never reads past the
    ///   128-tick ClientHistory ring).
    ///
    /// NOTE: same-tick prediction (host stays the only tick authority) means there is deliberately NO inputLead
    /// knob — the client predicts at its current tick and reconciles, rather than running ahead of the host.
    public static class PredictionTuning
    {
        public const float SmoothingDecayPerTick = 0.30f;   // ~140 ms blend at 60 Hz
        public const float SnapThreshold         = 1.5f;    // correction magnitude beyond which we cut, not ease
        public const int   ReplayCap             = 64;      // max ticks reconciliation will replay before hard-snapping
    }
}
