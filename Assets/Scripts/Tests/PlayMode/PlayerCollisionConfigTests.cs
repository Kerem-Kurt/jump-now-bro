using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Tests.PlayMode
{
    /// PlayMode coverage for #106: the collision world the host AND the v1.5 client predictor build is replay-safe
    /// — its casts follow the body's CURRENT position, which is exactly what reconciliation replay relies on
    /// (the client sets rb.position before each Movement.Step). Can't live in the no-Unity CI: it needs real
    /// Physics2D casts, so it's an in-Editor PlayMode suite (and stays out of the CI globs under Tests/PlayMode).
    public class PlayerCollisionConfigTests
    {
        const int GroundLayer = 8;                          // self-consistent: ground object + mask both use this
        GameObject ground;
        GameObject player;
        Rigidbody2D rb;

        [SetUp]
        public void SetUp()
        {
            ground = new GameObject("TestGround") { layer = GroundLayer };
            ground.transform.position = new Vector3(0f, 0f, 0f);
            var gbox = ground.AddComponent<BoxCollider2D>();
            gbox.size = new Vector2(20f, 1f);               // wide floor, top edge at y = +0.5

            player = new GameObject("TestPlayer");
            player.transform.position = new Vector3(0f, 5f, 0f);
            rb = player.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.useFullKinematicContacts = true;
            var pbox = player.AddComponent<BoxCollider2D>();
            pbox.size = new Vector2(1f, 2f);               // bottom edge at center.y - 1

            var groundCheck = new GameObject("GroundCheck");
            groundCheck.transform.SetParent(player.transform, false);
            groundCheck.transform.localPosition = new Vector3(0f, -1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            if (player != null) Object.Destroy(player);
            if (ground != null) Object.Destroy(ground);
        }

        static ICollisionWorld BuildConfigWorld(GameObject player, Rigidbody2D rb)
        {
            var cfg = player.AddComponent<PlayerCollisionConfig>();
            // solidLayers / groundCheckPoint / groundCheckRadius are private [SerializeField]s set in the Inspector;
            // a test has no Inspector, so seed them via reflection to exercise the real CreateWorld seam.
            Set(cfg, "solidLayers", (LayerMask)(1 << GroundLayer));
            Set(cfg, "groundCheckPoint", player.transform.Find("GroundCheck"));
            Set(cfg, "groundCheckRadius", 0.15f);
            return cfg.CreateWorld(rb);
        }

        static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        [UnityTest]
        public IEnumerator ConfigCreateWorld_Grounded_TrueOnGround_FalseInAir()
        {
            var world = BuildConfigWorld(player, rb);
            yield return null;

            rb.position = new Vector2(0f, 1f);              // bottom edge at y=0, groundCheck at y=0 inside floor top (0.5)
            Physics2D.SyncTransforms();
            Assert.IsTrue(world.Grounded(0f, 1f), "groundCheck overlapping the floor should report grounded");

            rb.position = new Vector2(0f, 5f);             // well above the floor
            Physics2D.SyncTransforms();
            Assert.IsFalse(world.Grounded(0f, 5f), "high in the air should not report grounded");
        }

        [UnityTest]
        public IEnumerator SweepY_BlocksAgainstFloorBelow()
        {
            var world = BuildConfigWorld(player, rb);
            yield return null;

            rb.position = new Vector2(0f, 3f);             // bottom edge at y=2, floor top at y=0.5 → gap ≈ 1.5
            Physics2D.SyncTransforms();
            world.SweepY(0f, 3f, -5f, out float resolvedDy, out bool blocked);

            Assert.IsTrue(blocked, "a downward sweep into the floor must report blocked");
            Assert.Less(resolvedDy, 0f, "resolved motion is downward");
            Assert.Greater(resolvedDy, -1.5f, "must stop short of penetrating the floor (gap ≈ 1.5)");
        }

        [UnityTest]
        public IEnumerator Sweep_CastsFromRigidbodyPosition_ReplaySafety()
        {
            // The load-bearing #106 property: casts originate at rb.position, so the client can re-establish the
            // origin (rb.position = predicted; SyncTransforms) before each replayed step and get host-identical
            // collision. Same sweep, two different rb positions → different result proves the origin is honored.
            var world = BuildConfigWorld(player, rb);
            yield return null;

            rb.position = new Vector2(40f, 3f);            // far from the 20-wide floor → clear
            Physics2D.SyncTransforms();
            world.SweepY(40f, 3f, -5f, out _, out bool blockedAway);
            Assert.IsFalse(blockedAway, "away from the floor the same downward sweep is unobstructed");

            rb.position = new Vector2(0f, 3f);             // back over the floor
            Physics2D.SyncTransforms();
            world.SweepY(0f, 3f, -5f, out _, out bool blockedOver);
            Assert.IsTrue(blockedOver, "over the floor the same sweep is blocked — cast follows rb.position");
        }
    }
}
