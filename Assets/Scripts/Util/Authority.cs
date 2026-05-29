using System;

namespace JumpNowBro.Util
{
    /// "Am I allowed to mutate authoritative game state?" — true on Host + SinglePlayer, false on Client.
    /// The four trigger volumes (SwapTrigger / Checkpoint / Hazard / LevelGoal) gate their effects on this
    /// so the client's STATE-rendered character can't fire local swaps / deaths / level loads.
    ///
    /// Lives in Util so Gameplay triggers can read it without referencing Networking (one-way asmdef:
    /// Networking → Gameplay → Util). NetworkManager registers the real check at Awake; default returns
    /// true so SinglePlayer scenes, Edit Mode tests, and any scene without a NetworkManager just work.
    public static class Authority
    {
        static Func<bool> isHostQuery = () => true;

        public static void RegisterIsHost(Func<bool> query) =>
            isHostQuery = query ?? (() => true);

        public static bool IsHost => isHostQuery();
    }
}
