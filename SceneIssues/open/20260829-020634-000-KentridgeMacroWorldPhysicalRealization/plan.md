# Plan

## Observed behavior / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the source-backed Kentridge macro graph to produce deterministic physical settlements, continuous terrain-aware hard routes, reusable regional constraints, a substantial lake/ridge with route response, real CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent; canonical `SceneIssues/README.md` governs.

## Current hypotheses and results
1. **Macro generation/streaming is wholly missing.** Rejected: production acceptance has repeatedly produced 20 hard routes and 16 generic buildings; routes and geography render in the built player.
2. **Duplicate Rossdam water ownership caused the clipped water sheet.** Confirmed and fixed: standalone callers retain generic water, while Kentridge combined composition gives Rossdam water only to the carved-basin catalogue.
3. **One terrain sample buried generic buildings.** Confirmed and fixed in catalogue authoring: each padded footprint now uses a bounded 5x5 relief sample; foundation spans sampled relief and shell/roof are offset above the sampled high point.
4. **The new all-building exclusion regression would expose any remaining semantic overlap.** Confirmed. Exact request `a3f2d6d6652abac2dcf9061f9dda51b9e6ecb52b`, run `33279138597`, failed specifically because `orc-village building 3` overlaps `southern-ridge`. The nested production acceptance passed first: 20 routes, 16 buildings, 5 constrained routes, max rise 2 voxels.
5. **That failure was Rossdam or infrastructure.** Rejected. Full NUnit evidence proves a real Orc/ridge product conflict.
6. **The remaining visual problem is only camera framing.** Rejected again by full-resolution artifact `9722783657`: Fairy/Orc settlement cameras are close enough that a present 13x10 m near-side blockout would dominate the frame, yet no shells render. The next exact CI must therefore also prove all 16 final-storage probes before visual closure is considered.

## Selected remediation / next discriminator
- Keep the source-backed topology and authored route solutions unchanged.
- Bound the modern Southern Ridge blockout so it still intersects the direct South Fighting Area -> Orc/Logan corridors, preserving `GoAround` and designated-pass behavior, while leaving the adjacent Orc settlement footprint buildable. Fixed-seed geometry places the ridge near `(3167,-697)`; changing half-depth `270 -> 120 dm` clears Orc plot 3 while retaining a substantial ~84 m x 24 m ridge and 11 m elevation.
- Reuse the existing all-building storage target. It must pass the Orc exclusion, both explicit geography-route assertions, water ownership, grounding/program checks, and timber/roof material probes for all 16 generic buildings.
- If storage is green but built-player settlements remain absent, treat that as a streaming/render-production defect; do not close on storage alone and do not mask it with camera changes.

## Blast radius / cost
- This remediation changes only one Kentridge macro-region extent/source note; no graph nodes, settlement coordinates, route topology, streaming radius, CharacterMotor, renderer budget, definition count, or placement count changes.
- Existing terrain-relief work remains bounded to 25 samples x 16 buildings = 400 deterministic catalogue-build queries.
- Prior green cost baseline: player ~73 s, peak RSS ~5.6 GB, zero swap growth. Re-measure final player CPU/GPU/frame/memory/streaming telemetry.

## Remaining gate
Self-review branch scope and current master, then issue only the designated exact-SHA target on `ci-test/fixes/agent-6`. Reject closure unless focused storage/behavioral validation is green **and** full-resolution built `KentridgePlayableSlice` evidence visibly shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village, a clean substantial lake, readable ridge/pass response, connected roads without large holes, and real CharacterMotor traversal. Only then complete pending metadata, move open -> pending -> closed, set `status=fixed`/`resolvedUtc`, merge current master, and non-force promote the exact feature head.
