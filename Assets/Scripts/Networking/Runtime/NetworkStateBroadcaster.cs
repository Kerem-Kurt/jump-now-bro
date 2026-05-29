using System;
using UnityEngine;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Networking
{
    /// Host-side STATE pump. Self-subscribes (in Awake) to PlayerController.OnSimStepCompleted so the
    /// broadcaster captures the authoritative state AT the moment of FixedUpdate completion — no race
    /// against a "Bind() landed after the first sim step" hazard (#76 Finding #9). On every other host
    /// tick (~30 Hz at 60 Hz sim, per DESIGN §8) it builds a StateBody and sends it on the unreliable
    /// channel, suppressed during async LevelManager.LoadNext to avoid LEVEL_LOAD/STATE wire races.
    ///
    /// Runs only when the host is actually hosting (defense in depth — #78 spawner only attaches this
    /// component on Hosting role, but the role check stays for robustness).
    [DefaultExecutionOrder(50)]
    public sealed class NetworkStateBroadcaster : MonoBehaviour
    {
        PlayerController controller;
        IReliableTransport transport;
        ControlMapStore store;
        LevelManager levelManager;
        Func<uint> lastConsumedClientTickGetter;
        byte[] sendBuffer;

        void Awake()
        {
            controller = GetComponent<PlayerController>();
            if (controller != null) controller.OnSimStepCompleted += Handle;
        }

        void OnDestroy()
        {
            if (controller != null) controller.OnSimStepCompleted -= Handle;
        }

        /// Called by the role-aware spawner (#78). Bind happens AFTER Awake's self-subscription, so the
        /// event is wired even if the first FixedUpdate beats Bind to the punch — Handle just no-ops on
        /// missing transport.
        public void Bind(IReliableTransport transport, ControlMapStore store, LevelManager levelManager,
                         Func<uint> lastConsumedClientTickGetter)
        {
            this.transport = transport;
            this.store = store;
            this.levelManager = levelManager;
            this.lastConsumedClientTickGetter = lastConsumedClientTickGetter;
            sendBuffer = new byte[StateBody.Size];
        }

        void Handle(uint hostTick, MovementState state)
        {
            if (transport == null) return;
            var role = NetworkManager.Instance != null ? NetworkManager.Instance.Role : GameRole.SinglePlayer;
            if (role != GameRole.Hosting) return;                                  // defense in depth: only host broadcasts
            bool isLoading = levelManager != null && levelManager.IsLoading;
            if (!StateBroadcastTiming.ShouldBroadcast(hostTick, isLoading)) return;

            var body = new StateBody
            {
                snapshotTick           = hostTick,
                lastConsumedClientTick = lastConsumedClientTickGetter != null ? lastConsumedClientTickGetter() : 0u,
                deathCount             = (ushort)(controller != null ? controller.DeathCount : 0),
                sceneIndex             = (byte)(levelManager != null && levelManager.CurrentLevelIndex >= 0 && levelManager.CurrentLevelIndex < 0xFF
                                                ? levelManager.CurrentLevelIndex : 0xFF),
                controlMap             = store != null ? store.Current : ControlMap.Default,
                remoteInputFrame       = controller != null ? controller.LastHostInputFrame : default,
                movementState          = state,
            };
            int n = body.Write(sendBuffer);
            transport.Send(Channel.Unreliable, MessageType.State, new ReadOnlySpan<byte>(sendBuffer, 0, n));
        }
    }
}
