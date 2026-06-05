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

        void Start()
        {
            BuildBackground();
            Place(moveLabel, -190f);
            Place(jumpLabel, 0f);
            Place(dashLabel, 190f);
            if (ControlMapStore.Instance == null) return;
            ControlMapStore.Instance.OnChanged += UpdateLabels;
            UpdateLabels(ControlMapStore.Instance.Current);
        }

        void OnDestroy()
        {
            if (ControlMapStore.Instance != null)
                ControlMapStore.Instance.OnChanged -= UpdateLabels;
        }

        static void Place(TMP_Text t, float x)
        {
            if (t == null) return;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 1f);   // top-centre band
            rt.anchoredPosition = new Vector2(x, -14f);
            rt.sizeDelta = new Vector2(180f, 36f);
            t.enableAutoSizing = false;
            t.fontSize = 26;
            t.fontStyle = FontStyles.Bold;
            t.color = Color.white;
            t.alignment = TextAlignmentOptions.Center;
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
            // action name in its colour, owner in white
            if (moveLabel != null) moveLabel.text = $"<color=#73E68C>MOVE</color>  {map.moveOwner}";
            if (jumpLabel != null) jumpLabel.text = $"<color=#FFB840>JUMP</color>  {map.jumpOwner}";
            if (dashLabel != null) dashLabel.text = $"<color=#73CCFF>DASH</color>  {map.dashOwner}";
        }
    }
}
