# Plan — 20260826-132505-873 VoxelShowcase

## Defect / acceptance
Capture note: `there is a floating mailbox`; no circles, so the whole saved pose is acceptance. The foreground object is the east-market street lamp near authored `(1530,549)` dm. Accept only when a clean replay of the saved `1928x836` pose shows its gray foot visually contacting the working-yard shoulder, with pole/lantern continuous and nearby streetscape unchanged.

## Competing hypotheses / evidence
1. **Wrong district elevation — confirmed/fixed.** Lamp placement now derives from the generated working-yard terrace rather than macro elevation.
2. **Thin reconstructed support — confirmed/fixed.** Dark pole and stone foot use `SurfaceStyles.Planar`.
3. **Terrain/foot seam — confirmed/fixed.** The foot embeds one voxel while preserving the upper lamp. `CapturedEastMarketLampKeepsPlanarSupportUnderLantern` evaluates both production catalogues and proves generated-surface overlap plus foot→pole→lantern continuity.
4. **Green replay still proves product failure — rejected twice as evidence.** Run `33107330590` restored a stale bake because WorldBuilder was absent from the cache fingerprint; feature `1ce0c43a...` fixed that. Run `33117731124` then logged a cache miss/fresh bake and passed the regression, but its generic `1600x900` Development-Build screenshot changed framing and included replay/FPS/debug overlays. Runtime verification did match the recorded camera transform/FOV.

## Current fix / blast radius
Keep lamp geometry unchanged. `tools/showcase-bake-cache.sh` now invalidates on `Assets/Game/WorldBuilder`. For clean evidence, make the existing non-development `SceneIssueCameraReplayHarness` consume `-voxel-scene-issue` directly, launch the player at the recorded dimensions, suppress the FPS HUD on that explicit replay path, require `SCENEISSUE camera pinned`, and reject undersized final frames. Normal showcase players retain their overlay and default resolution; runtime game/world behavior is unchanged.

## Remaining gates
Commit the replay-evidence correction, request exact-head PlayMode + saved-pose replay, confirm fresh/current bake semantics, green behavioral test, normal-player camera pin, native-resolution clean artifact, and direct visual contact at the target. Only then commit `verification-final.png`, complete metadata, move open→pending→closed, merge current master into `fixes/agent-1`, and push that exact head to master non-force.
