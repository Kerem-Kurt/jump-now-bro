using UnityEngine;

namespace JumpNowBro.Gameplay
{
    /// Cosmetic spinning glow behind a swap portal. Builds a swirl sprite child at Start (auto-sized to `size`,
    /// drawn behind the tinted ring) and rotates it every frame, giving the portal a living vortex. Pure local
    /// Update — identical on host and client, and never touches the SwapTrigger state machine (the ring itself
    /// stays on the trigger root, where SetBannerArmed tints it; the collider must not rotate, so the motion lives
    /// on this separate child).
    public sealed class PortalSpin : MonoBehaviour
    {
        [SerializeField] Sprite swirlSprite;
        [SerializeField] float degreesPerSecond = 35f;
        [SerializeField] float size = 1.7f;                 // world size, slightly larger than the ring so it haloes it
        [SerializeField] int sortingOrder = -1;             // behind the ring
        [SerializeField] Color tint = new Color(1f, 1f, 1f, 0.4f);

        Transform swirl;

        void Start()
        {
            if (swirlSprite == null) return;
            var go = new GameObject("Swirl");
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = swirlSprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;
            swirl = go.transform;
            swirl.SetParent(transform, worldPositionStays: false);
            swirl.localPosition = Vector3.zero;
            float native = sr.sprite.bounds.size.x;         // scale to `size` regardless of the sprite's native units
            swirl.localScale = native > 0f ? Vector3.one * (size / native) : Vector3.one;
        }

        void Update()
        {
            if (swirl != null) swirl.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
        }
    }
}
