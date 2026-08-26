# Plan — 20260824-221508-896 VoxelShowcase

## Defect / acceptance
All four annotations were inspected directly. The large central mark shows a pale-blue missing/stair-stepped strip through the market-stall support/plinth. The other three marks are separate gaps along the same lower blue/silver border's top edge. Accept when a fresh exact-pose replay shows continuous backing/border geometry at every circle with no new nearby seam.

## Competing hypotheses
1. **Stale bake/replay content.** Falsified: experiment 003 used a fresh bake and exact pose with no bootstrap; the gap family remained, although locations reduced/moved.
2. **Empty-neighbour curved boundary or face-write ownership only.** Partly causal but insufficient: experiments 002–003 and the positive-edge regression improved the geometry without clearing all marked regions.
3. **Mixed continuous faceted occupancy uses presentation material as occupancy.** Selected. `TransvoxelTopologyJob` already documents that density sampling may carry Stone/solid presentation identity at an authoritative-air, negative-density sample. `FacetedMaskJob` used `Materials[]` to suppress exact Planar faces, so a nearby Rounded/Smooth primitive could erase backing/border caps.

## Proven discriminator / fix
The behavioral regression now runs `TransvoxelDensityJob -> FacetedMaskJob`: Planar backing is occupied, the neighbour above is authoritative air, and a Rounded solid beside it makes the air sample carry a solid presentation material while density stays negative. Exact face ownership must still emit the Planar cap.

Fix: carry authoritative centre occupancy in unused renderer-transient surface flag bit 2 (storage persists only flag bits 0–1); `FacetedMaskJob` consumes that bit instead of `Materials[]`. Both faceted and topology packers strip it before vertex publication.

Blast radius/cost: CPU exact continuous lattice only; no storage/gameplay mutation, no new arrays/allocations, no extra Storage reads, one bit write per density sample plus bit tests/masks. Snapshot-only faceted chunks, GPU extraction, and mip rings remain unchanged.

Implementation head before evidence-only commits: `7a8df39059ac10a844207ce75577d9389026a69f`.

## Remaining gates
One final exact-SHA PlayMode CI request on `ci-test/fixes/agent-1`, with the focused regression plus this SceneIssue replay. Inspect all four circles, commit `verification-final.png`, complete pending metadata, then move `open -> pending` in a separate bookkeeping commit. Do not push master or open the closure PR.
