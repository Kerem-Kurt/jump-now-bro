using UnityEngine;

namespace JumpNowBro.Gameplay
{
    /// Self-contained drift + fade + self-destroy for a spawned intent ghost (#124). Tracks the live character so
    /// its motion is the character's velocity PLUS the intent drift, otherwise a moving character outruns the ghost
    /// and it reads on the wrong side. Falls back to its last-known anchor if the character is destroyed (level
    /// load), so it never freezes or NPEs. Render-only: the clone is a bare SpriteRenderer.
    public sealed class GhostFade : MonoBehaviour
    {
        SpriteRenderer sr;
        Transform follow;
        Vector3 anchor;
        Vector2 drift;
        float life, age, startAlpha;

        public void Begin(Transform follow, Vector2 drift, float lifetime, float alpha)
        {
            sr = GetComponent<SpriteRenderer>();
            this.follow = follow;
            anchor = follow != null ? follow.position : transform.position;
            this.drift = drift;
            life = Mathf.Max(lifetime, 0.0001f);
            startAlpha = alpha;
        }

        void Update()
        {
            age += Time.deltaTime;
            float k = Mathf.Clamp01(age / life);
            float ease = 1f - (1f - k) * (1f - k);            // ease-out: shoots out, then slows
            if (follow != null) anchor = follow.position;     // move WITH the character; drift is added on top
            transform.position = anchor + (Vector3)(drift * ease);
            if (sr != null)
            {
                var c = sr.color;
                c.a = startAlpha * (1f - k);                  // fade out over life
                sr.color = c;
            }
            if (k >= 1f) Destroy(gameObject);
        }
    }
}
