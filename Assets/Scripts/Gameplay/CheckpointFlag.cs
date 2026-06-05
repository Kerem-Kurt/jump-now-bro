using System.Collections;
using UnityEngine;

namespace JumpNowBro.Gameplay
{
    /// Cosmetic, client-visible checkpoint flag. The authoritative Checkpoint (host-only OnTriggerEnter2D) owns the
    /// respawn; this is a SEPARATE, non-authority-gated component so the flag drops on BOTH ends — it keys on the
    /// "Player" tag (the client's rendered player keeps it) and runs its own coroutine, never touching gameplay.
    ///
    /// The checkpoint volume has no renderer, so this builds the flag sprite itself at Start. It raycasts straight
    /// down to the ground so the flag lands ON it regardless of where the checkpoint sits — no per-scene tuning. On
    /// first touch it falls from `raiseHeight` above the ground to the ground; a `played` one-shot guard keeps it
    /// down through walk-backs and death-respawns (it must not re-drop).
    [RequireComponent(typeof(Collider2D))]
    public sealed class CheckpointFlag : MonoBehaviour
    {
        [SerializeField] Sprite flagSprite;
        [SerializeField] LayerMask groundLayers;        // Ground|OneWayPlatform; falls back to those if left unset
        [SerializeField] float raiseHeight = 2f;        // how far above the ground the flag starts before dropping
        [SerializeField] float maxDrop = 12f;
        [SerializeField] float dropSeconds = 0.45f;
        [SerializeField] int sortingOrder = 2;

        Transform flag;
        bool played;
        float droppedLocalY, raisedLocalY;

        void Start()
        {
            if (flagSprite == null) return;
            int mask = groundLayers.value != 0 ? groundLayers.value : (1 << 7) | (1 << 8);   // Ground|OneWayPlatform
            var hit = Physics2D.Raycast(transform.position, Vector2.down, maxDrop, mask);
            float groundY = hit.collider != null ? hit.point.y : transform.position.y - 1f;
            droppedLocalY = groundY - transform.position.y;     // bottom-pivot flag base rests on the ground
            raisedLocalY = droppedLocalY + raiseHeight;

            var go = new GameObject("Flag");
            flag = go.transform;
            flag.SetParent(transform, worldPositionStays: false);
            flag.localPosition = new Vector3(0f, raisedLocalY, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = flagSprite;
            sr.sortingOrder = sortingOrder;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (played || flag == null) return;
            if (!other.CompareTag("Player")) return;        // tag-based → fires on host AND client; not Authority-gated
            played = true;
            StartCoroutine(Drop());
        }

        IEnumerator Drop()
        {
            float t = 0f;
            while (t < dropSeconds)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dropSeconds);
                float eased = 1f - (1f - k) * (1f - k);     // ease-out so it reads as a fall, not a slide
                flag.localPosition = new Vector3(0f, Mathf.LerpUnclamped(raisedLocalY, droppedLocalY, eased), 0f);
                yield return null;
            }
            flag.localPosition = new Vector3(0f, droppedLocalY, 0f);
        }
    }
}
