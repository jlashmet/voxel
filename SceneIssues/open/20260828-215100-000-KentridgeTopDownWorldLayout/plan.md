# Plan — Kentridge top-down world layout

## Evidence / discriminator
- This architecture feature has `captures: []`: **0 recorded poses/marked regions**. Acceptance therefore requires explicit built-player top-down and normal traversal evidence.
- Existing WorldBuilder already produced local Kentridge/Hightown geometry; “missing local town generation” is falsified. Imported transition/progression evidence instead showed the missing owner was a reusable macro topology + physical route layer. Hard topology must outrank inferred compass hints.
- Source `8b6ea69c` passed focused PlayMode + real-player build/run in CI `33223023746`: one requested test passed; player build/run exited 0; streaming converged (`missingVisible=0`) and reached ~60–67 fps by the end.
- Artifact inspection falsified “green player run is enough visual proof”: both saved frames were still the `Loading Kentridge…` cover because complete near-surface coverage arrived at ~28 s, after the last 23.1 s screenshot in a 30 s run. Thus acceptance (9)/(10) remains open despite green workflow status.

## Selected fix
- Keep the implemented reusable WorldBuilder macro graph/planner and shared voxel backend: 21 source-backed nodes, 20 hard routes, typed verified/inferred provenance, reserved envelopes, corridor widths, deterministic placement/reachability, and neutral terrain-grounded route/marker output.
- Preserve one-shot selection so unrelated Kentridge catalogue consumers do not inherit macro-world cost.
- Implemented at `cd993026`: shared capture harness detects only **capture-less** `KentridgePlayableSlice` SceneIssues, auto-advances unattended dialogue, enables the existing real `CharacterMotor` walk after 40 s, and emits Kentridge evidence. Recorded-pose SceneIssues keep immutable replay behavior.

## Regression / remaining gates
`KentridgeTopDownWorldLayoutTests.SourceBackedWorldLayoutIsDeterministicPhysicallyRealizedAndRejectsSeveredHardRoute` proves deterministic non-overlap/provenance/reachability, exact Hightown anchor alignment, continuous Kentridge route tiles, production `FeatureCatalogue` realization, and severed-hard-route rejection.

Remaining: final exact-SHA CI with a 60 s player replay; inspect post-load top-down and walking frames; store durable verification; complete every `tasks.md` checkbox; pending/closed bookkeeping; merge current master and publish non-force.

## Blast radius / cost
Planner cost is O(21 nodes + 20 routes). Physical plan is 803 route tiles + 21 neutral markers / 41 definitions, selected only for playable Kentridge startup; no eager extra chunks or device-budget changes. Harness change affects only capture-less Kentridge SceneIssue validation, not game runtime or captured-pose replay semantics.
