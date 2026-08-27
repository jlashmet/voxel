# Experiment 004 — stale Showcase bake cache

## Hypothesis
The saved-camera replay is not exercising the current terrace source because the Showcase startup-bake cache does not fingerprint WorldBuilder inputs.

## Action and source
Compared exact replay run `33024802125` (request `873253059f853bef44de98a6b63a38c65d8a2ea5`) with run `33029263119` (request `ea80df8b3281a43b3893c2768769f57f00690147`) after `KentridgeTerraceSurfaceCorrection.Program.cs` materially changed. Both runs were green and emitted real-player evidence. The final frames were visually identical apart from runtime overlay noise; a pixel comparison changed only 0.0534% of pixels with mean RGB delta 0.022. Inspected `tools/showcase-bake-cache.sh`: its fingerprint includes composition/engine inputs but omits `Assets/Game/WorldBuilder`, where both active terrace fixes live.

## Result
Confirmed. The replay cache can restore a bake generated before WorldBuilder changes, so the two green replays do not discriminate the current product fix. Their visual result is stale evidence, not a product failure or success.

## Verdict
Supported. Add `Assets/Game/WorldBuilder` to the startup-bake fingerprint. This intentionally favors extra cache misses over stale visual verification and covers all WorldBuilder code that can feed the Showcase bake.

## Next gate
Run the current product source through exact saved-camera CI again. The new fingerprint must miss/store a fresh bake, and the replay must visibly differ where the terrace source changed. Only then accept or reject the ramp/material fix.
