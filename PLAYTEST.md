# RIFT — planetary salvage arena

Open `My project` in Unity and play `Assets/Scenes/SampleScene.unity`.
The arena builds automatically; no downloaded assets or start menu are required.

## The loop

- Shoot the glowing gold reactors on derelict ships. Fly close to the released gold salvage to collect it.
- Fly through the large refinery rings and remain inside briefly to capture and bank your cargo. Cyan is neutral, teal is yours, orange is a rival's; red indicates a contested zone.
- Banked salvage determines your leaderboard position. Seven named AI rivals gather, fight, bank, die, and respawn; their scores are earned in the simulation.
- Death spills carried cargo, preserves banked score, and automatically redeploys you after three seconds — into one of three launch corridors, picked so that no living rival is within 300 m of where you come back. Being shot down throws a gold fireball and a breakup boom; flying into the ground or burning through on re-entry sheds pale cold hull plating with a low thud instead, so you can tell from across the arena which one happened.
- Resources and rivals return indefinitely. **Higher-altitude salvage is worth far more**: the three low-orbit fields hold wrecks worth 24 apiece against the surface fields' 8, and everything picked up above the coast line is doubled again on collection. Getting it home is the problem — thin air, three wrecks instead of five, and no cover.

## Flight

Mouse displacement controls pitch and bank; the small tethered circle shows stick deflection. Return to center to stop commanding rotation, or press C to recenter the mouse. Banking tilts the lift vector: use pitch to pull through turns, not sideways strafing.

Mouse flight sensitivity is intentionally reduced so small aiming corrections do not throw the aircraft into a hard roll or pitch. Keyboard controls remain full-strength.

Mouse turns are **coordinated**: a quarter of your mouse bank is fed into the rudder automatically, so a sweeping turn comes out clean instead of skidding. Q and E remain a full-strength independent rudder for when you want to skid on purpose, and A/D bank without touching the rudder at all.

Let go of the roll axis and the aircraft **levels its own wings** at about 15°/s against the local vertical — a fifth of what a full-stick roll does, so it never fights you. The assist stands down the moment you put any roll input in, when you are pulling hard on the pitch axis, and past 100° of bank: if you have committed to going inverted, it lets you finish the roll. It is a player-only assist; the AI flies on the same raw control channel it always did.

| Input | Action |
| --- | --- |
| W / S | Pitch down / up |
| A / D | Roll left / right (no coupled rudder) |
| Q / E | Rudder left / right |
| Left Shift / Left Ctrl | Increase / decrease persistent throttle |
| Space | Boost (recharging fuel) |
| Left mouse / F | Cannon, with narrow-cone lead assistance |
| Right mouse | Start / hold a seeker lock — press again once locked to fire (instant against wrecks) |
| C | Center mouse steering |
| Escape | Pause / resume (opens the comfort panel) |
| Up / Down (or W / S) | Select a comfort row — **while paused** |
| Left / Right (or A / D) | Adjust the selected comfort row — **while paused** |
| 1 / 2 / 3 | Jump straight to a comfort row — **while paused** |
| Enter | Restart the match (results screen only) |

Lift, drag, gravity, momentum, stall behavior, and thinner air affect flight. This is an assisted small-planet prototype with compressed distances, not an IL-2-level flight simulator or accurate orbital mechanics model.

## Weapons

**The cannon gets less accurate as it gets hot.** A cold gun puts the round exactly on the bore; by the time heat reaches the 92% lockout the cone has opened to about two degrees. You can see it happen — the reticle's outer ring swells from 17 to 26 pixels as the gun warms — so the choice is always visible: keep the trigger down and accept a spray, or fire in bursts and keep the pinpoint that the 1.75× precision bonus needs. Heat bleeds off at 22% a second, and the AI is unaffected by this: rival dispersion comes from each pilot's own signature aim jitter, not from temperature.

