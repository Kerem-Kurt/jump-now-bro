using UnityEngine;

namespace JumpNowBro.Gameplay
{
    // Authoritative simulation tick the wire anchors to. Drives itself in FixedUpdate
    // so every role sees Current advance — including the v1.4 client, which destroys
    // PlayerController and would otherwise have nothing to drive the counter.
    // Early execution order so consumers find a non-null Instance in their own Awake.
    [DefaultExecutionOrder(-100)]
    public class TickClock : MonoBehaviour
    {
        public static TickClock Instance { get; private set; }

        // Backing field is [SerializeField] so it's live-visible in the Inspector during Play —
        // the read-only property keeps the write surface private to FixedUpdate.
        [SerializeField] uint current;
        public uint Current => current;

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

        void FixedUpdate()
        {
            current++;
        }
    }
}
