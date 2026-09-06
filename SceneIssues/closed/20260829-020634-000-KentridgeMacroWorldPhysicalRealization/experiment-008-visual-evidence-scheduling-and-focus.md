# Experiment 008 — visual evidence scheduling and settlement focus

## Exact source / transport
- Feature source: `34bcba11c160c36f110390c875df5d77e260d49d`
- CI request commit: `6255eaab11769b31bcdb1d979d7fd9989749709b`
- Workflow run: `33261744161`
- Evidence artifact: `9717484869`

## Result
The exact-source focused PlayMode acceptance and built-player workflow are green. The production physical-macro summary remains bounded and complete: 6 regions, 6 settlements, 16 generated blockout buildings, 20 hard routes, 833 route tiles, 5 constrained routes, 1108 route-solve steps, maximum road rise 2 voxels per 30 dm step, and 46-voxel water depth. The built-player driver also exercised the real `CharacterMotor` after restoring `Time.timeScale=1`, with approximately 6.81 m of local movement and 8.20 m of macro-road movement. No startup/runtime assertion failure was observed.

The artifact is still not closure-quality visual evidence. Within the fixed 60-second replay it completed macro-road, Moordell, Rossdam, Rossdam Lake, and Fairy Village captures, but did not finish Orc Village, southern ridge/pass, or macro-network overview. The generic-settlement survey also still centers the circulation/resident origin, leaving generated plots around the center less readable than the real physical geometry warrants.

## Hypothesis discrimination
- **Production macro-world realization is missing or too expensive.** Rejected by the focused production-path acceptance, physical summary, completed remote captures, and successful normal-time CharacterMotor traversal.
- **The remaining failure is validation-driver scheduling/focus.** Selected. The opening wait is unscaled wall-clock work, so 12x game time does not recover most of that wall-clock budget. After opening, the evidence driver additionally spends a fixed 2.4 seconds per target after published-near-coverage has already become its real readiness predicate, and several targets require separate validation-only teleports. Generic-town camera focus also uses the circulation center instead of a representative generated building.
- **Change planner/worldgen/story/gameplay semantics to make evidence easier.** Rejected. Nothing in this run supports a production change.

## Next isolated experiment
Change only `KentridgeMacroWorldEvidenceDriver`:
1. Derive generic-settlement capture focus from actual generated settlement/building geometry rather than the empty circulation center.
2. Keep published-near-surface coverage as the hard renderer-readiness gate, but replace the redundant 2.4-second fixed dwell with only a minimal camera-settle floor.
3. Compress validation-only movement timing/distance where safe while retaining real `CharacterMotor` motion at normal `Time.timeScale=1` with collision/streaming active.
4. Reuse the southern-ridge resident streaming location for the final macro-network survey where possible instead of paying another remote teleport/convergence cycle.
5. Do not modify the macro planner, route solver, catalogue, streamer, story, normal gameplay timing, or physical-world semantics.

## Success gate
One fresh exact-SHA request on the existing `ci-test/fixes/agent-6` transport must finish all eight durable PNGs (macro-road plus seven survey targets) inside the fixed playback window. Every capture must be coverage-ready and visually readable; all focused production assertions, normal-time CharacterMotor traversal, runtime cleanliness, and bounded cost telemetry must remain green. Only then may acceptance and closure metadata be checked.