# Experiment 005 — startup far-field fallback overdraw

## Runtime evidence
Final targeted run `33232756610` used request SHA `c3242d463941a860da7fd5767721d63f9f63460d` for feature source `3d79796adc05ce83ef2dd8692606d4ad447f6c8d`. The showcase startup bake succeeded, the focused far-material PlayMode test passed 1/1, and the real `VoxelShowcase` player built and ran the requested 30-second replay with exit code 0 before the workflow was cancelled during final status publication.

Direct inspection of the uploaded built-player frames at about 15.9 s and 25.9 s rejects the previous material-only hypothesis as sufficient: a single viewport-scale flat teal/green triangle still crosses the entire marked receiving-water view. The material change made part of that malformed proxy teal; it did not remove the geometry.

The player log provides the timing discriminator. Visible near chunks were still missing at both screenshots (`missingVisible=72` around 15.9 s, `22` around 24.5 s, `13` around 27.4 s) and reached zero only at roughly the 30-second cutoff. This overlaps the original issue capture time (~22 s), so it is acceptance-relevant startup behavior rather than an out-of-window transient.

## Source discriminator
`VoxelFarTerrain.BuildStartupFallback` creates the outermost emergency fallback as one flat 8 km square at `ShowcaseWorld.BaseHeightVoxels`, using only four vertices and two triangles. Ring zero is already synchronously height-sampled before this fallback is created, but the fallback square overlaps ring zero completely. The castle receiving river is authored below the analytic/base terrain height, so depth testing lets the flat fallback sit above the authoritative repaired river and produces exactly the giant screen-space triangle seen in the player.

This rules out the narrow 6.4 m coarse shoreline blend as the primary viewport-scale artifact: the observed triangle is the startup fallback topology itself.

## Selected correction
Retain the zero-sampling horizon fallback, but make it an annulus outside the synchronously sampled ring-zero footprint. Use one ring-zero cell of overlap at the inner boundary to avoid a crack under snap offset. The fallback remains flat only where no authoritative critical-ring mesh is already available.

Behavioral regression: `CastleLowerRiverWaterRepairPlayModeTests.StartupFallbackLeavesSynchronousCriticalFootprintUncovered` creates the production `VoxelFarTerrain` at the captured camera position, advances the first startup frame, proves the runtime fallback does not cover the camera/critical footprint, proves a 1 km horizon probe remains covered, and bounds the fallback to eight triangles.

Blast radius: generic `VoxelFarTerrain` startup only; authoritative clipmap sampling, near/far handoff, castle water authoring, runtime destruction, and steady-state ring topology are unchanged.

Cost: startup fallback changes from 4 vertices / 2 triangles to 8 vertices / 8 triangles, created once and discarded when the outermost authoritative ring lands. No additional terrain/storage sampling, jobs, per-frame queries, or persistent region data are introduced.
