using UnityEngine;

namespace JumpNowBro.Gameplay
{
    [RequireComponent(typeof(Collider2D))]
    public class LevelGoal : MonoBehaviour
    {
        bool fired;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (fired) return;
            if (!other.TryGetComponent<PlayerController>(out _)) return;

            var manager = LevelManager.Instance;
            if (manager == null)
            {
                Debug.LogError($"LevelGoal on '{name}' fired but no LevelManager in scene.", this);
                return;
            }

            fired = true;
            manager.LoadNext();
        }
    }
}
