using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] GameObject playerPrefab;
        [SerializeField] CameraFollow cameraFollow;

        PlayerController currentPlayer;
        int accumulatedDeaths;

        public PlayerController CurrentPlayer => currentPlayer;
        // Player is re-instantiated per level, so its DeathCount is per-level; this
        // folds finished levels into a running total for the completion summary.
        public int TotalDeaths => accumulatedDeaths + (currentPlayer != null ? currentPlayer.DeathCount : 0);
        /// Fires with the spawned Player GameObject — subscribers TryGetComponent for PlayerController
        /// when they need it. The v1.4 client destroys PlayerController, so the previous Action<PlayerController>
        /// signature would have fired with null and NRE'd downstream subscribers.
        public event Action<GameObject> OnPlayerSpawned;

        void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath += HandleDeathFromNotifier;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath -= HandleDeathFromNotifier;
        }

        // Camera shake on every death — host's PlayerController fires it via HandlePlayerDeath -> Raise;
        // client's ClientStateRenderer fires it on STATE.deathCount delta. Same path here either way.
        void HandleDeathFromNotifier(int deathCount)
        {
            if (cameraFollow != null) cameraFollow.Shake();
        }

        void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Only spawn for additive level-scene loads; Bootstrap is the single root scene.
            if (mode != LoadSceneMode.Additive) return;

            var spawnPoint = FindSpawnPointInScene(scene);
            if (spawnPoint == null)
            {
                Debug.LogWarning($"PlayerSpawner: no PlayerSpawnPoint found in scene '{scene.name}'.");
                return;
            }

            if (currentPlayer != null)
            {
                accumulatedDeaths += currentPlayer.DeathCount;
                currentPlayer.OnDeath -= HandlePlayerDeath;
                Destroy(currentPlayer.gameObject);
            }

            var instance = Instantiate(playerPrefab, spawnPoint.transform.position, Quaternion.identity);
            currentPlayer = instance.GetComponent<PlayerController>();
            if (currentPlayer != null)
            {
                currentPlayer.SetCheckpoint(spawnPoint.transform.position, ControlMap.Default);
                currentPlayer.OnDeath += HandlePlayerDeath;
            }

            if (cameraFollow != null)
                cameraFollow.SetTarget(instance.transform);                // works regardless of role: client destroys PlayerController, but the transform stays

            OnPlayerSpawned?.Invoke(instance);
        }

        // Bridge PlayerController.OnDeath -> DeathNotifier so all subscribers (HUD, camera) see deaths
        // through a single source — same path the client takes via STATE-delta in ClientStateRenderer.
        void HandlePlayerDeath(int deathCount) =>
            DeathNotifier.Instance?.Raise(deathCount);

        PlayerSpawnPoint FindSpawnPointInScene(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var sp = root.GetComponentInChildren<PlayerSpawnPoint>(includeInactive: true);
                if (sp != null) return sp;
            }
            return null;
        }
    }
}
