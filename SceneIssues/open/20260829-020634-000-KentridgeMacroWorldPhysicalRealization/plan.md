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
8. **Green storage therefore proves visual acceptance.** Rejected. Full-resolution artifact `9723674189` from the same exact-SHA run still shows no readable shells at Fairy Village or Orc Village, fewer than four obvious shells at Moordell, and no four-building settlement read at Rossdam.
9. **Persisted shell voxels are dropped by RegionLoader or the near-field mesher.** Rejected at the first production boundary. `RegionLoader` only manages logical residency, while the authoritative near-field cache consumes committed region data and invalidations. Production `ShowcaseWorld.FinishRegion()` publishes terrain first and only then queues feature realization; `CompleteFeatureBuild()` publishes the later feature commit/invalidation. The evidence driver's renderer-only `HasCompletePublishedNearSurfaceCoverage()` gate can therefore become true in the terrain-only interval and capture before settlement shells have been committed/remeshed. The defect is a missing world-content readiness contract, not missing persisted data or a proven mesher omission.

## Selected remediation / next discriminator
- Keep the source-backed topology, existing four-shell settlement program, Southern Ridge extent fix, authored route solutions, normal streaming radius, and camera evidence framing unchanged.
- Treat terrain generation and separately queued feature realization as one **current-demand readiness contract**. A view is not world-content settled while any currently demanded generated region is still awaiting terrain completion or feature realization that can publish/invalidate it.
- Add a reusable `ShowcaseWorld` readiness query scoped to current demand rather than a fixed delay or global whole-world idle scan. Expose that query through the Kentridge production slice.
- Gate evidence capture on content-settled **then** complete renderer near-surface publication so a feature commit's invalidation/remesh must be observed before capture.
- Add a behavioral regression around the two-stage publication boundary: terrain publication alone must not report current-demand content ready; readiness may become true only after feature publication, and the resulting settlement shell must be renderer/mesh-visible rather than merely non-air in storage.
- Do not add scene-local destination GameObjects, direct scene voxel writes, evidence-only geometry, eager remote generation, arbitrary settling delays, camera masks, or increased residency.
- Re-run focused production behavior plus the built-player evidence scenario on one immutable final source SHA. Closure requires four readable blockouts at every generic settlement in the normal representative cameras.

## Blast radius / cost
- `fixes/agent-6` was refreshed from current `origin/master` with merge commit `73c62df7dd6be7f16dae16da1b8c1b0a6646286f`; master changes were path-disjoint from agent-6 work. Re-check current master again before final promotion.
- Keep remediation within reusable generated-world readiness plus the Kentridge consumer of that contract. Avoid changes to `CharacterMotor`, streaming radius, unrelated SceneIssues, or scene-authored static destination hierarchies.
- The readiness query must inspect only current demand / already-maintained generation state; reject per-frame whole-world scans, duplicate geometry, or additional remote generation.
- Existing terrain-relief work remains bounded to 25 samples x 16 buildings = 400 deterministic catalogue-build queries.
- Re-measure final player CPU/GPU/frame/memory/streaming telemetry and compare generated mesh/voxel counts to the existing four-shell-per-settlement program.
- Prior green cost baseline: player ~73 s, peak RSS ~5.6 GB, zero swap growth; this remains the comparison point, not proof for the final remediation.

## Remaining gate
Implement and regress the current-demand generated-content readiness boundary, prove the final feature publication reaches renderer/mesh-visible geometry, self-review branch scope and current master, then issue only the designated exact-SHA target on `ci-test/fixes/agent-6`. Reject closure unless focused behavioral/storage validation is green **and** full-resolution built `KentridgePlayableSlice` evidence visibly shows four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village, a clean substantial lake, readable ridge/pass response, connected roads without large holes, and real CharacterMotor traversal. Only then complete pending metadata, move open -> pending -> closed, set `status=fixed`/`resolvedUtc`, merge current master, and non-force promote the exact feature head.
