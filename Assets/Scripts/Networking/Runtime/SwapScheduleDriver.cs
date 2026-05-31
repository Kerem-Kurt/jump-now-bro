using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Drives the PendingSwapScheduler each FixedUpdate so a control swap flips ControlMapStore on the same
    /// client-input-tick on both ends. apply_at_tick is a client input-tick: the host reaches it via its
    /// LastConsumedClientTick, the client (and solo) via TickClock. See DESIGN §8 + the v1.6 plan.
    ///
    /// Self-spawns before the first scene loads, so no Bootstrap wiring is needed; persists across levels.
    /// Execution order −45: after NetworkRemoteInputSource (−50) so the host's consumed-tick is fresh this
    /// FixedUpdate, before PlayerController (0) and ClientPredictor (−40) so the flipped map is the one they
    /// route input through this tick.
    [DefaultExecutionOrder(-45)]
    public sealed class SwapScheduleDriver : MonoBehaviour
    {
        public static SwapScheduleDriver Instance { get; private set; }

        // Telegraph + network slack before a swap applies, in client ticks. Floored for a readable telegraph,
        // raised toward the RTT when hosting, capped. v1.7 re-tunes under lag-sim.
        const int BaseLeadTicks = 9;     // ~0.15 s at 60 Hz
        const int LeadFloor     = 6;     // ~0.1 s
        const int LeadCap       = 20;

        public PendingSwapScheduler Scheduler { get; } = new PendingSwapScheduler();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void AutoSpawn()
        {
            if (Instance == null)
                new GameObject(nameof(SwapScheduleDriver)).AddComponent<SwapScheduleDriver>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            // Subscribe in Start so the gameplay singletons (DeathNotifier, LevelManager) already exist.
            SwapTrigger.OnSwapRequested += HandleSwapRequested;
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath += HandleDeath;
            if (LevelManager.Instance != null)  LevelManager.Instance.OnBeforeLevelLoad += HandleBeforeLevelLoad;
        }

        void OnDestroy()
        {
            SwapTrigger.OnSwapRequested -= HandleSwapRequested;   // static event: unsubscribe or it leaks the handler
            if (DeathNotifier.Instance != null) DeathNotifier.Instance.OnDeath -= HandleDeath;
            if (LevelManager.Instance != null)  LevelManager.Instance.OnBeforeLevelLoad -= HandleBeforeLevelLoad;
            if (Instance == this) Instance = null;
        }

        void FixedUpdate()
        {
            // Don't flip into a half-loaded scene (triggers mid-destroy); HandleBeforeLevelLoad already reset us.
            if (LevelManager.Instance != null && LevelManager.Instance.IsLoading) return;

            var due = Scheduler.OnTick(CurrentApplyClock);
            for (int i = 0; i < due.Count; i++)
            {
                ControlMapStore.Instance?.Apply(due[i].Map);
                SwapTrigger.GreyById(due[i].TriggerId);
            }
        }

        // Host/solo only (the client gates out in SwapTrigger). Compose onto the pending-final map so two
        // in-flight swaps don't clobber, schedule locally, and (when hosting) send the reliable SWAP EVENT.
        void HandleSwapRequested(SwapRequest req)
        {
            var store = ControlMapStore.Instance;
            if (store == null) return;
            uint applyTick = CurrentApplyClock + (uint)Lead();
            var map = ControlMap.WithSwap(Scheduler.PendingFinalMap(store.Current), req.action);
            Scheduler.Schedule(applyTick, map, req.triggerId);
            NetworkManager.Instance?.SendSwapEvent(applyTick, map, req.triggerId);   // no-op unless hosting
        }

        // Cancel pending swaps on death so one scheduled before the death can't fire after respawn. Host/solo
        // only: the client cancels via the reliable, in-order DEATH EVENT (delivered after the swaps it must
        // cancel), never off the unreliable STATE-delta that drives DeathNotifier — that would race the swaps.
        // The base map is irrelevant here (host/solo don't reconcile via MapAtTick); respawn re-applies it.
        void HandleDeath(int _)
        {
            if (!Authority.IsHost) return;
            Scheduler.ResetTo(ControlMapStore.Instance != null ? ControlMapStore.Instance.Current : ControlMap.Default);
        }

        // A new level starts at Default (matches LevelManager's own map reset); drop any cross-level pending swap.
        void HandleBeforeLevelLoad(int _) => Scheduler.ResetTo(ControlMap.Default);

        // The clock apply_at_tick is measured against: the host's last-consumed client tick, else local TickClock.
        uint CurrentApplyClock
        {
            get
            {
                var nm = NetworkManager.Instance;
                if (nm != null && nm.Role == GameRole.Hosting) return nm.HostConsumedClientTick;
                return TickClock.Instance != null ? TickClock.Instance.Current : 0u;
            }
        }

        int Lead()
        {
            int lead = BaseLeadTicks;
            var nm = NetworkManager.Instance;
            if (nm != null && nm.Role == GameRole.Hosting)
                lead = Mathf.Max(lead, Mathf.CeilToInt(nm.CurrentRtt / Time.fixedDeltaTime) + 2);
            return Mathf.Clamp(lead, LeadFloor, LeadCap);
        }
    }
}
