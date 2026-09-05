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
Natural-landform-first composition, 280 permille grade / 42 dm cut-fill, shared road lowering, terminal route beside the cube, and reusable provenance remain. Experiments 025-029 ruled out cut allowance, corridor winner/order, realized top-road mismatch, vegetation, and the old terminal cube overlap for the earlier terminal traversal stall; they do not substitute for the newer lower/mid-ascent visual overhang discriminator after the layered-massif redesign.

Experiment 030/run `33859073259` found material-13 road support one voxel below nominal feet. The narrow half-open minimum-face correction is retained; focused production collision regression passed in run `33867932199` before a later Unity Test Framework cleanup failure.

Current master was merged through PR #268; post-merge feature head `9ae65b51...` was behind master by zero at validation time. Exact post-merge run `33868687506` is **failed**, not accepted. Its requested current-source bake test passed in 167.245 s and exported 15,692,523 bytes, signature `7554A9C4`, SHA-256 `874c8fd12fdc99fc894c4d91669656cc45ec9dc4fb4228b7f4184daede3b2fb0`, but the workflow later failed and the real player timed out at `resolved-89`, feet about `(-104.589,45.600,28.000)` m, grounded and stationary. Therefore that payload is diagnostic only.

Fresh built-player screenshots remain `unacceptable`: the brown road is visible, but giant gray/white mountain rock/snow faces dominate approach/lower/mid/upper captures; the ascent reads as a trench/wall relationship, not a production-quality carved road.

## Current visual root cause / discriminator
Exact request `c5eab6baf370eece6f473f0603c1e8d209fcc610` for production `3b4d9b4d2f2f524e651eec2e1a8fc6194504192b`, run `33926166526`, passed the standalone SceneIssue replay but failed repository-derived module validation in two Mountain Dragon presentation regressions. This is a product failure, not infrastructure.

The first failure was composition policy: Mountain Dragon still authored `asymmetryXPermille=90` / `asymmetryZPermille=-70`, which shifted every shared core band from the semantic origin. The feature now keeps this broad massif concentric (`0/0`) while independent consumers retain reusable asymmetry support.

The second failure was the shared broad-massif radial profile. Serialized point 82 is `(-960,366,260)` dm. Its 25 dm inward clearance sample crossed the previous innermost band, whose final 14% of radial run carried about 24.5% of total rise. The shared four-band envelope has now been redistributed across the same total run/rise (`run 250/500/750/1000`, `rise 220/460/720/1000`) while preserving exact seams and increasing inward slope.

Exact request `66096709ef3a1e4b5ed1c44038b52b9cdae00f56` for production `3637070b11884fb19ac705fbf2483ff4f4700ae8`, run `33928639380`, proved the requested current-source bake test green in 143.158 s and standalone player launch/replay green, but it is **not closure evidence**. The required Mountain Dragon module-local player failed only because its 16 s scenario expired immediately after production VoxelShowcase completed its ~15.1 s 102.6M-voxel startup; the probe still required residency plus a 4 s presentation wait before emitting `MOUNTAIN_DRAGON_SHOWCASE_VALIDATION ready:`. The validation scenario is therefore extended to 32 s, with capture evidence delayed to 20 s, while retaining the exact required marker and all forbidden-error assertions.

Human review of that same run still rejects the production visuals. Castle and farmhouse footprints are far from the Mountain Dragon entrance, and the shared mountain compiler shows climate only repaints already-filled surface voxels, so the large white/gray overhang is the Mountain Dragon landform itself, not unrelated geometry or snow occupancy. The canonical `TerrainCorridorRasteriser` is also proven destructive: it explicitly clears existing solid voxels from the graded road target through `desiredY + clearAbove`. The current road profile, however, permits 42 dm of cut while the generic corridor clears only 24 dm above the road. Any actual cut deeper than 24 dm can therefore leave mountain voxels overhead, producing exactly the visually rejected tunnel/overhang even though traversal remains physically possible.

A focused production regression now checks every resolved Mountain Dragon road point against that invariant: the authored mountain surface may not sit more than 24 dm above the resolved road centreline. The next exact-SHA discriminator must run `MountainDragonRoadPresentationTests.ResolvedSpiralNeverCutsDeeperThanItsOpenSkyClearance`. Do not weaken the 280-permille grade, 42 dm maximum cut/fill policy, 24 dm corridor clearance, or visual acceptance to make it pass; if it fails, change only the landform/route composition needed to keep the actual resolved cuts inside the existing open-sky envelope.

## Remaining gates
Run the focused open-sky discriminator on the exact current feature head through only `ci-test/fixes/agent-4`; if it fails, correct the demonstrated Mountain Dragon geometry/route relationship without weakening shared limits. Then synchronize then-current master, refresh route diagnostics/evidence from the final authoritative route, and run exact integrated feature head requesting `ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` with the same SceneIssue replay. Require current-source bake + manifest, repository-derived module gates, and a full base-to-summit standalone replay from that checkout. Human-review all fresh production screenshots for one coherent mountain, open carved/graded path with no trench/tunnel/causeway artifacts, grounded traversal, supported dragon and exact dialogue. Only after visual acceptance may the candidate payload/manifest become the checked-in startup payload. Then prove normal editor manifest emission and clean-checkout consumption, complete `tasks.md`/`issue.json`, move only this task `open -> closed`, refresh/merge current master as required, and promote only through PR + auto-merge.
