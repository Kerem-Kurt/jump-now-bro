using System;
using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    public class ControlMapStore : MonoBehaviour
    {
        public static ControlMapStore Instance { get; private set; }

        public ControlMap Current { get; private set; } = ControlMap.Default;
        public event Action<ControlMap> OnChanged;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // Single mutation choke point. SwapTrigger is the only caller in Phase 1;
        // Phase 2's host wraps this to also enqueue a reliable SWAP EVENT.
        public void Apply(ControlMap newMap)
        {
            Current = newMap;
            OnChanged?.Invoke(newMap);
        }
    }
}
