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

Early scaffolding. Phase 1 (game built solo by Kerem) in progress; Phase 2 (split network layer with teammate) hasn't started.

## Building

1. Install Unity 6.4 (6000.4.7f1) via Unity Hub.
2. Open this folder in Unity Hub → "Add" → select the cloned repo.
3. Play in the editor, or use **File → Build Profiles** to produce a standalone Mac (IL2CPP) or Windows (Mono) build.

## Repo Layout

- `Assets/` — all Unity content (scripts, scenes, sprites, settings)
- `Packages/` — Unity package manifest
- `ProjectSettings/` — Unity project-wide settings
- `.gitignore`, `.gitattributes` — Unity-tuned, with Git LFS for binary assets
- `CLAUDE.md` — design + conventions reference for AI assistants

## License

No license — private project for academic submission.
