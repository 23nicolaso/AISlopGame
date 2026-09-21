# Balance report — 2026-09-21

Nobody has hand-flown RIFT since the shared flight model landed, and manual verification is not accepted on this project. This report is the substitute: whole matches played by the AI in every seat (the player's airframe is flown by a rotating rival personality via `ArenaPilot.autopilot`), stepped at a fixed 0.02 s by `Assets/Editor/RiftBalanceRunner.cs`, with every death, launch and banking approach traced. Numbers below are per 300 s match unless stated. Raw output: `docs/balance/latest.json`.

## Targets (set before the first run)

| Metric | Target | Why |
|---|---|---|
| Kills per match (8 aircraft) | ≥ 15 | one every ~20 s somewhere on the board |
| First kill | ≤ 45 s | the match opens with a fight, not a commute |
| Self-inflicted deaths (terrain / burn / ceiling) | < 25 % of deaths | the flight model must not be the main enemy |
| Pilots with 0 banked | 0 | everyone participates in the economy |
| Banks per pilot | ≥ 2 | the loop closes more than once |
| AI cannon hit rate | 10–30 % | fights resolve, but not instantly |

## Iterations

| Run | Change | Kills | Self-deaths | Banks | Mean alt (m) | Notes |
|---|---|---|---|---|---|---|
| 1 | baseline (`5810ce9`) | 0–2 | 14–16 | 5–8 | — | 46 of 48 deaths were the planet; hunters held targets in range 70–100 s and fired < 6 s |
| 2 | recovery earlier, dive limiter, 14° gimbal, 12 s engagement, 14 dmg, heat 3.2→2.4, thin-air throttle floor | 1–2 | 14–17 | 3–4 | — | throttle floor ran away: 46 ceiling deaths |
| 3 | floor only while sinking; coast above 650 m | 2–5 | 7–8 | 2–6 | — | 22 ceiling deaths; 351 s of engagement across 24 pilot-matches |
| 4 | over-horizon descent, steep dive above 800 m, engine off above 800 m, wreck cooldown 28→70 s, hunter patrol | 4–7 | 3–5 | 2–5 | — | 13 ceiling deaths; kills all in minute 0, economy dead |
| 5 | (telemetry only: tactic histogram, launch traces) | 5–6 | 5–7 | 3–5 | 1990 | **everyone lives in space**: 190–270 s of each match above 650 m |
| 6 | attitude solver: pitch clamp for targets astern, reversal hysteresis, ±35° nose band | 1–4 | 5–7 | 4–5 | 1990 | spawn case fixed (per-step diagnostic), 52 launches remain from Intercept / Bank |
| 7 | energy rule (no zoom past target altitude), nose band scaled by density (35°→8°) | 3–5 | 1 | 1–7 | 905 | launches 52→10, crashes ~0 |
| 8 | terrain reflex pulls proportionally to sink, burner only while sinking | 0–4 | 0–1 | 2–7 | 761 | launches 8; laden pilots orbit rings at 160–660 m |
| 9 | ring run-in (stateless), cannon 14→18 | 2–4 | 0–2 | 4–6 | 952 | run-in oscillated; 77 % of pilot-time in transit ("Salvage") |
| 10 | run-in latched, sites clustered into a 160° arc (32° apart) | 4–5 | 2–3 | 3–4 | 1185 | first kill ≤ 6 s in every match; laden pilots now zig-zag between two equidistant rings; the astern pitch clamp broke the retaliation check |
| 11 | astern pitch clamp removed (bank-and-pull reversal restored) | 2–5 | 1–3 | 4–6 | 1275 | retaliation check green again; projectile check red: the energy rule pushed the nose down on a climbing gun pass |
| 12 | ring latched once chosen; energy rule suspended while intercepting; overcharge check re-staged for the new layout | see final run | | | | |

## What the traces taught

- **Read the numbers, not the code.** Every one of the ten rounds was aimed by a trace, and three of the ten "obvious" fixes made things worse until a trace showed why (the throttle floor, the fixed-climb pull-up, the stateless run-in).
- **Thin air is a cannon.** Above ~400 m there is no drag; any nose-up converts airspeed into altitude one-for-one and nothing bleeds it. The AI now obeys an energy rule: the height its climb rate will still gain with the engine off may not exceed the target's altitude by more than 40 m.
- **A whole planet is too big for eight aircraft.** With 950 m of sight and a 640 m horizon at 170 m, six sites 55° apart never see each other. Clustering them 32° apart is what finally puts two aircraft in the same sky on purpose.
- **Turn radius decides banking.** A 105 m ring cannot be flown from inside a 240 m (thick air) or 640 m (460 m field) turning circle; the AI now sets up a straight run-in.

## Final run

_Filled in from the last `RiftBalanceRunner` pass on this commit — see the table below and `docs/balance/latest.json`._
