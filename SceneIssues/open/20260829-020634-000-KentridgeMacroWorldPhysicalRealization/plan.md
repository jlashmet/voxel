# Plan

## Observed behavior / acceptance
`captures` is empty, so the feature note is the repro contract. Closure requires the source-backed Kentridge macro graph to produce deterministic physical settlements, continuous terrain-aware hard routes, reusable regional constraints, a substantial lake/ridge with route response, real CharacterMotor traversal, closure-quality built-player evidence, and measured cost. `SceneIssues/feature-readme.md` is absent; canonical `SceneIssues/README.md` governs.

## Current hypotheses and results
1. **Macro generation/streaming is wholly missing.** Rejected: production acceptance has repeatedly produced 20 hard routes and 16 generic buildings; routes and geography render in the built player.
2. **Duplicate Rossdam water ownership caused the clipped water sheet.** Confirmed and fixed: standalone callers retain generic water, while Kentridge combined composition gives Rossdam water only to the carved-basin catalogue.
3. **One terrain sample buried generic buildings.** Confirmed and fixed in catalogue authoring: each padded footprint now uses a bounded 5x5 relief sample; foundation spans sampled relief and shell/roof are offset above the sampled high point.
4. **The new all-building exclusion regression would expose any remaining semantic overlap.** Confirmed. Exact request `a3f2d6d6652abac2dcf9061f9dda51b9e6ecb52b`, run `33279138597`, failed specifically because `orc-village building 3` overlaps `southern-ridge`. The nested production acceptance passed first: 20 routes, 16 buildings, 5 constrained routes, max rise 2 voxels.
5. **That failure was Rossdam or infrastructure.** Rejected. Full NUnit evidence proves a real Orc/ridge product conflict.
6. **The remaining visual problem is only camera framing.** Rejected again by full-resolution artifact `9722783657`: Fairy/Orc settlement cameras are close enough that a present 13x10 m near-side blockout would dominate the frame, yet no shells render.
7. **The Southern Ridge fix / final persisted storage is still the blocker.** Rejected. Exact request `c1a21b76cdc548436a32bd0866f26a2448a67286`, run `33283034449`, is green for source `0bbc9150f36281c0f951d9c75a60b318842fba46`; the production storage test reaches all expected persisted macro regions, route traversal metadata, and settlement shell material probes.
8. **Green storage therefore proves visual acceptance.** Rejected. Full-resolution artifact `9723674189` from the same exact-SHA run still shows no readable shells at Fairy Village or Orc Village, fewer than four obvious shells at Moordell, and no four-building settlement read at Rossdam. This is now a production load/meshing/render-path discriminator, not a generation/storage or framing discriminator.

## Selected remediation / next discriminator
- Keep the source-backed topology, existing four-shell settlement program, Southern Ridge extent fix, authored route solutions, normal streaming radius, and camera evidence framing unchanged.
- Trace one known persisted `SettlementStructure` shell voxel from `RegionFileStore`/`RegionData` through `RegionStreamer` into the production voxel mesh/render path. Find the first boundary where the feature is omitted, overwritten, culled, or rendered below/inside the visible terrain surface.
- Add a behavioral regression at that exact boundary. The regression must prove a persisted settlement shell produces renderer/mesh-visible solid geometry; a storage-only non-air assertion is insufficient.
- Fix the reusable production boundary only. Do not add scene-local destination GameObjects, direct scene voxel writes, evidence-only rendering, eager remote objects, or increased residency to make screenshots pass.
- Re-run focused production behavior plus the built-player evidence scenario on one immutable final source SHA. Closure requires four readable blockouts at every generic settlement in the normal representative cameras.

## Blast radius / cost
- `fixes/agent-6` was refreshed from current `origin/master` with merge commit `73c62df7dd6be7f16dae16da1b8c1b0a6646286f`; master changes were path-disjoint from agent-6 work.
- Keep remediation within reusable world-builder streaming/meshing/render integration. Avoid changes to `CharacterMotor`, streaming radius, unrelated SceneIssues, or scene-authored static destination hierarchies.
- Existing terrain-relief work remains bounded to 25 samples x 16 buildings = 400 deterministic catalogue-build queries.
- Any render-path fix must avoid per-frame whole-world scans or duplicate geometry. Re-measure final player CPU/GPU/frame/memory/streaming telemetry and compare generated mesh/voxel counts to the existing four-shell-per-settlement program.
- Prior green cost baseline: player ~73 s, peak RSS ~5.6 GB, zero swap growth; this remains the comparison point, not proof for the final remediation.

## Remaining gate
Trace and repair the production settlement render path, add the renderer/mesh-visible regression, self-review branch scope and current master, then issue only the designated exact-SHA target on `ci-test/fixes/agent-6`. Reject closure unless focused behavioral/storage validation is green **and** full-resolution built `KentridgePlayableSlice` evidence visibly shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village, a clean substantial lake, readable ridge/pass response, connected roads without large holes, and real CharacterMotor traversal. Only then complete pending metadata, move open -> pending -> closed, set `status=fixed`/`resolvedUtc`, merge current master, and non-force promote the exact feature head.
