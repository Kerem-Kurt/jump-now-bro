namespace JumpNowBro.Networking
{
    /// STATE-broadcast cadence + suppression rules. Engine-free so the gating math is CI-testable.
    /// Even-tick parity at 60 Hz simulation = 30 Hz STATE (DESIGN §8). Suppressed during async
    /// LevelManager.LoadNext so STATE's sceneIndex doesn't race the LEVEL_LOAD EVENT (#76 Finding #7).
    public static class StateBroadcastTiming
    {
        public static bool ShouldBroadcast(uint hostTick, bool isLoading) =>
            (hostTick & 1u) == 0u && !isLoading;
    }
}
