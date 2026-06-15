using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// #124 ghost intent cue: when the player who does NOT own an action signals it, spawn a faint, drifting clone
    /// of the character so the partner reads the intent without anyone talking. Move (left/right) -> a sideways
    /// drift ghost; jump -> an upward hop ghost. Render-only (clones the Visual sprite, no collider/rigidbody),
    /// never touches the sim. Reads each player's intent via GhostIntentSources (local keyboard or remote frame,
    /// resolved per role); "one ghost per tap" = a rising edge on the HELD bit (robust locally and over the wire).
    public sealed class GhostIntentCue : MonoBehaviour
    {
        [SerializeField] float driftDistance = 0.85f;
        [SerializeField] float hopHeight = 0.85f;
        [SerializeField] float lifetime = 0.5f;
        [SerializeField] float ghostAlpha = 0.3f;

        Transform visual;
        Vector3 visualBaseScale = Vector3.one;

        // previous-frame held bits per owner (index = InputOwner) for rising-edge ("tap") detection
        readonly bool[] prevLeft = new bool[2];
        readonly bool[] prevRight = new bool[2];
        readonly bool[] prevJump = new bool[2];

        void Awake()
        {
            visual = transform.Find("Visual");
            if (visual != null) visualBaseScale = visual.localScale;
        }

        void Update()
        {
            if (visual == null || ControlMapStore.Instance == null) return;
            var map = ControlMapStore.Instance.Current;
            int moveSig = (int)Flip(map.moveOwner);     // the player who does NOT own move
            int jumpSig = (int)Flip(map.jumpOwner);     // the player who does NOT own jump

            // Read both owners' intent each frame so the prev-bits stay current even across a swap.
            for (int owner = 0; owner < 2; owner++)
            {
                if (!GhostIntentSources.TryGet((InputOwner)owner, out var s))
                {
                    prevLeft[owner] = prevRight[owner] = prevJump[owner] = false;
                    continue;
                }
                if (owner == moveSig)
                {
                    if (s.left && !prevLeft[owner]) Spawn(new Vector2(-driftDistance, 0f), (InputOwner)owner);
                    if (s.right && !prevRight[owner]) Spawn(new Vector2(driftDistance, 0f), (InputOwner)owner);
                }
                if (owner == jumpSig && s.jumpHeld && !prevJump[owner])
                    Spawn(new Vector2(0f, hopHeight), (InputOwner)owner);

                prevLeft[owner] = s.left;
                prevRight[owner] = s.right;
                prevJump[owner] = s.jumpHeld;
            }
        }

        void Spawn(Vector2 drift, InputOwner owner)
        {
            var clone = Instantiate(visual.gameObject);
            clone.name = "IntentGhost";
            clone.transform.SetParent(null);                 // world-space; outlives the player so a level load can't freeze it
            clone.transform.position = visual.position;
            clone.transform.rotation = visual.rotation;
            clone.transform.localScale = visualBaseScale;    // reset so it never inherits a mid-squash scale
            var sr = clone.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                var c = PlayerIdentity.ColorOf(owner); c.a = ghostAlpha; sr.color = c;
                sr.sortingOrder -= 1;                        // behind the real character
            }
            clone.AddComponent<GhostFade>().Begin(visual, drift, lifetime, ghostAlpha);   // follow the character + drift
        }

        static InputOwner Flip(InputOwner o) => o == InputOwner.P1 ? InputOwner.P2 : InputOwner.P1;
    }
}
