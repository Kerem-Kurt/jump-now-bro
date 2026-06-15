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

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCanvas();
            BuildAnnouncement();
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
    }
}
