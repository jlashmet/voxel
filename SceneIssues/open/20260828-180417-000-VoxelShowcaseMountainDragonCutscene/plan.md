# Plan

## Acceptance
Built `VoxelShowcase` must show a substantial grounded mountain with a readable winding ascent, normal grounded traversal to a usable summit, the explicitly allowed cube dragon visibly supported, and proximity dialogue `Hello, I'm Mr. Dragon.` WorldBuilder/shared modules own geometry and interaction. Current `AGENTS.md` requires player-visible work to be `production-quality`; only dragon art has a lower issue-specific bar. Closure also requires a source-matched checked-in startup bake and exact built-player evidence.

## Proven state
Architecture, grounded traversal/headroom/support, castle-suppression avoidance, sparse upper-dragon bake coverage, raster fast-path equivalence, and exact-CI native bake exit are proven. Earlier run `33310677691` passed the structural final wrapper and 17/17 production replay under unchanged 240 s / 14 GiB guards.

## Latest discriminator — run 33314740587
Source `a6288a9411c5...` added dark-rock/moss/dirt material separation and revision-4 provenance. The fresh bake succeeded at the 240 s guard, reopened Unity, and logged `200 regions, 13.9 MiB`, signature `0x217FA141`.

The requested wrapper failed only because prepared-startup acceptance still expected legacy mountain material `1`; the new material-role regression itself passed. Standalone replay reached grounded waypoint 16/17, then hit its 58.0 s evidence timeout; dialogue was already visible, so gameplay/proximity did not regress.

Human review falsifies the “material monotony is the primary blocker” hypothesis. Separation improves readability but the result remains `prototype/blockout quality`: support banks repeat as giant rounded cylinders/domes, the landform reads as a pile of similar blobs, road edges are hard/extruded, and the summit is an engineered flat pad.

## Selected next fix
Correct the stale test semantically and restore evidence timing margin without changing gameplay. For art, replace the support row-of-frustums realization with fewer deterministic ridge-like tapered masses that blend into the mountain while preserving occupied support under every authored path span. Keep ground-cover as a presentation role but do not turn every full support volume into green material. Add a focused program/geometry invariant that rejects repetitive small support segmentation and retains primitive/cost bounds.

## Remaining gates
1. Implement/test the reusable ridge/support realization and evidence-only timing repair; keep coordinates, movement, collision, and 240 s / 14 GiB budgets unchanged.
2. Merge then-current master and use only existing `ci-test/fixes/agent-4` for the next exact final PlayMode + built-player discriminator.
3. Require exact rendered frames to classify `production-quality`; otherwise record concrete defects and continue.
4. Commit the final accepted generated payload + manifest as the clean-checkout startup image through a repository-sanctioned binary path, validate those exact shipped bytes, then perform `open -> pending -> closed` bookkeeping and non-force master promotion.
