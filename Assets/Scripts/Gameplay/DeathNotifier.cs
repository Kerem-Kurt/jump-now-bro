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

        /// Cumulative death count — the single source of truth for any HUD/UI that wants to display it.
        /// Updated by Raise; LevelHud reads this on spawn (when no death event has fired yet).
        public int Current { get; private set; }

        /// Fires when Current actually changes. Same-count Raise is a no-op (STATE may arrive at 30 Hz
        /// with the same deathCount on most ticks — we don't want camera-shake spam).
        public event Action<int> OnDeath;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void Raise(int newCount)
        {
            if (newCount == Current) return;                                     // dedup on equal so STATE-driven path is cheap
            Current = newCount;
            OnDeath?.Invoke(newCount);
        }
    }
}
