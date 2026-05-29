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
            // DeathNotifier.Current is the single source of truth — host writes cumulative TotalDeaths,
            // client mirrors via STATE.deathCount. PlayerSpawner.TotalDeaths reads 0 on the client (where
            // PlayerController is destroyed by the role-aware spawner), so we'd have shown the wrong total.
            int deaths = DeathNotifier.Instance != null ? DeathNotifier.Instance.Current
                       : (playerSpawner != null ? playerSpawner.TotalDeaths : 0);
            if (summaryLabel != null)
                summaryLabel.text = $"Complete!\nDeaths: {deaths}";
            if (panel != null) panel.SetActive(true);
        }
    }
}
