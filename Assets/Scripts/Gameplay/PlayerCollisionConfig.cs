using UnityEngine;
using UnityEngine.Serialization;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Collision config shared by the host's PlayerController and the v1.5 client predictor. It lives as its own
    /// component rather than on PlayerController because the client destroys PlayerController in WireClient — the
    /// predictor still needs the layers / ground-check refs to build the SAME UnityCollisionWorld the host uses,
    /// and a component on the surviving Player root is the only place that data still exists on the client.
    /// CreateWorld is the single construction seam both roles call, so host and client cast against identical
    /// geometry (the determinism #106 hinges on).
    public sealed class PlayerCollisionConfig : MonoBehaviour
    {
        [SerializeField, FormerlySerializedAs("groundLayers")] LayerMask solidLayers;
        [SerializeField] Transform groundCheckPoint;
        [SerializeField] float groundCheckRadius = 0.15f;

        public ICollisionWorld CreateWorld(Rigidbody2D rb) =>
            new UnityCollisionWorld(rb, solidLayers, groundCheckPoint, groundCheckRadius);
    }
}
