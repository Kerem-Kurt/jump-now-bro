# Jump Now Bro!

A 2D LAN co-op platformer where two players share control of one character. Triggers throughout the level swap which player drives which inputs — coordinate or fall.

**Applied Networks final project.** The interesting part is the hand-rolled UDP networking layer underneath the game; the game itself exists as a vehicle for that.

## Stack

- **Engine:** Unity 6.4 (6000.4.7f1), Universal 2D / URP
- **Language:** C#
- **Networking:** raw UDP via `System.Net.Sockets.UdpClient` with a custom reliability layer on top. No Unity multiplayer libraries (no Netcode for GameObjects, no Mirror, no Photon).

## Architecture (TL;DR)

- Host-authoritative listen-server — one player hosts and runs the only authoritative simulation; the other connects as a client.
- Fixed 11-byte packet header (type, seq, ack, ack-bits, timestamp).
- Two channels: unreliable (`INPUT`, `STATE`) and reliable (`HELLO`, `EVENT`, `PING`, `GOODBYE`).
- LAN broadcast discovery on `255.255.255.255`.
- RTT-driven retransmission via `PING`/`PONG` sampled once per second.
- Host owns the `ControlMap`; trigger volumes mutate it and broadcast the change as a reliable `EVENT`.

## Status

Phase 1 (single-player local, built solo by Kerem) is playable end-to-end: all three levels, the control-swap mechanic, death/respawn with checkpoints, dash trail and screen-shake juice, and a level-complete summary. Remaining polish (SFX) is deferred. Phase 2 (split network layer with teammate) hasn't started.

## Controls

Phase 1 runs on a single keyboard. One character is driven by two input sources; each owns a subset of the actions, and swap triggers reassign ownership mid-level. At the start, Player 1 owns everything — crossing a swap trigger hands an action to Player 2 (the banner is tinted by action while armed, grey once crossed). To play solo, drive both halves of the keyboard yourself.

| Action | Player 1 | Player 2 |
|---|---|---|
| Move (left/right) | A / D | ← / → |
| Jump | Left Shift | Space |
| Dash | Left Ctrl | Right Shift |

Up/down are bound but unused — movement is horizontal-only in the MVP. Touching a hazard or falling off the level respawns you at the last checkpoint; reaching the goal loads the next level; finishing all three shows the death-count summary.

## Building

1. Install Unity 6.4 (6000.4.7f1) via Unity Hub.
2. Open this folder in Unity Hub → "Add" → select the cloned repo.
3. Open `Assets/Scenes/Bootstrap.unity` and press **Play** — it loads Level 1 additively and spawns the player. Always start from Bootstrap; the persistent managers live there, so playing a level scene on its own won't spawn anything.
4. To produce a standalone build, use **File → Build Profiles** for a Mac (IL2CPP) or Windows (Mono) target.

## Repo Layout

- `Assets/` — all Unity content (scripts, scenes, sprites, settings)
- `Packages/` — Unity package manifest
- `ProjectSettings/` — Unity project-wide settings
- `.gitignore`, `.gitattributes` — Unity-tuned, with Git LFS for binary assets
- `CLAUDE.md` — design + conventions reference for AI assistants

## License

No license — private project for academic submission.
