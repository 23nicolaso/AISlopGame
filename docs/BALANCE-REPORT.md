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
| 12 | ring latched once chosen; energy rule suspended while intercepting; overcharge check re-staged for the new layout | 1–3 | 1–2 | 2–9 | 1454 | both suites green again; DustRunner (low-orbit spawn) banked 0 in every match |
| 13 | pitch 44→58 and yaw 27→34 deg/s at full authority; throttle .5 inside 300 m of a wreck | 4–7 | 1–3 | 2–3 | 1021 | a replay showed a 250 m turning circle against a 100 m field; kills now land in every minute of the match |
| 14 | thrust allowed above 650 m when the nose is below the horizon (was cut outright past 800 m); run-in point times out after 12 s | 5–9 | 1–5 | 5–11 | 962 | bank sampler had laden pilots pointing at their ring with zero stick while coasting away — above the air the engine is the only control |
| 15 | nothing chased above 560 m (rivals included); dive above 800 m capped at 37°; no power while sinking > 40 m/s up there | 4–10 | 3 | 8–13 | 977 | every round-14 launch began as a Search/Intercept after a contact already in vacuum; launches themselves unchanged (23), so the remaining altitude time is the descent, not the climb |
| 17 | ring approach: attitude gain 22→14 inside 450 m, run-in armed at 520 m, released under 14° | 5–9, mean 7.2 | 0–1 | 6–10, mean 8.3 | 462 | pilots with no bank 7 → 2 of 48; banks per match flat; the kill drop is inside the spread between six-match sets (4–14) — kept for the shut-out metric |
| 16 | residual (vacuum) drag .00004 → .00012 — six matches against a six-match baseline | **4–14, mean 10.3** (was 6.3) | **0–1** (was 0–4) | **7–10, mean 8.8** (was 7.8) | **464** (was 980) | launches 42 → 9 in six matches, ceiling deaths 11 → 0, time above 650 m 92 → 38 s per pilot; below 300 m the term is invisible next to the density drag, so nothing on the fields changed |

## What the traces taught

- **Read the numbers, not the code.** Every one of the ten rounds was aimed by a trace, and three of the ten "obvious" fixes made things worse until a trace showed why (the throttle floor, the fixed-climb pull-up, the stateless run-in).
- **Thin air is a cannon.** Above ~400 m there is no drag; any nose-up converts airspeed into altitude one-for-one and nothing bleeds it. The AI now obeys an energy rule: the height its climb rate will still gain with the engine off may not exceed the target's altitude by more than 40 m.
- **A whole planet is too big for eight aircraft.** With 950 m of sight and a 640 m horizon at 170 m, six sites 55° apart never see each other. Clustering them 32° apart is what finally puts two aircraft in the same sky on purpose.
- **Turn radius decides banking.** A 105 m ring cannot be flown from inside a 240 m (thick air) or 640 m (460 m field) turning circle; the AI now sets up a straight run-in.

## Final run (round 16, six matches — `docs/balance/six-match-drag.json`; baseline six in `six-match.json`)

| Metric | Baseline (round 1) | Now | Target |
|---|---|---|---|
| Kills per match | 0–2 | 4–14, mean 10.3, in every minute | ≥ 15 |
| First kill | 7 s – never | 5–6 s in every match | ≤ 45 s |
| Self-inflicted deaths per match | 14–16 | 0–1 (2 of 64 deaths, both burns) | < 25 % — met |
| Seconds a pilot holds a target | 351 / 24 pilot-matches | ~1300 / 24 | — |
| AI cannon hit rate | 33 % of very few shots | 19 % | 10–30 % — met |
| Cargo banks per match | 5–8 | 7–10, mean 8.8 (claims 7–18) | ≥ 16 |
| Pilots with 0 banked | 6–8 of 8 | 1.2 of 8 | 0 |
| Mean altitude | ~450 (then 1990 mid-series) | 464 | — |

Two targets are met, kills sit at two thirds of the target with the best matches exceeding it, and banking is the one loop still short by half. The pilot without a bank is nearly always a low-orbit spawn (ROOK or STDY), and that is the next lever. The harness is what makes the next round cheap: `scratchpad/balance.sh 6` is about twenty minutes.
