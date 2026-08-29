# Plan — Kentridge vegetation meadow density

## Scope / acceptance
Work only `20260829-015100-000-KentridgeVegetationMeadowDensity`. Kentridge must use reusable WorldBuilder ecology policy, allow only procedural Grass with no ambient animals, produce one connected meadow with >=3,000 blades, respect exclusions, and show visible stationary wind in the exact built `KentridgePlayableSlice`.

## Material results
- Reusable `RegionEcologyPolicy` now authors allowed vegetation/tree/animal kinds, density, spacing, deterministic seed salt, slope/route clearance, and exclusion classes; Kentridge selects Grass only with empty tree/animal allowlists.
- Shared packed grass uses deterministic 5–15 blade expansion and engine-managed shader time. The production-shader framebuffer repro proves the shared shader visibly deforms.
- Three clock attempts were falsified by built-player evidence. Exposed-top-face grounding is the correct surface contract but was not the visibility cause.
- Experiment 005 found the causal defect: at 0.4 m spacing, `MaxUndergrowth=12000` was exhausted by ~Z=143.2 m, behind the required camera near Z=150 looking +Z. Kentridge spacing is now 0.8 m while density 0.96 and the 12k cap remain unchanged.
- Final production-camera regression reports 11,322 grass roots in front of the real camera, 3,664 root clusters in its frustum, and 116.02 m forward coverage.

## Final validation
Source `ec92c3002a6b75ca86de7819f4175c5390a1ca2b`; request `d71730e46c2e12bc81e8c6e58cb87c07525904e3`; workflow `33249542767`; `ci/single-test=success`. Built player reports 113,490 blades total, 57,752 in the primary connected meadow, 16 packed chunks, and zero excluded-surface leakage. Direct inspection of stationary 39.8/49.8/59.8 s frames plainly shows dense individual procedural blades and changing silhouettes; grass-band pixel deltas are 42.89% and 44.08% between successive frames with sky/dialogue excluded.

## Blast radius / cost
Causal production change is Kentridge authoring only (0.4→0.8 m spacing); shared 12k semantic-instance cap, density, blade expansion, exclusions, and packed rendering topology are unchanged. Player build: 157 MB, 36.270 s. Wrapper peak RSS: 6,136 MB. Ordinary captured play after warmup: ~60–73 FPS before the held stationary phase. Separate CPU/GPU-ms are not emitted and are not inferred.

## Remaining gates
Write final metadata, complete every `tasks.md` checkbox, move only this assignment `open→pending→closed`, set `resolvedUtc`, merge current `origin/master`, and non-force publish the exact feature head to `origin/master`.