**The seeker is now locked, not launched.** Right mouse starts a lock on whatever rival sits in the 18° cone within 650 m; the ring around them tightens from 40 px to 14 px over 1.2 seconds while a pip climbs in your ears, then the box turns red, prints `LOCK`, and the bottom strip reads `LOCKED`. Press right mouse again to launch. The lock is not sticky: if the target leaves the cone, leaves 650 m, dies, or slips behind the planet, the whole 1.2 seconds is gone and you start over — which means hard-breaking out of someone's nose is a real defence, and so is holding boost once the missile is already in the air. Wrecks are the exception: they cannot evade, so a seeker fires at them instantly, which is still the cheapest way through an armoured belt.

## Two kinds of refinery

The six sites are no longer six copies of each other. Which band you are flying in is the main decision the map asks you to make, and you can tell them apart from the approach by their dock lighting.

- **Surface fields** (refineries 1, 3, 5 — ring at 170 m, teal docks and gold approach chevrons). Five wrecks worth 8 each, sitting in air thick enough to turn hard in, with rock spires and masts to fly through. Refineries 1 and 5 also carry the only armoured wrecks in the arena, which is where a seeker earns its keep.
- **Low-orbit fields** (refineries 2, 4, 6 — ring at 460 m, gold docks and teal chevrons). Three wrecks worth **24** each, above the coast line where every pickup already doubles — so one good pass up there is worth more than clearing a whole surface field. The price is the air: your controls go soft, the wings stop biting, and a full hold that heavy has to be flown all the way back down to a ring. Expect company, because the AI does the same arithmetic you do.

## Sound

The engine is two loops, not one. A low idle bed that is loudest with the throttle closed, and a rough burner layer that comes up with throttle and jumps again on boost — so you can hear how hard you are pushing without looking at the strip. Over the top of it sits the slipstream: broadband air noise that starts at 40 m/s, climbs with speed, and thins out with the atmosphere. When it goes silent you are in near-vacuum and your controls have gone with it, which is the same cue the wind streaks give your eyes.

Events have melodies rather than beeps. Claiming a refinery is three rising notes; banking cargo is four, lower down, so you can tell a claim from a deposit without reading the feed. A rival doing either still chimes, at a third of the volume — the board changing is news, it just is not your news. The countdown ticks once a second and resolves a fifth higher on GO, and the match ends on a four-note sting under the results panel. All of it, plus the seeker pip and the missile warning, is generated in code at startup; there are no audio files in this project.

## Reading the arena

The chase camera sits 14 m behind the aircraft (17 m on the burner) and the airframes are drawn 35% larger, so a rival at 60 m is a readable silhouette rather than a speck. Cannon tracers are thick enough to follow out to 100 m, and the engine plume stretches to two and a half times its length while the burner is lit — you can see a rival commit to a boost from directly astern.

The sky is a real gradient now, not a flat colour: a warm haze band along the horizon that leans toward the sun, deep blue overhead, and the sun itself as a disc with a bloom halo. All of it thins out with altitude on the same curve the air does, so by roughly 700 m the sky has emptied into space. Stars work the other way: invisible below 150 m, fully out above 600 m. If you can see stars, your wings have almost nothing left to bite on.

Three things exist purely so you can tell where you are and how fast you are going:

- **Refinery beacons.** Every capture ring fires a 400 m light pillar straight up along the local vertical, in the ring's current owner colour, dissolving into the sky at the top rather than stopping at a flat cap. It is visible from about 2 km, which is far enough to pick your next refinery before you can resolve the ring itself — and from that range the dock lighting already tells you whether it is a surface field or a low-orbit one.
- **Ground furniture.** Rock spires (up to 70 m) and relay masts with lit tips are scattered within 350 m of every refinery. They have no collision — you cannot hit them — but at low level they are the only thing that tells you 150 m/s from 80 m/s. Cloud banks sit between 40 m and 110 m and are kept at least 400 m clear of every ring, so weather never hides a fight.
- **Wind streaks.** Above 40 m/s the air starts showing streaks past the canopy, doubling on the burner and fading out as the atmosphere thins. In near-vacuum they stop entirely, which is the cue that your controls have gone soft.

## Reading the fight

The HUD answers five questions without you having to look away from the reticle.

