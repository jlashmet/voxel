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
Natural-landform-first composition, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, and reusable provenance remain. Experiments 025-029 ruled out cut allowance, corridor winner/order, realized top-road mismatch, vegetation, and the old terminal cube overlap.

Experiment 030/run `33859073259` found material-13 road support one voxel below nominal feet. The narrow half-open minimum-face correction is retained; focused production collision regression passed in run `33867932199` before a later Unity Test Framework cleanup failure.

Current master was merged through PR #268; post-merge feature head `9ae65b51...` was behind master by zero at validation time. Exact post-merge run `33868687506` is **failed**, not accepted. Its requested current-source bake test passed in 167.245 s and exported 15,692,523 bytes, signature `7554A9C4`, SHA-256 `874c8fd12fdc99fc894c4d91669656cc45ec9dc4fb4228b7f4184daede3b2fb0`, but the workflow later failed and the real player timed out at `resolved-89`, feet about `(-104.589,45.600,28.000)` m, grounded and stationary. Therefore that payload is diagnostic only.

Fresh built-player screenshots remain `unacceptable`: the brown road is visible, but giant gray/white mountain rock/snow faces dominate approach/lower/mid/upper captures; the ascent reads as a trench/wall relationship, not a production-quality carved road.

## Current visual root cause / discriminator
Exact request `c5eab6baf370eece6f473f0603c1e8d209fcc610` for production `3b4d9b4d2f2f524e651eec2e1a8fc6194504192b`, run `33926166526`, passed the standalone SceneIssue replay but failed repository-derived module validation in two Mountain Dragon presentation regressions. This is a product failure, not infrastructure.

The first failure is composition policy: Mountain Dragon still authored `asymmetryXPermille=90` / `asymmetryZPermille=-70`, which shifts every shared core band from the semantic origin (`-1112` expected, `-1091` actual). With ridges/roughness disabled this small offset is the only source of the rejected offset-lobe silhouette, so the Mountain Dragon policy must keep its broad massif concentric while independent consumers retain reusable asymmetry support.

The second failure is the shared broad-massif radial profile. Serialized point 82 is `(-960,366,260)` dm. Its 25 dm inward clearance sample crosses the current innermost band, whose final 14% of radial run carries about 24.5% of total rise (roughly 1.06 vertical dm per radial dm for this spec). Combined with ordinary resolver cut, the raw adjacent mountain rises 39 dm above the road where production acceptance allows 30 dm. After multiple materially different road/geometry changes reproduced the same slab/wall symptom, this is the isolated common cause: the reusable four-band massif envelope accelerates too sharply in its last two bands. The narrow correction redistributes the same total radial run and total rise across four continuous, progressively steeper bands (`run 250/500/750/1000`, `rise 220/460/720/1000`) without changing road grade, cut/fill, summit radius, mountain height, primitive authority, or acceptance tolerance.

## Remaining gates
Synchronize then-current master; run exact current feature head through only `ci-test/fixes/agent-4` requesting `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with the same SceneIssue replay; require current-source bake + manifest, repository-derived module gates, and standalone replay from that checkout. Human-review all fresh production screenshots for one coherent mountain, carved/graded path, grounded base-to-summit traversal, supported dragon and exact dialogue. Only after visual acceptance may the candidate payload/manifest become the checked-in startup payload. Then prove clean-checkout consumption, complete `tasks.md`/`issue.json`, move only this task `open -> closed`, refresh/merge current master as required, and promote only through PR + auto-merge.
