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

The atmosphere/suborbital screenshot menu entries stage and pause the camera; `Rift > Return to launch` resumes. Hand-taken screenshots live in `My project/Assets/Screenshots/`.

Both verification suites and the screenshots also run without anyone at the keyboard. With the Editor closed:

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.6.1f1/Unity.app/Contents/MacOS/Unity"
PROJECT="/Users/lishuyu/Codes/AISlopGame/My project"
# Both Rift*Verification suites. Exit 0 = pass, 1 = a check failed, 2 = never reached Play Mode (compile error).
"$UNITY" -batchmode -nographics -projectPath "$PROJECT" -executeMethod RiftHeadlessRunner.Run -logFile /tmp/rift-verify.log
# Five staged camera renders into docs/screenshots/ (no -nographics: this one needs the GPU). Exit 1 on a black or flat frame.
"$UNITY" -batchmode -projectPath "$PROJECT" -executeMethod RiftScreenshotRunner.Run -logFile /tmp/rift-shots.log
```

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

## Rival personalities, wreck variants and the bounty

The seven callsigns are no longer interchangeable. Each one carries its own engagement range, banking threshold, aim error, reaction delay, cargo greed and grudge length, and the leaderboard prints a four-letter tag beside every name so the differences are readable from the first match:

| Callsign | Tag | Flies like |
| --- | --- | --- |
| Moth.exe | HUNT | Commits from 800 m and reacts in 0.3 s. The most dangerous aircraft on the board. |
| Blue Finch | HORD | Banks at 80 cargo and rarely picks a fight, so it is usually the fattest target in the sky. |
| Periapsis | VULT | Chooses targets almost purely by what they are carrying, and will trade a blast radius for the kill. |
| DustRunner | STDY | Banks at 25. Small frequent deposits that are hard to pull away from. |
| Kite-09 | AVNG | Slowest to notice a hit, holds the grudge for fifteen seconds. |
| SoupDragon | ROOK | Three degrees of aim error. The arena's free first kill. |
| Last Comet | ELIT | No weak axis: half a degree of error, fast reactions, banks at 50. |

Salvage wrecks come in three kinds now, each with its own silhouette. Ordinary wrecks are unchanged. **Volatile** reactors glow violet behind a split containment cage: destroying one deals 55 damage to every pilot within 45 m — including whoever fired the shot — and spills half again as much cargo. Leading a pursuer past one is a weapon; taking the shot from inside the radius is a choice. **Armored** wrecks wear slate belts over a 200-point hull, shrug cannon fire down to thirty percent, take full missile damage, and drop three times the cargo, which finally gives the seven-second seeker a second job. Rivals keep a 60 m standoff from volatile reactors so they stop detonating them under their own nose — except the vulture, which accepts 25 m.

Three unanswered kills crown an **ace**. The ace's tracers turn gold, refining pays 1.5x, a pulsing BOUNTY strip appears under the match clock and the leaderboard tag switches to ACE. Every rival weights its target scoring toward that pilot until the mark dies, so a runaway leader gets hunted and a trailing pilot has a route back in without any explicit difficulty dial. Dying clears the streak and the bounty.

`Rift > Verify flip stability and rival combat` adds three checks: an armored wreck takes 30 of 100 cannon damage and the full 100 from a missile; a volatile detonation costs a pilot 30 m away exactly 55 hull and a pilot 300 m away nothing; and three kills set the bounty to the killer while killing that pilot clears it.
