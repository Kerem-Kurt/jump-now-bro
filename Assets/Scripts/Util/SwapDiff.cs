namespace JumpNowBro.Util
{
    /// Engine-free diff of two ControlMaps: which action's owner changed, and to whom. The swap announcement
    /// (#115) and the screen-edge flash (#126) both turn a before/after map pair into "this action just went
    /// to this owner"; keeping that comparison in one tested place stops each feature re-deriving it. A single
    /// SwapTrigger flips exactly one action and SwapScheduleDriver applies one DueSwap at a time, so the first
    /// change is the change.
    public static class SwapDiff
    {
        public readonly struct Change
        {
            public readonly PlayerAction action;
            public readonly InputOwner newOwner;
            public Change(PlayerAction action, InputOwner newOwner)
            {
                this.action = action;
                this.newOwner = newOwner;
            }
        }

        /// First action whose owner differs between old and @new, or null if the maps are identical.
        public static Change? FirstChange(ControlMap old, ControlMap @new)
        {
            if (old.moveOwner != @new.moveOwner) return new Change(PlayerAction.MoveHorizontal, @new.moveOwner);
            if (old.jumpOwner != @new.jumpOwner) return new Change(PlayerAction.Jump, @new.jumpOwner);
            if (old.dashOwner != @new.dashOwner) return new Change(PlayerAction.Dash, @new.dashOwner);
            return null;
        }
    }
}
