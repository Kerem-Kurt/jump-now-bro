using System;

namespace JumpNowBro.Util
{
    /// Engine-free visual error smoothing (#82). The simulated/collider position is always authoritative-truth;
    /// only the RENDER is offset by (offsetX, offsetY) and that offset decays to zero, so a reconciliation
    /// correction eases out over ~140 ms instead of snapping.
    ///
    /// The caller injects only the *discontinuity* a reconcile introduces — `Inject(forward − reconciled)` —
    /// never normal input-driven motion (which produces a zero correction). Net effect: render = sim + offset
    /// stays continuous across the correction, then offset trends to 0. Corrections beyond SnapThreshold zero the
    /// offset instead (an instant cut is intended for big jumps: respawn, level load, post-stall snap).
    public struct VisualSmoothing
    {
        public float offsetX, offsetY;

        public const float DefaultDecayPerTick = 0.30f;   // ~140 ms blend at 60 Hz
        public const float DefaultSnapThreshold = 1.5f;   // beyond this, cut instantly
        const float ZeroEpsilon = 0.0005f;                // clamp to 0 so the offset never lingers / oscillates

        /// Add a reconcile correction (forwardPredicted − reconciled). If the resulting offset magnitude exceeds
        /// `snapThreshold`, zero it (hard cut) instead of carrying a large visible lag.
        public void Inject(float dx, float dy, float snapThreshold = DefaultSnapThreshold)
        {
            offsetX += dx;
            offsetY += dy;
            if (offsetX * offsetX + offsetY * offsetY > snapThreshold * snapThreshold)
            {
                offsetX = 0f;
                offsetY = 0f;
            }
        }

        /// Decay the offset toward zero by `decayPerTick` (fraction), clamping to exactly zero below epsilon so it
        /// settles cleanly. Call once per predicted tick.
        public void Decay(float decayPerTick = DefaultDecayPerTick)
        {
            float keep = 1f - decayPerTick;
            offsetX *= keep;
            offsetY *= keep;
            if (offsetX * offsetX + offsetY * offsetY < ZeroEpsilon * ZeroEpsilon)
            {
                offsetX = 0f;
                offsetY = 0f;
            }
        }

        /// Instant cut — respawn / level teleport / hard-snap reconcile. Render jumps straight to sim truth.
        public void Clear()
        {
            offsetX = 0f;
            offsetY = 0f;
        }

        public float Magnitude => MathF.Sqrt(offsetX * offsetX + offsetY * offsetY);
    }
}
