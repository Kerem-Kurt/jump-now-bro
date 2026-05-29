using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Physics2D-backed ICollisionWorld. `Rigidbody2D.Cast` casts the body's own collider at the rb's
    /// position — self-hit-free (sidesteps queriesStartInColliders) and tracks any future collider
    /// resize automatically. ContactFilter2D{useTriggers=false} excludes Hazard/Checkpoint/Swap/Goal
    /// per-call, no global flag fight. Local kSkin absorbs Physics2D.defaultContactOffset (0.01 in
    /// this project) without mutating the global setting.
    ///
    /// v1.3 invariant: the marshal in PlayerController.FixedUpdate has set rb.position == (x, y) before
    /// every SweepX/Y call — so the sweep origin from rb matches the (x, y) the caller passed. Grounded
    /// uses the (x, y) directly with the GroundCheck child's local offset (matches v0.4-mvp's
    /// OverlapCircle behavior exactly). v1.5 prediction-replay may need a different impl if the
    /// invariant can't be maintained on the client.
    internal sealed class UnityCollisionWorld : ICollisionWorld
    {
        readonly Rigidbody2D rb;
        readonly Transform groundCheckPoint;
        readonly LayerMask solidLayers;
        readonly float groundCheckRadius;
        readonly RaycastHit2D[] hitBuf = new RaycastHit2D[1];
        readonly ContactFilter2D filter;
        const float kSkin = 0.01f;

        public UnityCollisionWorld(Rigidbody2D rb, LayerMask solidLayers,
                                   Transform groundCheckPoint, float groundCheckRadius)
        {
            this.rb = rb;
            this.solidLayers = solidLayers;
            this.groundCheckPoint = groundCheckPoint;
            this.groundCheckRadius = groundCheckRadius;
            filter = new ContactFilter2D
            {
                useTriggers = false,
                useLayerMask = true,
                layerMask = solidLayers,
            };
        }

        public bool Grounded(float x, float y)
        {
            var off = groundCheckPoint.localPosition;
            var p = new Vector2(x + off.x, y + off.y);
            return Physics2D.OverlapCircle(p, groundCheckRadius, solidLayers) != null;
        }

        public void SweepX(float x, float y, float dx, out float resolvedDx, out bool blocked)
        {
            if (dx == 0f) { resolvedDx = 0f; blocked = false; return; }
            var dir = dx > 0f ? Vector2.right : Vector2.left;
            int hits = rb.Cast(dir, filter, hitBuf, Mathf.Abs(dx));
            if (hits > 0)
            {
                resolvedDx = dir.x * Mathf.Max(0f, hitBuf[0].distance - kSkin);
                blocked = true;
            }
            else { resolvedDx = dx; blocked = false; }
        }

        public void SweepY(float x, float y, float dy, out float resolvedDy, out bool blocked)
        {
            if (dy == 0f) { resolvedDy = 0f; blocked = false; return; }
            var dir = dy > 0f ? Vector2.up : Vector2.down;
            int hits = rb.Cast(dir, filter, hitBuf, Mathf.Abs(dy));
            if (hits > 0)
            {
                resolvedDy = dir.y * Mathf.Max(0f, hitBuf[0].distance - kSkin);
                blocked = true;
            }
            else { resolvedDy = dy; blocked = false; }
        }
    }
}
