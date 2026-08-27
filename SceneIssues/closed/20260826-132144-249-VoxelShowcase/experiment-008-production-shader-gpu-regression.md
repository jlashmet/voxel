# Experiment 008 — production-shader GPU regression

## Hypothesis
A focused behavioral regression can reproduce the defect mechanism by executing the production `SmoothSurface.shader` with identical material inputs at near and handoff-range distances. With the fix, colour should remain stable; the removed 60–300 m sky blend would make the far sample measurably bluer.

## Action
The first PlayMode fixture at feature SHA `b6e2de6d4bdb54a65e9d37eac4e813fdcfab65b0` rendered the live Showcase camera into a small `RenderTexture`. Exact CI request `bbe1627eb85fdd0f180823e5500b1b8bdcba671e`, run `33016292855`, job `98335259302`, failed because that editor render path produced zero qualifying terrain pixels. The standalone replay frame from the same fix visibly contains material-coloured terrain, so the fixture did not reproduce the production presentation path.

Replaced it at feature SHA `bcd4d034f7429c9f9e627e08b9e1d4836e142cc0` with a GPU fixture that executes the actual `SmoothSurface.shader`, its structured vertex/index/draw-metadata contract, and controlled green material/blue-sky inputs at 20 m and 220 m.

## Result
**PASS.** Exact CI request `c0e640c65459498e46a05cc443de9dae3f433d0f`, run `33018680576`, job `98343307582`, completed successfully with `ci/single-test=success` for `DetailedSurfaceColourDoesNotShiftTowardSkyWithDistance`.

## Verdict
The corrected rendering implementation has behavioral GPU coverage through the production shader. The failed camera fixture was a test-fixture failure, not evidence that the detailed-terrain blue tint remained.

## Next
Complete a fresh exact-camera standalone-player replay and preserve `verification-final.png` before any pending bookkeeping.
