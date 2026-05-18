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
    }
}