- **What just happened.** An event feed runs down the right side under the rankings: kills across the whole board, refineries changing hands, your own banking, overcharges lighting up, and anyone getting crowned ace. Four rows, newest on top, three seconds each. Your own rows are coloured — teal when you did it, red when it was done to you — everyone else's are grey, so you can skim it in peripheral vision.
- **Something is chasing you.** A seeker with your name on it puts a magenta chevron on the reticle ring pointing at it (the plain red chevron still means "you were shot from there"), a `MISSILE` readout with its range at the top of the screen, and a tone that speeds up as it closes — one beep a second at 600 m, eight a second at 40 m. **Hold boost to break it:** a burner-jinking target cuts the seeker's turn rate roughly in half, which is usually enough to make it overshoot. There is no flare key; the boost you already have is the counter-play.
- **What you are aimed at.** Whatever falls inside the narrow lead-assist cone gets a full target box: four corner brackets that snap in from 1.4×, a health bar, a callsign and a range in metres. Wrecks get the same treatment, colour-coded — gold for ordinary salvage, violet for a volatile reactor, silver for an armoured hull that will shrug off your cannon. Everything else you can see keeps a pair of dim ticks.
- **Your gun is cooked.** Past 92% heat the cannon simply will not fire. The heat bar fills solid and pulses red, `OVERHEAT` appears both beside the bar and in the central warning stack, the whole reticle turns red, and the weapon bay vents once with a hiss. Nothing recovers faster or slower than before — the state is just impossible to miss now.
- **That shot counted.** Landing a hit kicks the reticle's four corner ticks outward and snaps them back. If your round's line of flight passed within 1.4 m of the hull centre it was a precision hit: the ticks go gold, an `x1.75` tag flashes, and the round does 1.75× damage (21 instead of 12 from the cannon, 105 instead of 60 from a seeker). Cargo and banked totals pop when they change, so a pickup or a deposit registers even mid-turn.

### Comfort settings

Pause with Escape and a COMFORT panel appears under the PAUSED text. Three rows, adjusted with the arrow keys (or WASD), remembered between sessions:

- **SHAKE** — 0 to 100% in steps of 10, default 80. Scales every source of camera shake at once; 0% removes it entirely.
- **REDUCE FLASHING** — turns the full-screen damage flash into a constant dim tint and takes the pulse out of the warning text and the overheat bar.
- **REDUCE CAMERA MOTION** — removes the roll component of impact shake and the field-of-view widening on boost, which are the two things most likely to cause motion discomfort.

## Vendetta

Whoever shoots you down is marked for sixty seconds: their rankings tag turns red and reads VNDT, a red ring breathes around their aircraft with the seconds left under it, and the feed says VENDETTA and their callsign. Take them down inside the window and whatever they were carrying is paid a second time straight into your banked score, twenty at the least, with a VENDETTA SETTLED line. Somebody else getting there first, or the clock running out, quietly clears it. It is one grudge at a time and it is the only rival the HUD ever tells you to want, so redeploying always comes with a target already chosen.

## Balance runs

Because nobody had flown the shared flight model by hand, the game plays itself: `RiftBalanceRunner` hands the player's airframe to a rotating rival personality and steps whole 300 s matches at a fixed 0.02 s, logging every death, every launch out of the atmosphere and every banking approach. Twelve rounds of that are written up in `docs/BALANCE-REPORT.md`. The headline changes it forced: the six salvage fields now sit 32 degrees apart along one arc instead of around the whole planet, wrecks take 70 s to regenerate so a field runs dry and its pilots move on, rivals obey an energy rule in thin air (no climb that would coast past the target's altitude), fly a straight run-in at a ring instead of orbiting it, and the cannon does 18 a round.

```bash
RIFT_BALANCE_MATCHES=3 "$UNITY" -batchmode -nographics -projectPath "$PROJECT" -executeMethod RiftBalanceRunner.Run -logFile /tmp/rift-balance.log
grep "\[BAL\] match" /tmp/rift-balance.log      # one line per match; docs/balance/latest.json has the per-pilot detail
```

## Verification

While playing, use `Rift > Verify planetary arena` for deterministic checks of population, collection, banking, contest, cargo drops, score retention, player and rival respawning, pause behavior, swept hit detection, neutral flight, the per-band wreck values and counts, the engine layer cross-fade, and the wind bed's speed and vacuum gates. The check resets pilot positions and changes temporary match state.

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
