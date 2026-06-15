using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Persistent screen-space overlay for the v2.1 swap-coordination cues: the swap announcement (#115),
    /// the screen-edge flash (#126), and the prep-telegraph label (#127). Self-spawns before the first scene
    /// (like SwapScheduleDriver / AudioManager) and builds its own ScreenSpaceOverlay canvas at sortingOrder 50
    /// (above the gameplay HUD at 0, below the main menu at 100), so it survives level loads with no scene wiring.
    public sealed class GameHudOverlay : MonoBehaviour
    {
        public static GameHudOverlay Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoSpawn()
        {
            if (Instance == null)
                new GameObject(nameof(GameHudOverlay)).AddComponent<GameHudOverlay>();
        }

        // #115 announcement
        TMP_Text announceLabel;
        CanvasGroup announceGroup;
        Coroutine announceRoutine;

        // #126 edge flash
        CanvasGroup flashGroup;
        Image flashLeftCore, flashRightCore;
        Coroutine flashRoutine;
        static Sprite edgeGradientL, edgeGradientR;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
            BuildAnnouncement();
            BuildEdgeFlash();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void BuildCanvas()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            // No GraphicRaycaster: the overlay is purely presentational and must never eat clicks.
        }

        void BuildAnnouncement()
        {
            var go = new GameObject("SwapAnnounce", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            announceLabel = go.AddComponent<TextMeshProUGUI>();
            announceLabel.alignment = TextAlignmentOptions.Center;
            announceLabel.enableAutoSizing = false;
            announceLabel.fontSize = 46;
            announceLabel.fontStyle = FontStyles.Bold;
            announceLabel.color = Color.white;
            announceLabel.raycastTarget = false;
            if (TMP_Settings.defaultFontAsset != null) announceLabel.font = TMP_Settings.defaultFontAsset;

            var rt = announceLabel.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, 110f);            // slightly above centre (#115)
            rt.sizeDelta = new Vector2(1000f, 90f);

            ApplySoftShadow(announceLabel);
            announceGroup = go.AddComponent<CanvasGroup>();
            announceGroup.alpha = 0f;
        }

        /// "JUMP -> Name": action word in its action colour, the name in that player's colour, arrow white.
        /// Identical text on both ends (the SwapScheduleDriver apply loop runs the same map on host + client
        /// at the shared tick).
        public void Announce(PlayerAction action, InputOwner newOwner)
        {
            if (announceLabel == null) return;
            string nameHex = ColorUtility.ToHtmlStringRGB(PlayerIdentity.ColorOf(newOwner));
            announceLabel.text = $"{ActionStyle.RichLabel(action)} → <color=#{nameHex}>{PlayerIdentity.NameOf(newOwner)}</color>";
            if (announceRoutine != null) StopCoroutine(announceRoutine);
            announceRoutine = StartCoroutine(AnnounceRoutine());     // newest-replaces
        }

        IEnumerator AnnounceRoutine()
        {
            const float inT = 0.12f, hold = 0.8f, outT = 0.35f;
            var rt = announceLabel.rectTransform;

            // Snappy in: alpha 0->1 (ease-out), scale 0.85->1.0 with a slight overshoot (ease-out-back).
            for (float t = 0f; t < inT; t += Time.unscaledDeltaTime)
            {
                float k = t / inT;
                announceGroup.alpha = 1f - (1f - k) * (1f - k);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(k));
                yield return null;
            }
            announceGroup.alpha = 1f;
            rt.localScale = Vector3.one;

            for (float t = 0f; t < hold; t += Time.unscaledDeltaTime) yield return null;

            // Soft out: alpha 1->0.
            for (float t = 0f; t < outT; t += Time.unscaledDeltaTime)
            {
                float k = t / outT;
                announceGroup.alpha = 1f - k * k;       // ease-in fade
                yield return null;
            }
            announceGroup.alpha = 0f;
            announceRoutine = null;
        }

        static float EaseOutBack(float k)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = k - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        static void ApplySoftShadow(TMP_Text t)
        {
            // Per-label material instance (fontMaterial) with a soft underlay = drop shadow, no backplate.
            var mat = t.fontMaterial;
            mat.EnableKeyword("UNDERLAY_ON");
            mat.SetColor("_UnderlayColor", new Color(0f, 0f, 0f, 0.75f));
            mat.SetFloat("_UnderlayOffsetX", 0.5f);
            mat.SetFloat("_UnderlayOffsetY", -0.5f);
            mat.SetFloat("_UnderlayDilate", 0.1f);
            mat.SetFloat("_UnderlaySoftness", 0.25f);
        }

        // ---- #126 edge flash --------------------------------------------------------------------------

        void BuildEdgeFlash()
        {
            var root = new GameObject("EdgeFlash", typeof(RectTransform));
            root.transform.SetParent(transform, false);
            var rrt = root.GetComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = rrt.offsetMax = Vector2.zero;
            flashGroup = root.AddComponent<CanvasGroup>();
            flashGroup.alpha = 0f;
            flashGroup.blocksRaycasts = false;
            flashGroup.interactable = false;

            // Left+right only (#126), in the action's colour. The flash only ever shows on the screen of the
            // player the change affects, so the action colour alone identifies it; no player-colour rim needed.
            flashLeftCore  = MakeEdgeStrip(root.transform, leftSide: true,  width: 200f);
            flashRightCore = MakeEdgeStrip(root.transform, leftSide: false, width: 200f);
        }

        // A side-anchored full-height strip using the edge-gradient sprite (opaque at the screen edge, fading inward).
        static Image MakeEdgeStrip(Transform parent, bool leftSide, float width)
        {
            var go = new GameObject(leftSide ? "EdgeStripL" : "EdgeStripR", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = EdgeGradientSprite(opaqueAtLeft: leftSide);   // opaque side hugs this screen edge
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(leftSide ? 0f : 1f, 0f);
            rt.anchorMax = new Vector2(leftSide ? 0f : 1f, 1f);
            rt.pivot = new Vector2(leftSide ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, 0f);                 // full height (top/bottom anchored), fixed width
            return img;
        }

        // White horizontal alpha ramp: opaque at one edge, transparent inward, eased for a soft glow. Cached per side
        // (the right side needs a genuinely mirrored sprite, not a transform flip, or its rect lands off-screen).
        static Sprite EdgeGradientSprite(bool opaqueAtLeft)
        {
            if (opaqueAtLeft && edgeGradientL != null) return edgeGradientL;
            if (!opaqueAtLeft && edgeGradientR != null) return edgeGradientR;

            const int w = 64, h = 4;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int x = 0; x < w; x++)
            {
                float t = (float)x / (w - 1);
                float a = opaqueAtLeft ? 1f - t : t;
                a *= a;                                              // ease the falloff
                for (int y = 0; y < h; y++) px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
            if (opaqueAtLeft) edgeGradientL = sprite; else edgeGradientR = sprite;
            return sprite;
        }

        /// Single bright pulse on the side edges in the action's colour. Only fired when THIS screen's player
        /// gains the action (#126); losing an action shows nothing.
        public void Flash(PlayerAction action)
        {
            if (flashGroup == null) return;
            flashLeftCore.color = flashRightCore.color = ActionStyle.ColorOf(action);
            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashRoutine());
        }

        IEnumerator FlashRoutine()
        {
            const float peak = 0.85f, inT = 0.12f, outT = 0.30f;
            for (float t = 0f; t < inT; t += Time.unscaledDeltaTime)
            {
                float k = t / inT;
                flashGroup.alpha = Mathf.Lerp(0f, peak, 1f - (1f - k) * (1f - k));   // ease-out in
                yield return null;
            }
            flashGroup.alpha = peak;
            for (float t = 0f; t < outT; t += Time.unscaledDeltaTime)
            {
                float k = t / outT;
                flashGroup.alpha = Mathf.Lerp(peak, 0f, k);
                yield return null;
            }
            flashGroup.alpha = 0f;
            flashRoutine = null;
        }
    }
}
