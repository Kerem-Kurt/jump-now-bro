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

        void HandlePlayerSpawned(PlayerController player)
        {
            UpdateDeathLabel(player != null ? player.DeathCount : 0);
        }

        void HandleDeath(int deathCount) => UpdateDeathLabel(deathCount);

        void UpdateDeathLabel(int count)
        {
            if (deathLabel != null) deathLabel.text = $"Deaths: {count}";
        }
    }
}
