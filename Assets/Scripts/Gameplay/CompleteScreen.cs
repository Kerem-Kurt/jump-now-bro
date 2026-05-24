using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JumpNowBro.Gameplay
{
    public class CompleteScreen : MonoBehaviour
    {
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text summaryLabel;
        [SerializeField] PlayerSpawner playerSpawner;
        [SerializeField] Color overlayColor = new Color(0f, 0f, 0f, 0.9f);

        void Start()
        {
            ConfigurePanel();
            if (summaryLabel != null)
            {
                summaryLabel.textWrappingMode = TextWrappingModes.NoWrap;
                summaryLabel.alignment = TextAlignmentOptions.Center;
            }
            // Subscribe in Start so LevelManager.Awake has already run; the completion
            // event is many levels away, so there's no race with early loads.
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnAllLevelsComplete += HandleComplete;
        }

        void OnDestroy()
        {
            if (LevelManager.Instance != null)
                LevelManager.Instance.OnAllLevelsComplete -= HandleComplete;
        }

        // Force a full-screen, near-opaque overlay regardless of how the panel was
        // authored, then start hidden.
        void ConfigurePanel()
        {
            if (panel == null) return;
            if (panel.transform is RectTransform rt)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }
            if (panel.TryGetComponent<Image>(out var img)) img.color = overlayColor;
            panel.SetActive(false);
        }

        void HandleComplete()
        {
            int deaths = playerSpawner != null ? playerSpawner.TotalDeaths : 0;
            if (summaryLabel != null)
                summaryLabel.text = $"Complete!\nDeaths: {deaths}";
            if (panel != null) panel.SetActive(true);
        }
    }
}
