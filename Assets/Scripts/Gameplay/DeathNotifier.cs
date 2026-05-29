using System;
using UnityEngine;

namespace JumpNowBro.Gameplay
{
    /// Manager-singleton that abstracts "the player died" so HUD + camera shake have a single source
    /// regardless of whether deaths come from PlayerController.OnDeath (Host/SinglePlayer) or from
    /// STATE deltas (Client). Wiring:
    ///   - Host / SinglePlayer: PlayerSpawner (#78) subscribes PlayerController.OnDeath → Raise(count).
    ///   - Client: ClientStateRenderer (#77) detects STATE.deathCount increment → Raise(newCount).
    /// LevelHud and CameraFollow subscribe to OnDeath at Awake; same code path on every role.
    [DefaultExecutionOrder(-100)]
    public class DeathNotifier : MonoBehaviour
    {
        public static DeathNotifier Instance { get; private set; }

        /// Fires with the NEW total death count post-increment.
        public event Action<int> OnDeath;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Raise(int newCount) => OnDeath?.Invoke(newCount);
    }
}
