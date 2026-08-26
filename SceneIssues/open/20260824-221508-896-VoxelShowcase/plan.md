# Plan — 20260824-221508-896 VoxelShowcase

## Defect / acceptance
All four annotations were inspected directly. The central mark showed a missing/stair-stepped strip through the market-stall support/plinth; the other three were top-edge gaps along the lower blue/silver border. Accept only when the exact captured pose shows continuous geometry at every circle with no new nearby seam.

## Competing hypotheses
1. **Stale bake/replay content.** Falsified by fresh-bake exact-pose replay; the gap family persisted.
2. **Positive-edge/empty-neighbour ownership alone.** Partly causal but insufficient; earlier ownership fixes reduced/moved defects without clearing all four regions.
3. **Presentation material mistaken for authoritative occupancy in mixed continuous extraction.** Selected. Density sampling can legitimately carry a solid render material at an authoritative-AIR, negative-density sample, while `FacetedMaskJob` had treated that presentation material as occupancy.

## Discriminator / fix
Behavioral regression runs `TransvoxelDensityJob -> FacetedMaskJob` with occupied Planar backing, authoritative air above, and adjacent Rounded solid. It first proves the air sample carries solid presentation material while density remains negative, then requires the Planar cap to remain exposed.

Fix: carry authoritative centre occupancy in renderer-transient surface flag bit 2, consume that bit for exact faceted exposure, and strip it before vertex publication.

Blast radius/cost: CPU exact continuous lattice only; no storage/gameplay mutation, allocations, new arrays, or extra Storage reads. Snapshot-only faceted extraction, GPU extraction, and mip rings are unchanged.

## Verification
Production/test head: `7a8df39059ac10a844207ce75577d9389026a69f`.
Final tested source: `2a6d289f1c4886c80bcf9e83c7337df9a436b97b`.
Final CI request: `26e67e0221a6466d925ccf6f5c8793ef78af7234`, run `33019033756`, attempt 2: **success**. Focused PlayMode regression and 30-second exact-pose replay both passed. Fresh replay clears all four original circles; `verification-final.png` records that result.

Remaining bookkeeping: move this capture `open -> pending` in a separate commit. `resolvedUtc` stays empty until human closure.
