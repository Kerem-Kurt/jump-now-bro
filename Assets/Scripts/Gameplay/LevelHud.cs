using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace JumpNowBro.Gameplay
{
    public class LevelHud : MonoBehaviour
    {
        [SerializeField] TMP_Text levelLabel;
        [SerializeField] TMP_Text deathLabel;
        [SerializeField] PlayerSpawner playerSpawner;

        void OnEnable()
        {
            if (playerSpawner != null) playerSpawner.OnPlayerSpawned += HandlePlayerSpawned;
            // Single death source on every role — Host/SinglePlayer feed it via PlayerController.OnDeath;
            // Client feeds it via STATE.deathCount delta. HUD doesn't need to know which.
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath += HandleDeath;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            if (playerSpawner != null) playerSpawner.OnPlayerSpawned -= HandlePlayerSpawned;
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath -= HandleDeath;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            if (levelLabel != null) levelLabel.text = $"Level: {scene.name}";
        }

        // PlayerController is re-instantiated per level, so its DeathCount resets to 0 across loads —
        // the HUD wants the cumulative total to match the end-of-game summary. PlayerSpawner.TotalDeaths
        // folds finished levels into the running count; sample it on both spawn and death-delta.
        void HandlePlayerSpawned(PlayerController player) => UpdateDeathLabel();
        void HandleDeath(int _) => UpdateDeathLabel();

        void UpdateDeathLabel()
        {
            int total = playerSpawner != null ? playerSpawner.TotalDeaths : 0;
            if (deathLabel != null) deathLabel.text = $"Deaths: {total}";
        }
    }
}
