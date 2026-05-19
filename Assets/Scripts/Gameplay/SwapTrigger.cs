using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class SwapTrigger : MonoBehaviour
    {
        [SerializeField] PlayerAction actionToSwap;
        bool fired;

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
