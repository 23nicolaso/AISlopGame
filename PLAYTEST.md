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
| Enter | Restart the match (results screen only) |

Lift, drag, gravity, momentum, stall behavior, and thinner air affect flight. This is an assisted small-planet prototype with compressed distances, not an IL-2-level flight simulator or accurate orbital mechanics model.

## Verification

While playing, use `Rift > Verify planetary arena` for deterministic checks of population, collection, banking, contest, cargo drops, score retention, player and rival respawning, pause behavior, swept hit detection, and neutral flight. The check resets pilot positions and changes temporary match state.

The atmosphere/suborbital screenshot menu entries stage and pause the camera; `Rift > Return to launch` resumes. Screenshots are in `My project/Captures`.

An accelerated autonomous run also verified all seven rivals earning banked scores and continuing across deaths. This is a smoke test, not a full balance or human-control playtest.

## Flip stability and rival combat update

Aircraft visuals and the chase camera now share an interpolated flight pose. The camera uses one continuous rotation frame through loops and rolls, with eased boost distance and smooth impact shake.

Rivals engage nearby aircraft even when they carry no cargo, retaliate against attackers, lead their shots, fire short cannon bursts, and extend after close passes. They predict terrain collisions and prioritize repairs when badly damaged. Combat runs have a time budget so gathering and banking remain part of the match.

The Rift > Verify flip stability and rival combat menu tests repeated camera rotations at 30/60/144 fps, combined aircraft rotations, boost transitions, target acquisition, the perception cone blind spot, delayed retaliation while loaded, actual projectile damage, spawn protection, cooldowns, occlusion, and terrain recovery. Like the original check, it changes temporary match state and resets pilot positions.

## Feedback and rival perception update

Every hit, pickup, kill and wreck break now runs through one feedback dispatcher with three weights. Camera shake is a trauma accumulator rather than a value each event overwrites, and the rig uses its square, so chip damage barely moves the frame while a kill genuinely kicks it. A kill or death the player takes part in freezes the game for 80 ms. Taking a hit paints a red chevron around the crosshair pointing at the shooter for one second, and a kill posts a banner under the rankings naming the pilot and the salvage taken.

Rivals fly the aircraft the player flies. Their attitude solver outputs stick deflection instead of setting rotation directly, so thin air and low speed cost them control authority exactly as they cost the player. They also have a perception model: a 110 degree forward cone out to 950 m, peripheral awareness inside 250 m, and gunfire heard within 600 m. Contacts outside the cone raise an Alert first, which costs 0.4 to 0.8 seconds of turning without firing, and losing a target starts a three second Search sweep along its last bearing rather than an instant return to salvaging. Approaching from dead astern without firing is now a real tactic, and so is being jumped from your own blind spot.

## Match structure, cargo weight and refinery income

A run is now a match, not an endless sandbox. Three seconds of countdown let you read the horizon with weapons cold, then five minutes of play, then a results screen. The clock sits top centre and turns red and ticking in the final minute. When time runs out the board freezes — damage, fire and pickups all stop — and a panel ranks every pilot by banked score with kills, losses and totals. Enter starts a fresh match: everyone respawns, scores and cargo clear, refineries go neutral, and loose salvage is swept away.

Cargo now has weight. Every ten units adds four percent to gravity and three percent to drag, up to sixty and forty-five percent with a full hold. A loaded aircraft climbs badly, turns wide, and burns boost fuel for less return, and the penalty applies to rivals exactly as it applies to you — the fattest pilot on the board is also the slowest target. The CARGO readout turns gold with a HEAVY tag at sixty units, which is roughly where the handling change becomes obvious.

Holding a refinery is now worth something after the capture. Every twelve seconds an uncontested owner banks five points and the ring flashes white. Holding three rings is seventy-five points a minute of free score, which is the reason to go take someone else's ring instead of farming wrecks — and because two pilots inside a ring contest it, taking one means shooting the defender out first.

`Rift > Verify planetary arena` adds four checks for this work: Ended blocks damage, fire and pickups; restart returns every pilot and gate to a fresh countdown; a 150-unit hold ends five seconds of neutral flight lower and slower than an empty one; and a held refinery pays exactly five points per twelve-second cycle without double-paying. Both verification menus pin the match phase to Playing while they run and restore it afterwards.
