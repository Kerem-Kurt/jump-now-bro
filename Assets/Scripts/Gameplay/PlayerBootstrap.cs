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

            // #124: solo intent sources are both local keyboards. Networked roles overwrite this registration the
            // same frame in WireHosting/WireClient (before any ghost reads it). Add the render-only cue once here so
            // it exists in every role (this Awake runs on every spawned Player; the cue ignores the destroyed sim).
            GhostIntentSources.Register(() => GhostIntentSources.From(p1), () => GhostIntentSources.From(p2));
            if (GetComponent<GhostIntentCue>() == null) gameObject.AddComponent<GhostIntentCue>();
        }
    }
}
