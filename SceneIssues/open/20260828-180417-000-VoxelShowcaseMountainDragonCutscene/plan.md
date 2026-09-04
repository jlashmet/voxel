# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain with a readable shared-road ascent, normal grounded traversal from accessible exterior terrain to a usable summit, a visibly supported allowed cube dragon, and exact proximity dialogue `Hello, I'm Mr. Dragon.` Final proof must be exact-SHA standalone-player output, production-quality by `AGENTS.md`, source-matched to the checked-in startup bake, with unchanged 240 s / 14 GiB guards.

## Ownership
- `MountainLandformSpec` / `MountainLandformSurface`: deterministic semantic landform.
- `WorldRoadIntent` / `WorldRoadResolver` / `WorldRoadNetwork` / `EmitTerrainCorridor`: canonical route and physical road realization.
- `ShowcaseMountainDragonLayout`: scene-only mountain/road/dragon composition policy.
- `CharacterMotor`: shared production movement/collision; do not weaken it for this scene.
- `StartupBakeProvenance` / `ShowcaseStartupBakeContract`: exact source-to-payload binding.

## Proven state
Natural-landform-first composition, ridge strength 300, shared 280 permille grade / 42 dm cut-fill, and the shared road pipeline are retained. Experiments 025-027 ruled out analytic cut allowance, corridor winner/order discontinuity, and incorrect realized top-road columns. Experiment 028 isolated authoritative voxel collision rather than tree wood. Experiment 029 proved the then-terminal blocker was material 9: the solid dragon cube; the route now finishes beside that placeholder and its clearance regression remains.

Exact current-source bake/replay `33839531278` then exposed a distinct upper-approach stop: feet `(-104.590,45.600,28.000)` m, target `(-108.0,28.0)` X/Z, grounded with zero movement; intended X sweep was voxel-blocked before and after the normal step-up. Its screenshots also remain rejected for segmented mountain massing and abrupt upper-road faces.

The attempted 140-permille / 64 dm terrace policy failed deterministically (`6b0f48e...`: 70 dm cut/fill required). Run `33857362837` falsified the replacement 3 dm-per-resolved-point invariant: `WorldRoadResolver` removes collinear search samples, so semantic route vertices can differ by 19 dm across a legal 280-permille segment. Those speculative production changes are backed out; effective layout is restored to the source-matched 20 dm / 280 / 42 configuration.

## Next discriminator
Experiment 030 replays the exact `33839531278` stall through production `CharacterMotor.FootMin` / `FootMax` / `IsBlocked` and serializes occupied voxel coordinates/materials for current, negative-X, raised, and raised-negative-X capsule AABBs. Hypotheses: (1) physical road/terrain realization owns the blocker; (2) another Showcase-composed solid owns it. Fix only the proven owner and add an independent regression.

## Remaining gates
After the blocker fix: merge then-current `origin/master`; exact-SHA current-source bake + manifest + automatic module gates + grounded standalone replay; inspect fresh approach/lower/mid/upper/summit screenshots to production-quality; promote only the accepted payload/manifest; complete all `tasks.md`/`issue.json` criteria; move only this task `open -> closed` with fixed metadata; final exact-head validation; PR `fixes/agent-4 -> master`, enable auto-merge, and monitor required `affected` gate until merged and closed on `origin/master`.
