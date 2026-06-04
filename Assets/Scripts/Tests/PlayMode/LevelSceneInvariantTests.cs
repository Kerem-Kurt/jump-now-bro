using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using JumpNowBro.Gameplay;
using JumpNowBro.Util;

namespace JumpNowBro.Tests.PlayMode
{
    /// Asset-invariant guard for the v1.8 cosmetic pass (refs #113). Loads each real level scene and asserts the
    /// gameplay-load-bearing facts a sprite/DrawMode swap must NOT disturb: solid platforms keep a non-trigger
    /// BoxCollider2D, there's a spawn point, one checkpoint, one goal, and every swap trigger keeps the
    /// action→id convention (see [[swaptrigger-id-convention]]: Move→1, Jump→2, Dash→3). Green on the white-box
    /// build; a cosmetic edit that nudges a collider, drops a spawn point, or re-keys a portal reddens it.
    ///
    /// PlayMode-only (it loads real scenes via SceneManager) — out of the no-Unity CI globs under Tests/PlayMode.
    /// Levels load standalone here: the trigger volumes only act in OnTriggerEnter2D, so no Bootstrap managers are
    /// needed at load time. The player-prefab collider/anchor invariants live in the integration suite, where a
    /// real player is spawned.
    public class LevelSceneInvariantTests
    {
        const int GroundLayer = 7, OneWayLayer = 8, CheckpointLayer = 10, GoalLayer = 12;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Unload every additively-loaded level so the static SwapTrigger.active list and scene objects don't
            // bleed into the next case (OnDisable de-registers each trigger as its scene unloads).
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                var sc = SceneManager.GetSceneAt(i);
                if (sc.IsValid() && sc.name != null && sc.name.StartsWith("Level_"))
                    yield return SceneManager.UnloadSceneAsync(sc);
            }
        }

        [UnityTest] public IEnumerator Level_01_Invariants() => CheckLevel("Level_01", expectedSwaps: 1);
        [UnityTest] public IEnumerator Level_02_Invariants() => CheckLevel("Level_02", expectedSwaps: 3);
        [UnityTest] public IEnumerator Level_03_Invariants() => CheckLevel("Level_03", expectedSwaps: 3);

        IEnumerator CheckLevel(string level, int expectedSwaps)
        {
            yield return SceneManager.LoadSceneAsync(level, LoadSceneMode.Additive);

            // A spawn point must exist or the player can't enter the level.
            Assert.GreaterOrEqual(ComponentsIn<PlayerSpawnPoint>(level).Count, 1,
                $"{level}: no PlayerSpawnPoint — the player would have nowhere to spawn.");

            // Every solid platform keeps a real, non-trigger, non-degenerate collider. The stone pass only touches
            // the SpriteRenderer (sprite + DrawMode), so the collider must be byte-for-byte unchanged.
            var solids = ObjectsOnLayers(level, GroundLayer, OneWayLayer);
            Assert.GreaterOrEqual(solids.Count, 1, $"{level}: expected at least one solid platform on Ground/OneWay.");
            foreach (var go in solids)
            {
                var box = go.GetComponent<BoxCollider2D>();
                Assert.IsNotNull(box, $"{level}: solid '{go.name}' lost its BoxCollider2D.");
                Assert.IsFalse(box.isTrigger, $"{level}: solid '{go.name}' became a trigger — it must stay solid.");
                Assert.Greater(box.size.x, 0f, $"{level}: '{go.name}' collider width collapsed.");
                Assert.Greater(box.size.y, 0f, $"{level}: '{go.name}' collider height collapsed.");
            }

            // Exactly one checkpoint and one goal per level (the cosmetic pass repaints them, never adds/removes).
            Assert.AreEqual(1, ObjectsOnLayers(level, CheckpointLayer).Count, $"{level}: expected exactly one checkpoint.");
            Assert.AreEqual(1, ObjectsOnLayers(level, GoalLayer).Count, $"{level}: expected exactly one level goal.");

            // Swap triggers: count is fixed, and each keeps the action→id convention. The portal commit re-keying an
            // id (so the wrong banner greys / wrong action swaps) is exactly what this catches.
            var swaps = ComponentsIn<SwapTrigger>(level);
            Assert.AreEqual(expectedSwaps, swaps.Count, $"{level}: swap-trigger count changed.");
            foreach (var t in swaps)
            {
                var action = (PlayerAction)GetField(t, "actionToSwap");
                byte id = (byte)GetField(t, "triggerId");
                Assert.AreEqual(ExpectedId(action), id,
                    $"{level}: '{t.name}' swaps {action} but triggerId is {id}, not {ExpectedId(action)}.");
            }
        }

        static byte ExpectedId(PlayerAction action) => action switch
        {
            PlayerAction.MoveHorizontal => 1,
            PlayerAction.Jump           => 2,
            PlayerAction.Dash           => 3,
            _                           => 0,
        };

        // --- scene-scoped collection helpers ---

        static List<T> ComponentsIn<T>(string sceneName) where T : Component
        {
            var result = new List<T>();
            foreach (var root in SceneManager.GetSceneByName(sceneName).GetRootGameObjects())
                result.AddRange(root.GetComponentsInChildren<T>(true));
            return result;
        }

        static List<GameObject> ObjectsOnLayers(string sceneName, params int[] layers)
        {
            var wanted = new HashSet<int>(layers);
            var result = new List<GameObject>();
            foreach (var root in SceneManager.GetSceneByName(sceneName).GetRootGameObjects())
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    if (wanted.Contains(tr.gameObject.layer))
                        result.Add(tr.gameObject);
            return result;
        }

        static object GetField(object target, string field) =>
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);
    }
}
