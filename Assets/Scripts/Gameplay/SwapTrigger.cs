using System.Collections.Generic;
using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class SwapTrigger : MonoBehaviour
    {
        static readonly List<SwapTrigger> active = new List<SwapTrigger>();

        [SerializeField] PlayerAction actionToSwap;
        bool fired;
        bool firedAtCheckpoint;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        // Checkpoint locks in which triggers have fired up to this point.
        public static void SaveCheckpointStates()
        {
            foreach (var t in active) t.firedAtCheckpoint = t.fired;
        }

        // Respawn: triggers crossed since the checkpoint re-arm; ones crossed before it stay consumed.
        public static void RestoreCheckpointStates()
        {
            foreach (var t in active) t.fired = t.firedAtCheckpoint;
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired) return;
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            var store = ControlMapStore.Instance;
            if (store == null)
            {
                Debug.LogError($"SwapTrigger on '{name}' fired but no ControlMapStore in scene.", this);
                return;
            }

            fired = true;
            store.Apply(ControlMap.WithSwap(store.Current, actionToSwap));
        }
    }
}
