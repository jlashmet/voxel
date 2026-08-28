# Plan — VoxelShowcase civic south-west dirt/grass seam

## Observed / acceptance
Exact request `c11d015e…` passed one PlayMode test and a fresh 45-second saved-camera replay, and the player converged to `missingVisible=0`; direct inspection still shows a hard rectangular green tongue in the upper circle while the lower circle is clean. Reprojecting the immutable camera with Unity convention (+Z forward, +X right, top-down screen Y) puts the upper marked envelope at approximately `X=91.0..93.8m, Z=28.6..30.4m`. The later `Z≈11.6m/20.9m` interpretation used the wrong camera/screen orientation. Acceptance: both original circles are visually clean with no rectangular/notched Dirt/grass boundary.

## Competing hypotheses / evidence
1. **Stale bake or streaming/LOD:** falsified. WorldBuilder inputs were freshly baked and the replay reached full residency.
2. **Road shoulder quantization:** incomplete. Granular road shoulders cleaned the lower mark but the upper tongue persisted.
3. **Civic west-edge profiling:** falsified. `c6a4a89c…` profiled that west edge and its fresh replay retained the same upper tongue.
4. **Civic south edge + late civic-west court:** supported. The civic south shoulder reaches outer `Z=31.2m`; its old whole-width ramp reused one centreline terrain sample, flattening the marked west corner. The precedence-85 `civic-west-block-court` spans `X=92.8..108.2m, Z=25.4..29.8m`, so its Fill overlaps the marked envelope and can re-stamp a rectangular shelf after the terrace repair.

## Selected fix / regression
Rebuild only the first `9.6m × 7.2m` civic south-west shoulder as eight `1.2m` strips, each meeting a local `TerrainQuery` outer-edge sample. Make only `civic-west-block-court` surface-only so it keeps material ownership without owning height; all other courts retain Fill behavior. Keep the obsolete upper material repaint absent.

`SceneIssue20260826132234356CivicSouthWestShoulderFollowsLocalTerrainProfile` builds `KentridgeCombinedVoxelCatalogue`, verifies all eight ramp outer elevations, requires PaintSurface/no Fill at the marked court overlap, preserves civic paving, and keeps primitive budgets at civic `18` / court `2`.

## Blast radius / cost
Geometry changes only the observed civic corner; the court mode change is limited to the one overlapping civic-west court. The repair adds at most 16 civic primitives within its `18` cap; the court remains one emitted primitive within its existing `2` cap. Sampling is catalogue-build-only with no per-frame work.

## Gate
Merge current `origin/master` before the final request. Use only `ci-test/fixes/agent-8`. The exact request SHA must execute the one behavioral test in under five minutes, then a fresh 45-second saved-camera replay must clear the upper mark while keeping the lower mark clean.
