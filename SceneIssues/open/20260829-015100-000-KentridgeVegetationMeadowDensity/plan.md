# Plan

## Observed defect / acceptance
- Kentridge currently reads as nearly barren: grass is extremely sparse and does not visibly animate.
- Closure requires a built-player Kentridge meadow with at least 3,000 procedural grass blades attributable to one contiguous meadow region, visually dense at player height, plus time-separated visual evidence proving wind animation.
- Kentridge's current area policy must allow only the new procedural grass vegetation and no ambient animal kinds.

## Architecture / likely owners
- Route authoring through top-level WorldBuilder area ecology/vegetation policy: allowed kinds, density/coverage, deterministic variation, surface/exclusion constraints, and an ambient-animal allowlist/spawn-policy hook.
- Reuse the packed procedural grass path (`Assets/VoxelEngine/Rendering/Runtime/Vegetation/ProceduralGrassBatch.cs`); do not restore legacy grass sprites, hand-place thousands of coordinates, spawn grass GameObjects, or fork a Kentridge-only shader.
- If motion is absent, discriminate sparse semantic placement from broken material/shader/time/wind binding in the shared renderer before changing production code.

## Validation gates
- Focused production-path regressions for area policy, allowed-kind filtering, density, determinism, exclusions, and empty animal allowlist.
- Exact-SHA built-application Kentridge harness with no startup/runtime exceptions.
- Durable visual evidence: approach view, player-height dense meadow view, >=3,000-blade meadow diagnostic, and two or more fixed-view time-separated frames (or short sequence) showing blade motion.
- Human visual review is mandatory before pending/closed. Static screenshots, source assertions, primitive counts, or crash-free launch alone cannot satisfy closure.
- Measure blast radius and CPU/GPU/memory/world-build cost for the dense animated result against existing vegetation/rendering budgets.
