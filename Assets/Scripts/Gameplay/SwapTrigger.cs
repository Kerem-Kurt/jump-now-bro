using System.Collections.Generic;
using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class SwapTrigger : MonoBehaviour
    {
        static readonly List<SwapTrigger> active = new List<SwapTrigger>();
        static Sprite quadSprite;

        [SerializeField] PlayerAction actionToSwap;
        [SerializeField] bool showBanner = true;
        [SerializeField] float armedAlpha = 0.55f;
        [SerializeField] Color firedColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        [SerializeField] int bannerSortingOrder = 1;

        bool fired;
        bool firedAtCheckpoint;
        SpriteRenderer banner;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Start()
        {
            if (showBanner) AcquireBanner();
            SetBannerArmed(!fired);
        }

        // Checkpoint locks in which triggers have fired up to this point.
        public static void SaveCheckpointStates()
        {
            foreach (var t in active) t.firedAtCheckpoint = t.fired;
        }

        // Respawn: triggers crossed since the checkpoint re-arm; ones crossed before it stay consumed.
        public static void RestoreCheckpointStates()
        {
            foreach (var t in active)
            {
                t.fired = t.firedAtCheckpoint;
                t.SetBannerArmed(!t.fired);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired) return;
            // Detect Player via the "Player" tag instead of TryGetComponent<PlayerController> — client's
            // role-aware spawner destroys PlayerController, so the component check would always fail
            // on client and the visual update below would never fire.
            if (!other.CompareTag("Player")) return;

            // Visual update on BOTH host and client — banner greys regardless of role so the client's
            // HUD matches the host's. The ControlMap mutation below is the only authoritative state
            // change and stays gated to host (client mirrors via STATE.controlMap).
            fired = true;
            SetBannerArmed(false);

            if (!Authority.IsHost) return;

            var store = ControlMapStore.Instance;
            if (store == null)
            {
                Debug.LogError($"SwapTrigger on '{name}' fired but no ControlMapStore in scene.", this);
                return;
            }
            store.Apply(ControlMap.WithSwap(store.Current, actionToSwap));
        }

        void AcquireBanner()
        {
            // Prefer a sprite the level author already placed on the trigger so we
            // recolor that instead of stacking a second quad on top of it.
            banner = GetComponentInChildren<SpriteRenderer>();
            if (banner == null) BuildBanner();
        }

        void BuildBanner()
        {
            var col = GetComponent<Collider2D>();
            var go = new GameObject("SwapBanner");
            banner = go.AddComponent<SpriteRenderer>();
            banner.sprite = QuadSprite();
            banner.sortingOrder = bannerSortingOrder;

            if (col is BoxCollider2D box)
            {
                go.transform.SetParent(transform, worldPositionStays: false);
                go.transform.localPosition = box.offset;
                go.transform.localScale = new Vector3(box.size.x, box.size.y, 1f);
            }
            else
            {
                // Non-box colliders: size from world bounds, parented in world space.
                var b = col.bounds;
                go.transform.SetParent(transform, worldPositionStays: true);
                go.transform.position = new Vector3(b.center.x, b.center.y, transform.position.z);
                go.transform.localScale = new Vector3(b.size.x, b.size.y, 1f);
            }
        }

        void SetBannerArmed(bool armed)
        {
            if (banner == null) return;
            banner.color = armed ? ColorFor(actionToSwap, armedAlpha) : firedColor;
        }

        static Color ColorFor(PlayerAction action, float alpha)
        {
            Color c = action switch
            {
                PlayerAction.MoveHorizontal => new Color(0.4f, 0.9f, 0.5f),
                PlayerAction.Jump => new Color(1f, 0.7f, 0.2f),
                PlayerAction.Dash => new Color(0.4f, 0.8f, 1f),
                _ => Color.white
            };
            c.a = alpha;
            return c;
        }

        static Sprite QuadSprite()
        {
            if (quadSprite == null)
            {
                var tex = Texture2D.whiteTexture;
                quadSprite = Sprite.Create(
                    tex, new Rect(0, 0, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f), tex.width);
            }
            return quadSprite;
        }
    }
}
