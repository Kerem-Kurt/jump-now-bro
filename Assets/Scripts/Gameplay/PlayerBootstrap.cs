using UnityEngine;

namespace JumpNowBro.Gameplay
{
    // Wires the Player prefab's input sources to the PlayerController on the same GameObject.
    // Replaced by PlayerSpawner in v0.3 (issue #26).
    [RequireComponent(typeof(PlayerController), typeof(KeyboardInputSource_P1), typeof(KeyboardInputSource_P2))]
    public class PlayerBootstrap : MonoBehaviour
    {
        void Awake()
        {
            var ctrl = GetComponent<PlayerController>();
            var p1 = GetComponent<KeyboardInputSource_P1>();
            var p2 = GetComponent<KeyboardInputSource_P2>();
            ctrl.Inject(p1, p2);
        }
    }
}
