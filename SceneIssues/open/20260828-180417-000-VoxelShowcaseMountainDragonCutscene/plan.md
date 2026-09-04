# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be green exact-SHA standalone-player output, `production-quality` by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / resolver / `EmitTerrainCorridor`: canonical road truth.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon policy.
- `CharacterMotor`: shared collision/movement; fix only reusable demonstrated defects.
- startup-bake provenance: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, ridge strength 300, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, and reusable provenance remain. Experiments 025-029 ruled out cut allowance, corridor winner/order, realized top-road mismatch, vegetation, and the old terminal cube overlap.

Experiment 030/run `33859073259` found material-13 road support one voxel below nominal feet. The narrow half-open minimum-face correction is retained; focused production collision regression passed in run `33867932199` before a later Unity Test Framework cleanup failure.

Current master was merged through PR #268; post-merge feature head `9ae65b51...` was behind master by zero at validation time. Exact post-merge run `33868687506` is **failed**, not accepted. Its requested current-source bake test passed in 167.245 s and exported 15,692,523 bytes, signature `7554A9C4`, SHA-256 `874c8fd12fdc99fc894c4d91669656cc45ec9dc4fb4228b7f4184daede3b2fb0`, but the workflow later failed and the real player timed out at `resolved-89`, feet about `(-104.589,45.600,28.000)` m, grounded and stationary. Therefore that payload is diagnostic only.

Fresh built-player screenshots are `unacceptable`: the brown road is visible, but giant gray/white mountain rock/snow faces dominate approach/lower/mid/upper captures; the ascent reads as a trench/wall relationship, not a production-quality carved road.

## Current hypotheses / discriminator
1. **Traversal:** the 280-permille road plus voxel/crown variation creates a capsule-footprint rise that exceeds the 0.3 m step even though centreline grade is legal.
2. **Traversal alternative:** a different authoritative voxel intersects the exact raised movement AABB; the old replay discriminator is insufficient because it still uses raw minimum-face quantization.

Exact request `33874459381` on source `4e1de8ec...` uses production `CharacterMotor.IsBlocked`, serializes every occupied raised-negative-X cell plus the complete footprint surface-height range, and makes no product change. Do not change traversal policy before its result.

For visuals, first determine whether the wall faces are the canonical mountain cut relationship or unrelated feature overlap; only then change road/landform composition.

## Remaining gates
Resolve traversal and mandatory visual defects; add/update the required Showcase module-local validation scene for changed player-visible motor/composition behavior; merge then-current master; obtain green exact-SHA current-source bake + derived module gates + full grounded replay + exact dialogue; human-accept all production screenshots; promote the exact accepted payload/manifest and make normal bake emit its manifest; prove clean-checkout consumption; complete `tasks.md`/`issue.json`; move only this task `open -> closed`; final exact-head validation; PR `fixes/agent-4 -> master` with auto-merge and monitor until merged.
