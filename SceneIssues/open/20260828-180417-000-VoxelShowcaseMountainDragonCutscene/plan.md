# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be exact-SHA standalone-player output, production-quality by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / `WorldRoadResolver` / `WorldRoadNetwork` / `EmitTerrainCorridor`: canonical route and physical road realization.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon composition policy.
- `CharacterMotor`: shared production movement/collision; this feature may fix demonstrated reusable collision defects but must not weaken collision policy for the scene.
- `StartupBakeProvenance` / `ShowcaseStartupBakeContract`: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, ridge strength 300, shared 280 permille grade / 42 dm cut-fill, and the shared road pipeline are retained. Experiments 025-027 ruled out analytic cut allowance, corridor winner/order discontinuity, and incorrect realized top-road columns. Experiment 028 isolated authoritative voxel collision rather than tree wood. Experiment 029 proved the then-terminal blocker was material 9: the solid dragon cube; the route now finishes beside that placeholder and its clearance regression remains.

Exact current-source bake/replay `33839531278` then exposed a distinct upper-approach stop: feet `(-104.590,45.600,28.000)` m, target `(-108.0,28.0)` X/Z, grounded with zero movement; intended X sweep was voxel-blocked before and after the normal step-up. Its screenshots also remain rejected for segmented mountain massing and abrupt upper-road faces.

The attempted 140-permille / 64 dm terrace policy failed deterministically, and run `33857362837` falsified the replacement 3 dm-per-resolved-point invariant because `WorldRoadResolver` removes collinear search samples. Those speculative production changes are backed out; effective layout remains the source-matched 20 dm / 280 / 42 configuration.

## CharacterMotor blocker root cause and correction
Experiment 030 exact request `da61cfb8dde34c7f6dece9eedaf09acbb2b077e1` / run `33859073259` identified every late-stall authoritative blocker cell as material 13, the road surface, exactly one voxel below the nominal feet. Earlier realized-corridor evidence already proved the road top equals its authoritative target there. `CharacterMotor.IsBlocked` was half-open only on maximum AABB faces: raw flooring of a minimum face on an exact voxel boundary could round infinitesimally downward and include the supporting voxel as body overlap.

The narrow reusable correction treats both minimum and maximum voxel collision faces with the same existing 0.1 mm boundary tolerance. It does not change route geometry, road grade/cut-fill, step height, summit placement, or ground-probe depth. The deliberate 2 cm downward ground-contact query still overlaps support normally.

Exact feature source `fbc5a35d309a99abfa2b86b188d1b4770e424c62` is covered by `MountainDragonCharacterMotorBlockerDiagnosticTests.UpperApproachRoadSupportFaceDoesNotCountAsCapsuleOverlap`: nominal support-face contact and normal raised position are free, a genuine 1 cm support penetration still collides, and the production 2 cm grounded probe still detects support. Run `33867932199` passed that regression in 21.868 s; the workflow later exited only on the known Unity Test Framework post-build cleanup lifecycle failure. Actual traversal, rather than rounded synthetic X coordinates, is the next product proof.

## Remaining gates
Merge then-current `origin/master`; run exact-SHA current-source bake + matching manifest + automatically derived module gates + the same grounded standalone SceneIssue replay. Require full base-to-summit completion and exact proximity dialogue, then inspect fresh approach/lower/mid/upper/summit screenshots against the mandatory visual bar. If visuals still show segmented mountain masses or abrupt road faces, isolate and repair only the demonstrated presentation relationship. Promote only a visually accepted payload/manifest, prove clean-checkout consumption, complete all `tasks.md`/`issue.json` criteria, move only this task `open -> closed` with fixed metadata, run final exact-head validation, then promote `fixes/agent-4 -> master` through PR + auto-merge and monitor the required gate until merged.