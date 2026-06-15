using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Canonical per-action look: colour + label. The single source for "what an action looks like" across the
    /// control HUD, the swap-trigger banners, the swap announcement (#115), the prep telegraph (#127) and the
    /// edge flash (#126). Replaces the two divergent action-colour tables that predated v2.1 (the HUD's hex set
    /// and SwapTrigger.ColorFor's RGB set). The HUD's green/orange/cyan is the canonical palette (most prominent
    /// surface); kept distinct from PlayerIdentity's per-player colours so "what" and "who" never read as one.
    public static class ActionStyle
    {
        public static Color ColorOf(PlayerAction action) => action switch
        {
            PlayerAction.MoveHorizontal => new Color(0.451f, 0.902f, 0.549f),   // #73E68C
            PlayerAction.Jump           => new Color(1.000f, 0.722f, 0.251f),   // #FFB840
            PlayerAction.Dash           => new Color(0.451f, 0.800f, 1.000f),   // #73CCFF
            _                           => Color.white,
        };

        public static string LabelOf(PlayerAction action) => action switch
        {
            PlayerAction.MoveHorizontal => "MOVE",
            PlayerAction.Jump           => "JUMP",
            PlayerAction.Dash           => "DASH",
            _                           => "",
        };

        /// Hex (no leading #) for inline TMP rich-text colour tags.
        public static string HexOf(PlayerAction action) => ColorUtility.ToHtmlStringRGB(ColorOf(action));

        /// The action label wrapped in its colour, e.g. "<color=#FFB840>JUMP</color>".
        public static string RichLabel(PlayerAction action) => $"<color=#{HexOf(action)}>{LabelOf(action)}</color>";
    }
}
