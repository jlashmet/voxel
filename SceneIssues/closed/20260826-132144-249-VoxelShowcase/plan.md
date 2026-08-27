# Plan — 20260826-132144-249-VoxelShowcase

## Goal
Remove the blue cast from high-detail terrain at the detailed/far handoff in `VoxelShowcase`; do not hide it by adding matching near fog to far terrain.

## Root cause and selected fix
`SmoothSurface.shader` blended its final lit colour toward `SkyColour(viewDirection)` from 60–300 m (about 40% at the far end). That explicit camera-distance tint caused the close/detail terrain to read blue. `FarTerrain.shader` has a separate intentional long-range `_AerialDistance` haze.

Production fix `bc24592304d8c0bdb92ee7647adc5536586e6450` removes only the detailed surface's 60–300 m sky blend and restores/preserves far terrain's native long-range haze. Normal-oriented `SkyColour(normal)` ambient remains.

## Verification
- Source/compile guard `DetailedTerrainDoesNotBlendSkyColourByCameraDistance`: `ci/single-test=success` on request `a80bf59385ae1601ad63696698b6d3bf0b5c1bfa`, run `33014528240`.
- Production-shader GPU behavioral regression `DetailedSurfaceColourDoesNotShiftTowardSkyWithDistance` at source `bcd4d034f7429c9f9e627e08b9e1d4836e142cc0`: `ci/single-test=success` on request `c0e640c65459498e46a05cc443de9dae3f433d0f`, run `33018680576`.
- Fresh saved-camera standalone-player replay from the same source: request `4a8d0af0edab8955bbec91ddccc11c81ec74154d`, run `33018852581`, job `98343889283`, artifact `9625790344`, all successful. Final frame shows green/material-coloured detailed terrain with no reported blue cast. Repository preview is `verification-final.png`; full-frame SHA-256 `3f3cf67080095f1d80ab0446b4eb281e1b29da428bca94b30840ade83d112aca`.

## Remaining gate
Do **not** move this capture to `pending` yet. Current master `025e88ef6e2d097143607c3018184ddc99cb747c` → feature diff still contains unrelated prior capture `20260825-192751-413-VoxelShowcase` plus its scheduler/test changes. `SceneIssues/README.md` forbids pending promotion with unrelated capture/code in the feature-only diff, and the current assignment forbids altering that other capture.

The GitHub connector also cannot decode the original repository PNG directly, so direct pixel inspection of `screenshot-001.png` remains a coordinator/local-worktree evidence check. The saved issue note, camera metadata, and successful exact-pose replay are preserved.

Current state: product fix and replay verified; capture remains `open` pending coordinator branch/evidence reconciliation.
