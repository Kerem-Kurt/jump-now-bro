using System;

namespace JumpNowBro.Util
{
    [Serializable]
    public struct ControlMap
    {
        public InputOwner moveOwner;
        public InputOwner jumpOwner;
        public InputOwner dashOwner;

        public static ControlMap Default => new ControlMap
        {
            moveOwner = InputOwner.P1,
            jumpOwner = InputOwner.P1,
            dashOwner = InputOwner.P1
        };

        public static ControlMap WithSwap(ControlMap current, PlayerAction action)
        {
            var result = current;
            switch (action)
            {
                case PlayerAction.MoveHorizontal: result.moveOwner = Flip(result.moveOwner); break;
                case PlayerAction.Jump:           result.jumpOwner = Flip(result.jumpOwner); break;
                case PlayerAction.Dash:           result.dashOwner = Flip(result.dashOwner); break;
            }
            return result;
        }

        private static InputOwner Flip(InputOwner o) =>
            o == InputOwner.P1 ? InputOwner.P2 : InputOwner.P1;

        /// One-to-one extraction of the four reads PlayerController.FixedUpdate does today
        /// (lines ~105–117): each action takes its bits from the owning player's frame; left+right cancels.
        public static EffectiveInput Route(ControlMap map, PlayerInputFrame p1, PlayerInputFrame p2)
        {
            var moveFrame = map.moveOwner == InputOwner.P1 ? p1 : p2;
            var jumpFrame = map.jumpOwner == InputOwner.P1 ? p1 : p2;
            var dashFrame = map.dashOwner == InputOwner.P1 ? p1 : p2;
            return new EffectiveInput
            {
                moveDir     = (moveFrame.moveRight ? 1 : 0) - (moveFrame.moveLeft ? 1 : 0),
                jumpPressed = jumpFrame.jumpPressed,
                jumpHeld    = jumpFrame.jumpHeld,
                dashPressed = dashFrame.dashPressed,
            };
        }
    }
}
