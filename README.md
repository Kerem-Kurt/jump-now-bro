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
- Client input at **60 Hz**, host state at **30 Hz**. Control swaps carry an `apply_at_tick` so both screens flip on the same logical tick rather than ~RTT apart.

## Status

Phase 1 (single-player local) is complete, and Phase 2 (the hand-rolled UDP network layer) is functional end-to-end: LAN host/join with broadcast discovery, host-authoritative simulation, client-side prediction + reconciliation, reliable control-swap / death / level-transition `EVENT`s scheduled to a shared tick, and graceful connection-loss handling with rejoin. It's verified in two-instance testing under an in-editor latency/loss simulator. Remaining work is visual/UX polish (character art, a UGUI connection screen, SFX).

## Controls

One character is driven by two input sources; each owns a subset of the actions, and swap triggers reassign ownership mid-level. At the start, Player 1 owns everything — crossing a swap trigger hands an action to Player 2 (the banner is tinted by action while armed, grey once crossed). **In LAN play, the host drives Player 1 and the client drives Player 2**, each on their own keyboard. **Solo on one machine**, drive both halves of the keyboard yourself.

| Action | Player 1 | Player 2 |
|---|---|---|
| Move (left/right) | A / D | ← / → |
| Jump | Left Shift | Space |
| Dash | Left Ctrl | Right Shift |

Up/down are bound but unused — movement is horizontal-only in the MVP. Touching a hazard or falling off the level respawns you at the last checkpoint; reaching the goal loads the next level; finishing all three shows the death-count summary.

## Network play (LAN)

Start every instance from `Assets/Scenes/Bootstrap.unity`. The in-game panel (top-left) drives the session:

- **Host** — binds the gameplay port and broadcasts a discovery beacon. The host runs the only authoritative simulation and drives Player 1.
- **Join** — connects to a host and drives Player 2. Enter the host's IP manually (default `127.0.0.1` for same-machine testing); LAN broadcast discovery also surfaces hosts automatically.
- **Solo** — single-player on one machine (drive both input halves yourself).
- **Leave** — graceful disconnect back to the menu.

If a peer drops, the surviving side pauses with a "connection lost" overlay: the client can **Rejoin** (resuming into the host's current level) or return to the menu; the host keeps its progress and waits for the rejoin.

**Two-machine play** needs two people — only the focused OS window receives keyboard input, so one keyboard can't drive both halves at once. For solo iteration, [ParrelSync](https://github.com/VeriorPies/ParrelSync) runs two editor instances against `127.0.0.1`.

### Network-condition simulator (testing)

To exercise the netcode under latency/loss, an **editor-only** simulator wraps each instance's outbound channel. In the connection panel's idle menu the `Sim:` button cycles **Clean / Fair / Stress** — set it per instance before Host/Join. Profiles are one-way latency / jitter / loss: **Fair** ≈ 75 ms / 20 ms / 5%, **Stress** ≈ 125 ms / 50 ms / 10% (RTT ≈ 2× the one-way latency). It compiles out of player builds.

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
