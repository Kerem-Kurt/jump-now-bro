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

        PlayerController currentPlayer;

        void OnEnable()
        {
            if (playerSpawner != null) playerSpawner.OnPlayerSpawned += HandlePlayerSpawned;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        void OnDisable()
        {
            if (playerSpawner != null) playerSpawner.OnPlayerSpawned -= HandlePlayerSpawned;
            if (currentPlayer != null) currentPlayer.OnDeath -= HandleDeath;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Additive) return;
            if (levelLabel != null) levelLabel.text = $"Level: {scene.name}";
        }

        void HandlePlayerSpawned(PlayerController player)
        {
            if (currentPlayer != null) currentPlayer.OnDeath -= HandleDeath;
            currentPlayer = player;
            if (currentPlayer != null) currentPlayer.OnDeath += HandleDeath;
            UpdateDeathLabel(player != null ? player.DeathCount : 0);
        }

        void HandleDeath(int deathCount) => UpdateDeathLabel(deathCount);

        void UpdateDeathLabel(int count)
        {
            if (deathLabel != null) deathLabel.text = $"Deaths: {count}";
        }
    }
}
