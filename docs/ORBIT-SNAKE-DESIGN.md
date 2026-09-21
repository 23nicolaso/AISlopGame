# ORBIT SNAKE — Design

## Status

Supersedes the free-flight Kepler draft committed as `9304457` (`OrbitSnake.cs` / `OrbitBodies.cs`). That code does not compile — it references art/audio partial members that were never written — and is being rewritten from scratch against this document. Nothing below reuses the old orbital-mechanics math; it reuses the *shape* of a few techniques (trail-buffer segment placement, dt-parameterized `Step()` for deterministic verification) that worked regardless of what state they were driving.

## Pitch

A future Earth buried in its own orbital debris. Low Earth orbit has already been swept clean once, by the last generation of sweepers. You're the next one: a segmented supply snake that grows by catching what Earth launches up to you, and climbs — shell by shell — into the higher belts that were never cleaned and have only gotten worse since.

## Core movement — "only the normal changes"

No orbital mechanics. The snake's position is pinned to a fixed-radius shell around the planet:

```
position = normal * shellRadius
```

There is no separate velocity state to integrate. The snake moves at a constant speed along the shell surface; turning input (A/D) rotates the normal itself about the current heading axis, and heading is always recomputed as the tangent to that motion. Consequences:

- No periapsis, apoapsis, eccentricity, or energy to manage — those words don't appear in this design.
- The player never "falls." Leaving a shell only ever happens through the explicit Space action below, never as a physics outcome of a bad burn.
- The world math is the same *shape* of problem RIFT already solved (`Up(p)`, great-circle bearings, `SurfacePoint`) for a different genre, so the planet/camera plumbing is reusable.

**Why position-based over acceleration-based** (left to my judgment — user said "whichever's funner"): a fixed-radius walk gives an exact, single-vector answer to "where is the snake" and "where is this debris" every frame, which is what the catch/strike and self-bite rules below need to stay crisp. An acceleration model would still have to re-project onto the shell every step to stop drift, buying nothing for a second integrated state it doesn't need.

## World

- **Planet**: one sphere, reusing RIFT's `SurfacePoint`/`Up` pattern at a scale tuned for camera legibility rather than RIFT's 1200 m arena radius.
- **Shells**: concentric spherical surfaces above the planet, ascending only — LOW ORBIT → MID → HIGH → DEEP FIELD → ESCAPE. Direction is fixed by the backstory: LEO is the clean, safe starting shell; every climb moves into a shell that has never been swept.
- **Camera**: small planet, elevated top-down-ish framing so the current shell band reads as a whole ring rather than a strip. A minimap is not planned for the greybox — only added if playtesting shows the single view can't carry it.

## The snake

- A chain of segments trailing the head at fixed arc-length spacing along the shell surface — same trail-buffer indexing the old `SnakeShip.TrailPoint` used, just walking a sphere instead of sampling a Kepler ellipse.
- Segments are the only currency: gained by catching supply, spent to change shells, lost to debris strikes or self-collision.
- No fuel resource (explicitly cut) and no thrust budget — the only economy in the game is segment count.

## Debris & hazards

- Debris rides fixed-axis rotation on great circles of the current and adjacent shells — constant angular rate around one axis, deterministic by construction, easy for a headless verification step to assert against.
- Where great circles cross (nodes) and at the poles, multiple debris rings intersect — these read as a shell's dense/dangerous zones for free, without a separate density system.
- A Kessler clock slowly raises a shell's debris count/speed the longer a run stays on it, so camping the safe low shell is not a viable strategy — the field gets worse under you if you idle.

## Core verb: interception

- Catch (safe) vs strike (damaging) is decided by **approach**, not raw speed: contact where the snake's heading roughly matches the debris's local direction of travel is a catch; any other contact angle is a strike. This is the fixed-speed-track version of "low relative velocity = capture, high = strike" — once both sides move at a constant local speed, matching heading *is* low relative velocity.
- **Self-bite**: the head touching its own tail past a short buffer near the neck severs everything behind that point into loose debris left on the current shell — same rule as the old `CheckSelfCollision`, expressed in shell-arc-length instead of 3D distance.

## Space — changing shells

- A dedicated action (Space) ejects a batch of segments "downward" off the shell (they fall back toward the planet, cosmetically) and moves the head up one shell. Batch size is a player choice, up to the segments on hand — bigger batch banks a higher score multiplier on the ejection.
- Segments spent this way are gone for good; forward progress on the new shell is funded entirely by catching fresh supply there.

## Death

No fuel-out, no periapsis decay, no HP bar. Death is exactly one condition: struck by debris while holding zero segments. Segments are the buffer between the snake and dying — the same role a tail plays against a wall in a classic snake game.

## Roguelite structure (confirmed)

- Reaching a new shell offers a choice of 1 of 2–3 skills, not a fixed unlock order — every run's build is different.
- A run's wreckage (severed segments, the corpse of a failed climb) stays on the shell where it died and becomes part of that shell's hazard field for the *next* run. The world visibly accumulates the player's own failures.
- No meta/account-level stat growth between runs. Every run starts even; the only thing that carries over is the wreckage left in the world.

## Setting & tone

A future Earth that already paid once to clean LEO, now sending up the mission that keeps doing it — one shell at a time, against belts that were never swept and keep getting worse on their own clock. README/marketing language should call this a debris-clearing snake game, not an orbital-mechanics simulator; the physics is deliberately gone.

## Explicitly cut from the old draft

- Velocity Verlet integration, `Conic`/`Elements()`, `PointOn()`, vis-viva pod-insertion burns, and any energy/eccentricity-based verification checks.
- Kept as a *technique*, not as code: arc-length-indexed trail buffering for segment placement, now driven by shell-walk state instead of a Kepler state vector.
- Collision checks move from Euclidean distance to angular distance on the shell, since everything now lives on a fixed radius.

## Greybox scope — build this first

One sphere. One snake. Debris on great circles only — no per-shell belt density yet, no Kessler clock yet, no skill picks yet, no multi-shell ladder beyond a single Space up-shift to prove the transition works. Catch-from-behind is the only interaction rule implemented.

**Kill/keep criterion**: does that single loop — walk the shell, dodge or catch debris by matching heading, risk severing your own tail if you get greedy — hold up as something worth five minutes of play? If it doesn't, the shell-walk model itself is what gets revisited, not the decoration around it.

## Verification

Headless editor checks (matching the RIFT convention: `RiftVerification.cs` / `RiftCombatVerification.cs`) cover rules and correctness only: shell radius is held exactly every step, heading follows A/D turning at the documented rate, catch vs. strike is decided correctly by approach angle, self-bite severs the correct suffix of the tail, and segment count changes exactly on catch / Space / strike. Whether it's *fun* is not something a harness can answer — that needs an actual playtest, ideally from a shared WebGL build rather than a description of the rules.
