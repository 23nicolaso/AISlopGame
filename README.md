![RIFT key art](./docs/rift-keyart.png)

# RIFT — planetary salvage arena

A small-planet aerial combat / salvage prototype built in **Unity 6000.6.1f1** (URP, new Input System). Shoot the reactors on derelict ships, scoop up the gold salvage they spill, and fly through a refinery ring to bank it before one of seven AI rivals shoots it back out of you.

Everything in the arena — the planet, atmosphere, ships, wrecks, refinery rings, sounds — is generated from code at runtime. There are no imported models, prefabs, colliders or rigidbodies: ~1300 lines of C# in `My project/Assets/Scripts` plus two custom URP shaders.

## Play

1. Open `My project` in Unity Hub (6000.6.1f1) and let Package Manager resolve dependencies.
2. Open `Assets/Scenes/SampleScene.unity` and press Play. The arena builds itself; no menu.

| Input | Action |
| --- | --- |
| Mouse | Pitch / bank (small tethered circle shows stick deflection) |
| W / S · A / D · Q / E | Pitch · roll · rudder |
| Left Shift / Left Ctrl | Throttle up / down |
| Space | Boost (recharging fuel) |
| Left mouse / F | Cannon with lead assist |
| Right mouse | Seeker missile |
| C | Recenter mouse |
| Escape | Pause |

Full rules, flight-model notes and the verification checklist are in [`PLAYTEST.md`](./PLAYTEST.md).

## The loop

- **Salvage** — shoot the glowing gold reactor on a wreck; it bursts into salvage cubes that fly to the nearest pilot.
- **Bank** — hold inside a refinery ring for ~1.4 s to claim it and convert carried cargo into score. Two pilots in the same ring contest it and nothing banks.
- **Fight** — death spills carried cargo but keeps banked score; you redeploy in three seconds. Rivals gather, fight, retaliate, repair and bank on the same rules you do.
- Higher-altitude salvage is worth more. Wrecks and rivals respawn indefinitely.

## Verify

There are no unit tests; the deterministic checks live as Editor menu items that run in Play Mode:

- `Rift > Verify planetary arena` — population, collection, banking, contest, cargo drops, respawns, pause, swept hits, neutral flight.
- `Rift > Verify flip stability and rival combat` — chase camera through loops at 30/60/144 fps, AI engagement and retaliation, real projectile hits, cooldowns, occlusion, terrain recovery.

Both throw on failure and log a `PASS` line on success.

## Tooling

- [`AGENTS.md`](./AGENTS.md) — architecture notes and constraints for AI coding agents.
- [`MCP-SETUP.md`](./MCP-SETUP.md) — MCP for Unity bridge (`Window → MCP for Unity`, HTTP on `localhost:8080`).
