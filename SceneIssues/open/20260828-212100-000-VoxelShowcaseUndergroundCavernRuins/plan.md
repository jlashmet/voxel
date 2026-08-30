# Plan

## Acceptance / current evidence
`VoxelShowcase` must progress from a natural daylight mouth through a long organically shaped walkable descent into a huge dark irregular cavern, then to an aged reachable ruin framed by exactly two grounded humanoid statues. Closure requires focused exact-SHA regression, real built-player traversal/capture, direct rendered review, and bounded generation/render/light cost.

The earlier exact run `33284693031` / source `492ea820...` was functionally green but visually failed with repeated vertical ribs, planar ceiling bands, and a rectangular destination throat. That falsified movement/capture-window and cadence-only explanations.

The rounded-vault experiment replaced visible passage cylinders in full-route naturalization, doglegs, and destination circulation with deterministic rounded profiles while preserving the generic cave core, renderer, materials, camera, authoring API, floor support, lights, CharacterMotor path, and existing write/light ceilings. The vault profile is emitted through bulk radial column spans to retain the fast authoring path.

## Exact final-request result
The single canonical request used transport `2afc0626968adcb1d858bc7a21925b50225f5563` and exact feature source `263b6667f3f98ff9a8f580403e7ec95540aeebf8`. Workflow `33286541699` / job `99190592084` is green:

- focused PlayMode regression passed, including deterministic rounded-profile and worst-case rectangular-core-cover checks;
- standalone `VoxelShowcase` completed the normal production route at waypoint 38/38 with zero harness assertions;
- 35,166,289 total writes, 4,792,841 naturalization writes / 215 nodes, 3,580,112 visual-finish writes, 20 preloaded regions, 6 route / 8 total lights;
- generation remains below the unchanged 55M total / 15M naturalization / eight-light ceilings;
- compared with the prior failed visual baseline, total writes rose 1.06% and naturalization writes 8.53%;
- post-startup FPS samples: min 46.4, median 92.8, mean 109.3, with the isolated low sample coincident with a 210.67 ms streaming-admission spike;
- renderer arena telemetry: peak 18,969,600 allocated vertex slots, 28,677,632 index slots, 2,405 draw leases, zero lease failures; visible-region `drawn` peaked at 520 and was 274 near the destination. Arena counters are allocation/lease telemetry, not direct visible-triangle counts.

## Direct rendered decision — FAIL
All seven standalone-player frames from the exact run were inspected individually. The vault repair partially changed the silhouette: the old flat cylinder caps are gone and the ceilings now form stepped/concentric rounded crowns. It does **not** satisfy the capture:

1. Underground frames 1–5 remain a repetitive masonry/tiled tube with strong vertical ribbing and terraced crowns, not geological cavern walls.
2. The terminal frame is a huge but strongly rectangular chamber with flat vertical walls and a straight paved/railed approach, not a huge irregular natural cavern.
3. The aged ruin and exactly two grounded humanoid statues are not clearly readable as the required flanking destination composition in any frame.
4. Localized darkness/route lights, collision/traversal, determinism, and cost are healthy, but they cannot substitute for the failed visual criteria.

## Remaining product work / constraint
Primitive topology was a contributor, but the exact rendered discriminator shows the dominant remaining ownership is deeper: trace and replace the layer leaving masonry/tiled natural-cave surfaces and the rectangular destination host/approach, then ensure the ruin plus exactly two grounded statues are unmistakably composed from the normal route. Do not use a camera/capture workaround.

The user explicitly prohibited extra CI transports. The one canonical final transport has now been consumed by run `33286541699`; therefore no second transport will be created under the current instruction. The assignment remains `open`, no pending/closed metadata will be written, and no master promotion will occur from this failed visual gate.
