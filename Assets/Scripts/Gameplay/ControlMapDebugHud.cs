using UnityEngine;
using UnityEngine.UI;
using TMPro;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Top-bar control indicator: who currently drives MOVE / JUMP / DASH. Colour-coded per action (matching the
    /// portals) with the owner in white, styled in code so it reads as a clean HUD rather than the raw debug text.
    public class ControlMapDebugHud : MonoBehaviour
    {
        [SerializeField] TMP_Text moveLabel;
        [SerializeField] TMP_Text jumpLabel;
        [SerializeField] TMP_Text dashLabel;

        ControlMap currentMap = ControlMap.Default;   // cached so a PlayerIdentity change can re-render without a swap

        void Start()
        {
            BuildBackground();
            Place(moveLabel, -190f);
            Place(jumpLabel, 0f);
            Place(dashLabel, 190f);
            PlayerIdentity.OnChanged += Refresh;                              // names/colours may arrive after this HUD starts
            if (ControlMapStore.Instance != null)
            {
                ControlMapStore.Instance.OnChanged += UpdateLabels;
                UpdateLabels(ControlMapStore.Instance.Current);
            }
            else Refresh();
        }

        void OnDestroy()
        {
            PlayerIdentity.OnChanged -= Refresh;
            if (ControlMapStore.Instance != null)
                ControlMapStore.Instance.OnChanged -= UpdateLabels;
        }

        void Refresh() => UpdateLabels(currentMap);

        static void Place(TMP_Text t, float x)
        {
            if (t == null) return;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);   // top-centre band
            rt.anchoredPosition = new Vector2(x, -14f);
            rt.sizeDelta = new Vector2(180f, 36f);
            t.enableAutoSizing = true;            // a long display name shrinks instead of overflowing the strip
            t.fontSizeMin = 14;
            t.fontSizeMax = 26;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
            if (TMP_Settings.defaultFontAsset != null) t.font = TMP_Settings.defaultFontAsset;   // follow the TMP default (Inter) despite the serialized font
        }

        // A dark semi-transparent strip behind the three labels so they stay readable over the level.
        void BuildBackground()
        {
            if (moveLabel == null) return;
            var go = new GameObject("ControlsBg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(moveLabel.transform.parent, false);
            go.transform.SetAsFirstSibling();                                 // render behind the labels
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.5f);
            img.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -8f);
            rt.sizeDelta = new Vector2(600f, 44f);
        }

        void UpdateLabels(ControlMap map)
        {
            // action name in its fixed colour (the "what", via ActionStyle); owner name in that player's colour (the "who")
            currentMap = map;
            if (moveLabel != null) moveLabel.text = $"{ActionStyle.RichLabel(PlayerAction.MoveHorizontal)}  {Owner(map.moveOwner)}";
            if (jumpLabel != null) jumpLabel.text = $"{ActionStyle.RichLabel(PlayerAction.Jump)}  {Owner(map.jumpOwner)}";
            if (dashLabel != null) dashLabel.text = $"{ActionStyle.RichLabel(PlayerAction.Dash)}  {Owner(map.dashOwner)}";
        }

        static string Owner(InputOwner o)
            => $"<color=#{ColorUtility.ToHtmlStringRGB(PlayerIdentity.ColorOf(o))}>{PlayerIdentity.NameOf(o)}</color>";
    }
}
