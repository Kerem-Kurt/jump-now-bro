using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JumpNowBro.Gameplay
{
    public class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] GameObject playerPrefab;
        [SerializeField] CameraFollow cameraFollow;

        PlayerController currentPlayer;

        public PlayerController CurrentPlayer => currentPlayer;
        public event Action<PlayerController> OnPlayerSpawned;

        void OnEnable() => SceneManager.sceneLoaded += HandleSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= HandleSceneLoaded;

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

            if (currentPlayer != null) Destroy(currentPlayer.gameObject);

            var instance = Instantiate(playerPrefab, spawnPoint.transform.position, Quaternion.identity);
            currentPlayer = instance.GetComponent<PlayerController>();
            if (currentPlayer != null)
                currentPlayer.SetCheckpoint(spawnPoint.transform.position);

            if (cameraFollow != null && currentPlayer != null)
                cameraFollow.SetTarget(currentPlayer.transform);

            OnPlayerSpawned?.Invoke(currentPlayer);
        }

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
