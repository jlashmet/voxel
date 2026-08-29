# Plan — Kentridge top-down world layout

## Evidence / discriminator
- This architecture feature has `captures: []`: **0 recorded poses/marked regions**, so acceptance required explicit built-player top-down and normal traversal evidence.
- Existing WorldBuilder already produced local Kentridge/Hightown geometry; “missing local town generation” was falsified. Imported transition/progression evidence instead showed the missing owner was a reusable macro topology + physical route layer. Hard topology must outrank inferred compass hints.
- Initial source `8b6ea69c` passed focused test + player build/run, but artifact inspection showed both 30 s frames still under `Loading Kentridge…`; workflow green alone did not satisfy geography evidence.
- Final source `9e532f52` passed exact-SHA CI run `33224752568`: exactly one requested production-path PlayMode test passed; exact Kentridge player built and ran 60 s at 1600x900 with exit 0 and five screenshots.

## Selected fix / final evidence
- Reusable WorldBuilder macro graph/planner + shared voxel backend: 21 source-backed nodes, 20 hard routes, typed verified/inferred provenance, reserved envelopes, corridor widths, deterministic placement/reachability, and neutral terrain-grounded route/marker output.
- One-shot selection prevents unrelated Kentridge catalogue consumers from inheriting macro-world cost.
- Capture-less Kentridge SceneIssue harness validation auto-advances unattended dialogue and drives the existing real `CharacterMotor` after 40 s; recorded-pose SceneIssues are unchanged.
- `showcase-002-t033.4s-stationary.png` is a fully rendered post-load frame with the production source-backed top-down layout overlay. `showcase-003/004` are walking frames; the 53.4 s frame is normal eye-level generated Kentridge/outskirts with a grounded travel path and overlay still visible. Final streaming recovered to `missingVisible=0`; player log contains no error/exception/fatal/crash line.
- Durable details are in `verification-kentridge-topdown-world.txt`.

## Regression
`KentridgeTopDownWorldLayoutTests.SourceBackedWorldLayoutIsDeterministicPhysicallyRealizedAndRejectsSeveredHardRoute` proves deterministic non-overlap/provenance/reachability, exact Hightown anchor alignment, continuous Kentridge route tiles, production `FeatureCatalogue` realization, and severed-hard-route rejection.

## Blast radius / cost
Planner is O(21 nodes + 20 routes). Physical plan is 803 route tiles + 21 neutral markers / 41 definitions, selected only for playable Kentridge startup; no eager extra chunks or device-budget changes. Harness change affects only capture-less Kentridge SceneIssue validation.

Remaining gates are bookkeeping/publication only: pending metadata + move, close metadata + move, merge current master, non-force publish, final ref/path verification.
