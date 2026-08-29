# Experiment 011 — focused water validation scene

## Hypothesis
The final visual gate can avoid the full `VoxelShowcase` world while still proving the issue behavior by rendering the same production `VoxelFarTerrain` startup fallback against authored water in a tiny built-player scene.

## Action
Added `Assets/Scenes/WaterStartupFallbackValidation.unity` with a validation-only runtime bootstrap. It seeds the same known 51.2 m authored-water region used by `StartupFallbackPreservesAuthoredWaterHeightAndMaterial`, creates production `VoxelFarTerrain` with a small 30–180 m clipmap, waits through its first `LateUpdate`, then freezes independent copies of the production-generated synchronous/fallback meshes. The camera looks directly into that authored-water region. `tools/showcase-player-capture.sh` maps only the exact regression filter to this scene for a 20 s real-player review.

## Why freeze
The emergency fallback is intentionally transient; in a tiny unloaded scene the authoritative rings can publish before CI's first screenshot. Freezing the first-frame meshes is validation presentation only: production `VoxelFarTerrain` generated the mesh geometry, authored lowered height, and water albedo before copying. Normal scenes never instantiate this bootstrap.

## Result
Exact-source CI run `33279274759` completed `success` on request SHA `5aba178e7588d7638b6961741f5ff8381cddbeda`, whose direct parent is feature SHA `5a86122c4ec91b1e6b52afa3b035cd59486a4f7f`. NUnit selected exactly `StartupFallbackPreservesAuthoredWaterHeightAndMaterial` and passed 1/1 in 0.058 s. The same run built `WaterStartupFallbackValidation.app`, launched it for 20 s, captured frames at 2 s and 12 s, and logged `WATER_FALLBACK_VALIDATION ready: production startup meshes frozen with authored water semantics.` Both frames visibly show the blue authored-water surface; no runtime error/exception/assertion failure appears in the player log.

## Blast radius / cost
No additional production rendering behavior changed. The validation scene allocates a two-region in-memory store, 256 authored water samples, and a 30–180 m far clipmap only while explicitly loaded. Capture routing changes only one exact test filter. Production fallback cost remains startup-only and bounded below 3,000 vertices, with no steady-state sampling after authoritative publication.

## Verdict
Confirmed. The generic startup proxy was the common owner across all five marked regions, and the semantic fallback preserves authored water in both the focused behavioral regression and built-player presentation. Final gate is green; proceed to bookkeeping and master integration.
