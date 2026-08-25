# Experiment 001 — Baseline floating-tower replay

## Hypothesis

If the reported object is a deterministic generated placement rather than a transient streaming artifact, then a fresh-baked VoxelShowcase replay at the saved SceneIssue camera should reproduce the unsupported roofed tower after the scene settles.

## What performed + source commit

- Baseline source tip: `64b371df659263501ce64431159ecef5204d71a7`.
- Added a temporary capture-specific replay workflow on `fixes/agent-1`; no production/test source was changed.
- GitHub Actions run `32886508286` fresh-baked `Assets/Scenes/VoxelShowcase.unity`, replayed `SceneIssues/open/20260825-033249-397-VoxelShowcase/issue.json` through the standalone-player SceneIssue path at the original `1364x836` resolution, and ran the fixed view for 90 seconds.
- Downloaded artifact `sceneissue-033249-baseline-32886508286` (artifact id `9577984446`, digest `sha256:4d7f30dc4e8747c5ffd608fb44cbde70e891412403aae17e239b38fa34690d32`).
- Inspected the untouched bundled `screenshot-001.png` and all eight replay frames from `showcase-000-t014.5s-stationary.png` through `showcase-007-t084.5s-stationary.png`.

## Result

The original report is clear and valid: a compact masonry tower with a steep dark roof is suspended in mid-air near the center of the saved view, with its underside visibly exposed and a large empty gap to the terrain below.

The current persistent feature branch does **not** reproduce that tower after a fresh bake. The first replay frame is still loading, but by 24.5 seconds the scene has converged; every settled frame through 84.5 seconds shows the same terrain and right-edge town structures while the centrally floating roofed tower is absent. The disappearance is stable, not a one-frame streaming transition.

Therefore this attempt did not reproduce the defect on the current branch. It also means the issue cannot be closed merely because the tower is gone: `fixes/agent-1` contains earlier unmerged SceneIssue work, so one of those stacked changes may have incidentally removed the bad authoring/placement path. The root cause and a regression still need to be identified.

## What learned

- The source screenshot is a settled frame (~222.4 seconds after load), so the reported floating structure was not early-load noise.
- The object is visually distinct from the larger crenellated/spired town structures visible at the right edge. It is a small standalone roofed tower/house-like structure with exposed underside geometry.
- The saved camera is near Kentridge's civic area, but the image alone does not prove this is the church tower; the fresh branch replay retaining other town structures while removing only this object makes composition-path differences between `master` and the current stacked branch a higher-value lead.

## Next

Compare the VoxelShowcase catalogue/authoring composition on current `master` versus the stacked `fixes/agent-1` source, identify which prior change removed the floating object, and trace that object to its exact definition/placement. Add a focused regression for the resulting deterministic support/placement invariant before treating the incidental disappearance as a fix. Remove the temporary baseline workflow before terminal completion.
