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

        // DeathNotifier.Current is the single source of truth — on Host it's set by PlayerSpawner.Raise(TotalDeaths),
        // on Client it's set by ClientStateRenderer.Raise(STATE.deathCount). Both carry cumulative count.
        // Spawn-time read picks up whatever's already been pushed (e.g., client mid-game join with prior deaths).
        void HandlePlayerSpawned(GameObject player) =>
            UpdateDeathLabel(DeathNotifier.Instance != null ? DeathNotifier.Instance.Current : 0);

        void HandleDeath(int newCount) => UpdateDeathLabel(newCount);

        void UpdateDeathLabel(int count)
        {
            if (deathLabel != null) deathLabel.text = $"Deaths: {count}";
        }

        /// Blank the labels on session teardown (Leave). The level scene stays loaded until the next session's
        /// load (unloading here raced the next load — see LevelManager.ResetIndex), so clear the text in place.
        public void Clear()
        {
            if (levelLabel != null) levelLabel.text = "";
            if (deathLabel != null) deathLabel.text = "";
        }
    }
}
