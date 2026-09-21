# ORBIT SNAKE — design (v2, replaces the Kepler draft)

Status: **awaiting sign-off.** Sections marked *decided* are the user's calls; sections marked *proposed* are Claude's reading and need a yes/no before any code. Nothing in the Kepler scaffold (`9304457`) survives except the tail-trail buffer.

## 1. Pitch

A snake game on the shell of a planet. The world is a future Earth wrapped in space junk; low orbit has already been pulled down and is clean. You are a junk hauler: intercept debris from behind so it latches on as a segment, avoid hitting anything head-on, and when your train is long enough throw it all downward to burn up in the atmosphere — the recoil lifts you one shell higher, into dirtier sky, where you pick a new skill and do it again.

One control (turn), one button (level change), one verb (interception). The score is junk de-orbited.

## 2. Decided (user's words)

| Call | Source |
| --- | --- |
| No orbital mechanics. Play is 2D at a constant altitude; only the normal changes. | "去掉开普勒，做 2d 同高度，只改法线" |
| 2D plus one button that changes level. | "2d + 一个按钮 levelchange" |
| Eccentricity is not part of the game. | "偏心率和我想的不一样" |
| Setting: a future world covered in space junk. | "太空垃圾覆盖未来世界" |
| Backstory: LEO's junk has already been pulled down. | "LEO 的垃圾都被拉下去了" |
| Snake; Earth launches supplies; accumulate segments then change level; each level unlocks a skill. | original pitch, not retracted |

## 3. World and movement — *proposed*

- The planet is RIFT's sphere (radius 1200, centre (0,−1200,0)) reused as-is. Each level is a **shell**: a fixed altitude above it. Shell altitudes rise with level (e.g. 120, 170, 230, 300, 380 m).
- The ship's state is a unit **normal** `n` (where it is on the shell) and a unit **tangent** `t` (heading), nothing else. Position is `centre + n·(R+altitude)`. Speed is constant per shell.
- Each step: `n ← normalize(n + t·(v·dt/R_shell))`, then `t` is re-projected onto the new tangent plane. A/D (or mouse X) rotates `t` about `n` at a fixed turn rate. There is no throttle, no pitch, no fuel.
- Because movement is a rotation of the normal, a straight run is a great circle and a held turn is a small circle: the snake naturally draws the same geometry the junk moves on.
- Camera: RIFT's chase camera, pulled higher and further back so the horizon and roughly a quarter of the shell are in frame. Minimap only if the greybox shows it is needed.

**Alternative reading of "只改法线"**: the ship has a fixed tangential speed and the only input is normal (turning) acceleration. This is the same thing expressed as forces instead of state; the state form above is easier to verify. Confirm which was meant.

## 4. Junk — *proposed*

- Every piece of junk moves on a **great circle** of its shell: an orbit plane (axis) and a phase that advances at a fixed angular rate, prograde or retrograde. It never leaves its shell.
- Where great circles cross each other and each other's poles is where junk naturally piles up. Those crossings are the dangerous, rich places; the design does not need to hand-place them.
- Junk is both the food and the hazard. Which one is decided by **relative velocity at contact**:
  - `|v_rel| < capture threshold` → it latches onto the tail as a segment (you came up behind it, matched its motion).
  - `|v_rel| ≥ threshold` → **strike**: you lose the two hindmost segments; with none left, the hull goes and the run ends.
- Classic snake rule: the head touching its own tail past the third segment **severs** it there. The loose segments become junk on the current shell, on the great circle the snake was drawing when it laid them, and come round again.

## 5. Level change — *proposed*

- Space, when the train has at least the shell's quota (5, 7, 9, 11…): the whole train is **ejected downward**. It streaks into the atmosphere below (visible burn on the planet), banks score, and the recoil moves the ship up one shell.
- Bigger train → higher multiplier at ejection. Holding on past the quota is the risk/reward knob: more score, longer tail to bite, more mass to lose on a strike.
- Arriving on a new shell presents a **skill pick** (see §7) and starts that shell's Kessler clock.

