# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. Current `AGENTS.md` requires player-visible work to be `production-quality`; only final dragon art has a lower issue-specific bar. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, traversal/headroom/support, castle-suppression avoidance, sparse upper-dragon bake coverage, raster fast-path equivalence, and the exact-CI native bake exit are proven. Run `33310677691` on source `2100df40287a...` passed fresh bake, Unity reopen, structural acceptance, 17/17 production `AutoWalk -> CharacterMotor.Step` replay, and dialogue capture under unchanged 240 s / 14 GiB guards.

Re-review under master `dfbc43b086b6...` classified that run's mountain/path `prototype/blockout quality`: dominant bright masonry, repeated primitive/frustum masses, retaining-wall-like cuts, weak ground/material integration. The cube remains acceptable by explicit issue text.

## Selected visual fix
Hypothesis 1: material monotony is the primary blocker; hypothesis 2: silhouette/support proportions remain procedural even after separation. Test hypothesis 1 first because it changes presentation semantics without adding geometry or cost.

Implemented semantic mountain roles (`rock`, `groundCover`, `path`, `placeholder`) in reusable WorldBuilder composition. VoxelShowcase now selects dark masonry rock, moss ground-cover/support banks, dirt path, and unchanged red cube. Revision 4 invalidates old single-material startup bakes. `MountainDragonVisualFinalAcceptanceTests` proves the material adapter changes only additive shoulder/support frustum material fields and preserves primitive order/count plus the entire established structural acceptance.

## Next discriminator
Merge current master, then use the existing `ci-test/fixes/agent-4` transport for one exact final PlayMode request targeting `MountainDragonVisualFinalAcceptanceTests.ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay` with built-player VoxelShowcase replay. Human-review approach/base/middle/upper/summit/dialogue frames. If not `production-quality`, record the remaining silhouette/support defects and continue; do not close.

## Remaining gates
1. Final exact PlayMode + built-player visual run and cost check.
2. Record the newly accepted source-matched payload/manifest and commit them as the clean-checkout startup image through an existing repository-sanctioned binary path; provenance validation stays strict.
3. Validate clean-checkout shipped bytes, then complete `open -> pending -> closed` metadata/lifecycle, merge then-current master, and non-force push the exact feature head to master.
