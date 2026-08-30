# Plan

## Observed behavior / acceptance

`WaterRenderingShowcase` has no capture frames, so the ticket specification, supplied Stylized Water Shader package, and `WaterfallReference.shader` define the target. The resumed branch already implements the core shared architecture: game composition installs semantic still/river/waterfall presentation rows; engine extraction classifies water from presentation data instead of hard-coded game IDs; per-vertex material identity is preserved; and the renderer owns one reusable `Hidden/VoxelEngine/WaterSurface` material. The shared shader now contains shallow/deep response, foam, animated detail/refraction/highlights, directional river flow, and dedicated waterfall turbulence/aeration/lip/base/mist cues.

Acceptance still requires proving this path survives a real player build, is globally selected by normal scenes, preserves gameplay semantics, provides the required showcase/portability cases, meets visual quality in motion, and stays within cost budgets.

## Hypotheses / discriminator

1. **One canonical production water seam serves all standard scenes.** Supported by render-path tracing; falsified if scene assets/builders bind an alternate normal water renderer. Next: inspect `WaterRenderingShowcase`, `VoxelShowcase`, Kentridge/second consumer, and renderer-data serialization.
2. **Presentation refactoring is gameplay-neutral.** Supported by existing spreading-water regression because authoritative simulation remains keyed to gameplay material semantics while presentation classification is separate. Next: trace swimming/buoyancy/collision/discovery/streaming/edit/diagnostic consumers for accidental dependency on presentation rows.
3. **The current shader is player-retained and initialized before extraction.** Unproven. Falsified if active renderer data lacks the shader reference or catalogue installation can occur after surface scheduling. Next: trace active URP assets and normal player bootstrap ordering.

## Selected fix / current result

Keep the existing shared path; do not add a second material system, scene shader, or hand-authored production plane. Add only the smallest missing retention/bootstrap/test/showcase capability proven by the remaining discriminators. Current source head before this plan refresh: `7177cebaca9773ee6b57a1fd36b59e983daed40f`.

## Blast radius / cost

Scope remains material presentation, water extraction/rendering, game composition, focused regressions, and this ticket’s showcase/evidence. Six 32-row `Vector4` profile tables are bounded at 3,072 bytes and installed once. Preserve existing water culling/streaming and one shared render material; quantify draw count, transparent overdraw, shader work/variants, and waterfall turbulence/foam/mist without weakening device budgets.

## Remaining gates

Retention/bootstrap and gameplay-consumer trace; scene/showcase/portability validation; extraction regression; build/shader reliability and cost evidence; exact-SHA built-player visual review for showcase plus existing scenes; one final exact-SHA targeted-CI request; pending/closed metadata; merge latest master and non-force promote exact head.