## 6. Kessler clock — *proposed*

Junk on the current shell multiplies over time: pieces that cross each other spawn fragments. Early on a shell is sparse and forgiving; stay too long and it clogs. This is the only pressure against camping the safe shell, and it is diegetic — it is what the setting says is already happening.

## 7. Earth launches and skills — *proposed*

- "Earth launches supplies" becomes the **skill delivery**: a pod climbs from the surface to your shell on arrival at a new level (and rarely mid-level). Catching it opens a choice of one skill out of two or three. Roguelite, not a fixed unlock order.
- Candidate skills (keep to what the greybox proves it needs): wider capture window (magnet), a brief brake to widen the from-behind approach, tail whip (fire the last segment forward to destroy junk), one free strike (armour), segments also capture on contact (sweep), a short dash.
- No meta-progression between runs. What persists is **wreckage**: where a run died, that snake's train is junk on that shell next run. Roguelike over soulslike: nothing to retrieve, only more to dodge.

## 8. Run structure — *proposed*

- One life. Score = junk de-orbited, multiplied by train size at ejection, plus a per-shell bonus. Highest shell reached is shown next to the score.
- Death: struck with zero segments. The atmosphere is not a hazard (you never leave the shell) and there is no fuel to run out of.
- Direction: **ascend** from clean LEO into dirtier shells. The backstory says LEO was cleaned, so the run starts where the game is easiest and the goal is upward.

## 9. Greybox slice — the first thing built

One sphere, one snake, junk on great circles, the from-behind capture rule, strike, self-bite, Space ejection to the next shell. No art beyond flat primitives, no skills, no Kessler clock, three shells.

**Kill criterion:** does *intercept from behind* hold up as the only action for five minutes of play? If it does not, nothing in §5–§8 will rescue it and the concept changes again before more is built.

## 10. Verification (headless, deterministic)

Rules and correctness only; feel needs a human. Each check drives `Step(dt)` at a fixed dt:

1. The normal stays unit and the altitude stays on the shell to 1e-3 over 60 s of turning.
2. A held turn closes a small circle; a straight run returns to its start after one great circle.
3. Junk stays on its orbit plane to 1e-3 and keeps its angular rate.
4. Contact below the capture threshold adds a segment; above it sheds two; at zero segments it ends the run.
5. Segment spacing along the trail stays within 10 % through a held turn.
6. Self-bite past segment three severs, and the loose segments become junk on the current shell.
7. Space below quota does nothing; at quota it ejects the train, raises the shell by one, and scores with the multiplier.
8. Skill gating: a locked skill's input is inert.
9. `paused` blocks `Step`.
10. Determinism: two runs from the same seed produce the same score at t = 120 s.

Screenshot runner: three framed shots (launch, mid-train, ejection) for the README.

## 11. Discarded

Kepler free flight with player-managed ellipses (eccentricity is not the game). Concentric lanes with phasing and Hohmann transfers (user: "我们理解上有差别"). From the current code: Verlet integration, `Elements()`, `PointOn()`, vis-viva pod insertion, the energy/Kepler checks. Kept: the `SnakeShip` trail buffer; collisions become angular distance on the shell.

## 12. Sign-off list

| # | Question | Recommendation |
| --- | --- | --- |
| 1 | Movement = state (normal + tangent, A/D rotates heading) rather than normal acceleration? | Yes, state form. |
| 2 | Ascend from clean LEO? | Yes. |
| 3 | Junk is the food (capture by relative velocity); Earth launches deliver skills, not segments? | Yes. |
| 4 | Roguelite skill pick, wreckage persists, no meta upgrades? | Yes. |
| 5 | Drop fuel entirely? | Yes. |
| 6 | Ejection = de-orbit burn that both scores and lifts you? | Yes. |
| 7 | Greybox first, judged on the five-minute kill criterion? | Yes. |
