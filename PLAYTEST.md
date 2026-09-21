# RIFT — planetary salvage arena

Open `My project` in Unity and play `Assets/Scenes/SampleScene.unity`.
The arena builds automatically; no downloaded assets or start menu are required.

## The loop

- Shoot the glowing gold reactors on derelict ships. Fly close to the released gold salvage to collect it.
- Fly through the large refinery rings and remain inside briefly to capture and bank your cargo. Cyan is neutral, teal is yours, orange is a rival's; red indicates a contested zone.
- Banked salvage determines your leaderboard position. Seven named AI rivals gather, fight, bank, die, and respawn; their scores are earned in the simulation.
- Death spills carried cargo, preserves banked score, and automatically redeploys you after three seconds.
- Resources and rivals return indefinitely. Higher-altitude salvage is worth more.

## Flight

Mouse displacement controls pitch and bank; the small tethered circle shows stick deflection. Return to center to stop commanding rotation, or press C to recenter the mouse. Banking tilts the lift vector: use pitch to pull through turns, not sideways strafing.

Mouse flight sensitivity is intentionally reduced so small aiming corrections do not throw the aircraft into a hard roll or pitch. Keyboard controls remain full-strength.

| Input | Action |
| --- | --- |
| W / S | Pitch down / up |
| A / D | Roll left / right |
| Q / E | Rudder left / right |
| Left Shift / Left Ctrl | Increase / decrease persistent throttle |
| Space | Boost (recharging fuel) |
| Left mouse / F | Cannon, with narrow-cone lead assistance |
| Right mouse | Seeker missile |
| C | Center mouse steering |
| Escape | Pause / resume |

Lift, drag, gravity, momentum, stall behavior, and thinner air affect flight. This is an assisted small-planet prototype with compressed distances, not an IL-2-level flight simulator or accurate orbital mechanics model.

## Verification

While playing, use `Rift > Verify planetary arena` for deterministic checks of population, collection, banking, contest, cargo drops, score retention, player and rival respawning, pause behavior, swept hit detection, and neutral flight. The check resets pilot positions and changes temporary match state.

The atmosphere/suborbital screenshot menu entries stage and pause the camera; `Rift > Return to launch` resumes. Screenshots are in `My project/Captures`.

An accelerated autonomous run also verified all seven rivals earning banked scores and continuing across deaths. This is a smoke test, not a full balance or human-control playtest.

## Flip stability and rival combat update

Aircraft visuals and the chase camera now share an interpolated flight pose. The camera uses one continuous rotation frame through loops and rolls, with eased boost distance and smooth impact shake.

Rivals engage nearby aircraft even when they carry no cargo, retaliate against attackers, lead their shots, fire short cannon bursts, and extend after close passes. They predict terrain collisions and prioritize repairs when badly damaged. Combat runs have a time budget so gathering and banking remain part of the match.

The Rift > Verify flip stability and rival combat menu tests repeated camera rotations at 30/60/144 fps, combined aircraft rotations, boost transitions, target acquisition, loaded retaliation, actual projectile damage, spawn protection, cooldowns, occlusion, and terrain recovery. Like the original check, it changes temporary match state and resets pilot positions.
