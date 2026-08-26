# Experiment 07 — saved replay rules out far terrain and localizes the live handoff

## Hypothesis

The ring-0 far-terrain boundary traced in experiment 06 is the representation producing the striped low-resolution patches in the saved SceneIssue view.

## What was performed

Reconciled the exact standalone-player evidence from mapped replay run `32892693260` with the current `VoxelFarTerrain` and voxel-ring diagnostics, then followed the exploratory far-boundary regression request from source `d221c10bb9f8d98a7b9507d6f20f345f3162f5e1` through CI request commit `5a6d289120782e77982a3bb616ce7746b6226a65` (run `32929889298`).

The settled standalone replay reports:

- `FAR hole=365.9m inner=409.6m streamed=409.6m coverage=True`
- step 1: `0-57.6m`
- step 2: `57.6-115.2m`
- step 4: `115.2-172.8m`
- step 8: `172.8-409.6m`
- `missingVisible=0`

The saved camera is at 48.95 m elevation and the three marked patches are foreground/mid-ground terrain around the first voxel handoff, not hundreds of metres away at the analytic far-field boundary. The far renderer therefore has a large open hole around every marked region in the settled frame.

The exploratory far-boundary CI request did not execute NUnit: Unity crashed during Burst import with a native bus error (exit 138). That run is infrastructure-inconclusive and is not counted as a red regression.

## Result

Falsified for this capture. The code-level ring-0 boundary overlap described in experiment 06 exists as a separate topology concern, but it cannot be the source of the three marked patches in this saved replay because no far-terrain triangles are submitted inside the 365.9 m published hole.

The replay instead localizes the visible transition to the voxel LOD bands themselves. `VoxelShowcase` applies `m_DetailBandScale = 0.6`, moving the configured 96 m fine-band edge inward to 57.6 m. This is exactly where the settled view starts admitting step-2 terrain. The scene is therefore intentionally presenting coarse terrain much closer than the scheduler's full-resolution 96 m layout.

## Disposition

Discard the exploratory `FarTerrainBoundaryOwnershipTests` from this feature branch; it is unrelated to the assigned capture and never produced behavioral evidence. Add a focused scene-presentation regression that keeps the flagship showcase's fine band at its configured 96 m extent, prove it red against the current `0.6` scene setting, then restore the full band and replay the original saved view.
