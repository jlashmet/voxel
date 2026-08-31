# Experiment 005 — structural evidence residency lifecycle

## Symptom
Exact-SHA run `33343416007` was mechanically green: focused PlayMode passed, the built-player audit captured all eight structural frames, all three `CharacterMotor` traversals and required negative contracts passed, and the workflow completed successfully. Direct full-resolution review still rejected every structural frame because the intended bridge/castle/cliff/facade assemblies were absent or hidden behind terrain-only near-field output.

## Minimal discriminator
The motor/camera alignment fix made `WorldbuildingGalleryShowcase.Update()` stream from the actual evidence camera. The player log then showed the missing lifecycle step directly:

- bridge frame 1/2 moved the streamer to the remote bridge district;
- relocation back to castle emitted eviction batches through `evict#208` before frame 3;
- there was no `STRUCTURAL_PRESENTATION authored=True` / `STRUCTURAL_REFINEMENT authored=True` between bridge and castle frames;
- castle frame 4 had `pending=0` in the full-resolution HUD but contained no castle, ruling out renderer lag as the castle failure;
- the same move/evict pattern repeated before cliff and facade frames.

This proves the evidence loop was moving the production residency origin correctly but was not restoring authoritative proof voxels after each cross-district eviction. Bridge frame 1/2 additionally showed 33 then 8 pending regions in the HUD, so those views were also captured before the production near-field streamer converged.

## Selected bounded fix
Reuse the existing public `EnsureWorldbuildingGalleryStructuralRefinementBlocking()` contract rather than pinning storage or changing eviction policy. On the first frame of each proof family:

1. pin camera + production motor to the evidence pose;
2. seed the bounded line-of-sight strip;
3. yield one frame so normal scene streaming performs any required eviction at the new origin;
4. invoke the public structural ensure to restore authoritative proof content if residency invalidated it;
5. wait, fail-closed, for `PendingRegionLoads == 0` (4 s maximum) and a short render settle before capture.

The ensure runs once per proof family rather than once per screenshot, bounding cost inside the 60 s player replay. Shared storage, renderer, terrain, solver, global budgets, and `CharacterMotor` behavior remain unchanged. Implemented at `32fc898f42ed5ec6779a035842ed370d86acd6bd`.
