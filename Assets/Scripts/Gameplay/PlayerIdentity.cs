using System;
using UnityEngine;
using JumpNowBro.Util;

namespace JumpNowBro.Gameplay
{
    /// Per-player display identity, the "who" axis (name + colour) that ownership UI reads, distinct from the
    /// per-action colour the control HUD already uses. The host fills P1 from its own menu entry and P2 from the
    /// client's HELLO; the client fills the mirror from the WELCOME, so both screens agree (#114, #125).
    ///
    /// Plain static state, no scene object: NetworkManager sets it at handshake and resets it between sessions.
    /// HUD elements read NameOf/ColorOf and re-render on OnChanged (identity can arrive after a level's HUD starts).
    public static class PlayerIdentity
    {
        /// Fixed assignment palette. Slot 0 = host/P1, slot 1 = client/P2; the extra slots reserve room for a
        /// later picker (#128) or more players without another protocol change. Chosen distinct from the control
        /// HUD's per-action green/orange/cyan so the two colour axes don't read as the same thing.
        public static readonly Color[] Palette =
        {
            new Color(1.00f, 0.36f, 0.36f),   // 0 coral red
            new Color(0.69f, 0.49f, 1.00f),   // 1 violet
            new Color(0.25f, 0.85f, 0.78f),   // 2 teal
            new Color(1.00f, 0.50f, 0.82f),   // 3 pink
            new Color(0.36f, 0.66f, 1.00f),   // 4 sky
            new Color(1.00f, 0.62f, 0.27f),   // 5 amber
        };

        struct Entry { public string Name; public byte ColorIndex; }

        static readonly Entry[] entries =
        {
            new Entry { Name = "P1", ColorIndex = 0 },
            new Entry { Name = "P2", ColorIndex = 1 },
        };

        /// Raised on any identity change so subscribed HUD elements can re-render.
        public static event Action OnChanged;

        public static void Set(InputOwner owner, string name, byte colorIndex)
        {
            entries[(int)owner] = new Entry
            {
                Name = string.IsNullOrWhiteSpace(name) ? DefaultName(owner) : name.Trim(),
                ColorIndex = colorIndex,
            };
            OnChanged?.Invoke();
        }

        public static string NameOf(InputOwner owner) => entries[(int)owner].Name;
        public static Color ColorOf(InputOwner owner) => Palette[entries[(int)owner].ColorIndex % Palette.Length];

        /// Back to the solo defaults (P1 / P2 on slots 0 / 1). Called when a session ends or a solo run starts.
        public static void Reset()
        {
            entries[0] = new Entry { Name = "P1", ColorIndex = 0 };
            entries[1] = new Entry { Name = "P2", ColorIndex = 1 };
            OnChanged?.Invoke();
        }

        static string DefaultName(InputOwner owner) => owner == InputOwner.P1 ? "P1" : "P2";
    }
}
