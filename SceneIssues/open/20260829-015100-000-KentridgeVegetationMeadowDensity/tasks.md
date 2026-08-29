# Tasks — Kentridge vegetation meadow density

## Investigation
- [x] Fetch current repository state and resume `fixes/agent-5`.
- [x] Read `AGENTS.md`, the canonical SceneIssue workflow, and the assigned issue; the requested `SceneIssues/feature-readme.md` is absent, so use `SceneIssues/README.md` as the repository workflow source.
- [x] Maintain separate `plan.md` and `tasks.md` before implementation.
- [x] Inspect Kentridge definition/runtime vegetation, point-cloud placement, procedural grass renderer, and existing tests.
- [x] Reject renderer-capacity hypothesis: the current renderer packs semantic grass into 32 m mesh chunks rather than creating per-blade GameObjects or relying on the old 1,023-instance path.
- [x] Identify acceptance-proof gaps: semantic instance count was not rendered blade count and required exclusion classes were not represented by reusable policy.
- [x] Confirm the shared grass wind path is intended to be time-varying through `_GrassTime` / `Time.time`; built-player evidence remains authoritative for whether motion actually reaches rendered blades.
- [x] Inspect Kentridge/world classification evidence: route distance, `HasBuiltContentAbove`, riverbank theme, and terrain normals provide existing route/building/water/slope evidence; the current theme map does not separately identify cultivated plots, so do not misclassify all farmland meadow terrain as cultivated.
- [x] Inspect failed final CI run `33234652795`: classify it as a product/branch compile failure, not infrastructure; no player build or screenshots were produced.
- [x] Root-cause the compile failure: `Game.Kentridge.PlayableSlice.KentridgeDefinition` shadows the imported WorldGen definition and its compatibility facade did not forward `CountrysideEcology`.
- [x] Inspect final CI run `33236269717` and its one permitted infrastructure retry: targeted PlayMode acceptance passes and the built player runs 60 seconds with zero assertions, but the visual replay gate rejects this ticket as having no replayable camera snapshot.
- [x] Compare a known-good historical Kentridge SceneIssue capture: `poseAnchor` may be null and older captures used `FirstPerson-AIO/FirstPersonCharacter/Capsule/PlayerCamera`; do not assume that historical hierarchy still exists in the current scene.
- [x] Inspect failed final CI run `33240721951`: focused PlayMode acceptance passes and the real player reports 11,478 grass instances / 114,580 blades with zero excluded-surface grass, but visual replay arms and never pins.
- [x] Discriminate replay hypotheses against the exact current scene: `Assets/Scenes/KentridgePlayableSlice.unity` contains a root `Kentridge Player Camera` tagged MainCamera, while the issue metadata targeted the obsolete historical FirstPerson hierarchy. This is an assignment-local capture-metadata defect, not a reason to change production camera/harness code.
- [x] Inspect corrected exact-SHA CI run `33242524673`: focused PlayMode, built-player launch, replay pinning, artifact upload, and automated status are green; the meadow is dense and correctly framed.
- [x] Perform mandatory human visual inspection of run `33242524673`: stationary late meadow captures at approximately 29.7 s, 39.7 s, 49.7 s, and 59.7 s show unchanged grass silhouettes/pixels, so visible wind animation is still a product/visual failure despite green automation.
- [ ] Root-cause the frozen built-player wind by discriminating shader-time aliasing, paused/scaled Unity time, per-frame material-state publication, and shader/material binding/inclusion using the shared vegetation rendering path.

## Implementation
- [x] Keep the existing additive per-region ecology policy with allowed vegetation, density, deterministic seed salt, sample spacing, route clearance, and slope controls.
- [x] Add explicit reusable ecology exclusion classes/policy for building, path/route, cultivated, water/wet, steep/cliff, and other-invalid surfaces.
- [x] Route Kentridge meadow sampling through that reusable exclusion policy and measure rejection/leakage by concrete class.
- [x] Move the renderer's deterministic 5–15 blades-per-seed calculation into shared `VoxelEngine.Vegetation.Api` code used by `ProceduralGrassBatch`, Kentridge diagnostics, and regressions.
- [x] Change Kentridge diagnostics to report renderer-equivalent visible blade count and connected-primary-meadow blade count while retaining semantic grass-instance count for cost visibility.
- [x] Keep one deterministic connected primary meadow authored from Kentridge regional configuration rather than scene-local grass objects.
- [x] Keep denser undergrowth synthesis driven by the regional ecology profile.
- [x] Preserve a single shared grass wind system; do not introduce a Kentridge shader fork or second animation mechanism.
- [x] Forward the authored WorldGen `CountrysideEcology` policy through the playable WorldBuilder compatibility facade so the local `KentridgeDefinition` shadow remains an intentional adapter rather than a duplicated policy owner.
- [x] Correct only this assignment's recorded camera replay metadata to the exact current `Kentridge Player Camera` hierarchy; scene serialization, gameplay code, and shared replay harness remain unchanged for that metadata defect.
- [ ] Fix the proven shared procedural-grass wind defect at the minimal reusable rendering/material/shader seam, preserving batching and avoiding per-blade GameObjects, per-frame mesh rebuilds, and material allocation churn.

## Regression coverage
- [x] Prove Kentridge definition exposes the grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove the reusable policy can author every required exclusion class individually: building, path, cultivated, water, steep/cliff, and other-invalid.
- [x] Prove production-path deterministic meadow placement reaches `>= 3000` renderer-equivalent blades in one connected field and generated grass originates only from eligible samples.
- [x] Prove density/kind filtering remain deterministic through the production vegetation-placement path.
- [x] Prove the shared blade-count contract stays deterministic and bounded at 5–15 blades per semantic grass instance and is the exact contract used by the packed renderer.
- [ ] Add a focused regression at the shared rendering seam proving grass wind state advances across render frames/time without requiring a vegetation rebuild.
- [x] Preserve the current packed-chunk renderer and remove stale >1023 batching assumptions from feature evidence.

## Blast radius / cost
- [x] Keep the world-builder API additive: new constructor input is optional and defaults to the safe exclusion mask.
- [x] Keep Kentridge realization changes confined to the Kentridge playable composition seam; non-Kentridge callers retain existing behavior unless they opt into ecology authoring.
- [x] Record production-player runtime grass volume: `11,478` semantic grass instances / `114,580` deterministic rendered blades total; primary meadow `5,777` instances / `57,589` blades; packed into `8` grass mesh chunks; measured excluded-surface leakage `0`.
- [x] Confirm by diff/source review that no new per-frame allocations, material churn, grass GameObjects, scene serialization, shader fork, or per-frame CPU blade animation was introduced by the ecology/density changes; added collections are populated during `Populate`.
- [x] Review current feature diff for assignment-only scope and confirm `.github/test-request.json` is absent from `fixes/agent-5` changes.
- [x] Confirm the compile-seam correction is a single compatibility-property forwarder with no runtime allocation or renderer cost.
- [x] Confirm the replay repair is assignment-local capture metadata only and has zero production runtime/rendering cost.
- [x] Record available canonical built-player cost evidence: the prior visual run reports approximately `110.01` FPS; logs expose semantic-instance/blade/chunk topology above. The canonical artifact exposes no per-feature CPU-ms, GPU-ms, resident-memory, or build-time budget metric, so those unavailable dimensions are explicitly documented rather than invented.
- [ ] Reassess per-frame CPU/material/render cost after the wind fix and confirm no new allocation/material churn or mesh rebuild is introduced.

## Workflow validation / artifacts
- [ ] Run required canonical pre-merge validation scripts/checklists for the changed module set.
- [ ] Refresh required validation hashes/reports and feature-local validation evidence.
- [ ] Run focused EditMode behavioral regressions for Kentridge vegetation and procedural grass rendering.
- [ ] Complete runtime blast-radius/cost report before closure.
- [ ] Re-run the final focused PlayMode + exact built-player replay after the wind fix from the new exact feature SHA.
- [x] Validate that corrected replay metadata photographs the intended meadow viewpoint rather than the opening interior/cutscene.

## Built-player visual gate
- [x] Validate exact Kentridge scene in the built application without startup/runtime exceptions on run `33242524673`; this must be repeated after the wind fix.
- [x] Capture a close player-height meadow view showing dense procedural grass on run `33242524673`; repeat final evidence after the wind fix.
- [x] Record durable diagnostic proving one connected meadow has `>= 3000` rendered blades and zero excluded-surface leakage: primary meadow `57,589` blades / `5,777` semantic instances, leakage `0`.
- [ ] Capture at least two time-separated frames from the same stationary view proving visible wind motion; run `33242524673` explicitly fails this gate because frames are visually unchanged.
- [ ] Store concise human-inspectable final verification evidence beside the feature.

## Acceptance
- [x] (1) WorldBuilder exposes reusable per-area controls for allowed vegetation, density/coverage, deterministic variation, exclusions, and ambient-animal allowlist.
- [ ] (2) Kentridge uses that path and one connected built-player meadow has `>= 3000` rendered grass blades and reads as a full meadow in final post-fix evidence.
- [ ] (3) Roads, building footprints/interiors, water, cliffs/steep terrain, cultivated plots when semantically identified, and other invalid surfaces receive zero meadow placements in final evidence.
- [ ] (4) Placement is deterministic and shared grass motion/wind visibly animates the built field while stationary.
- [ ] (5) Durable regression/diagnostic plus built evidence proves the rendered-blade threshold and zero excluded-surface placements.
- [ ] (6) Blast radius/cost is measured and acceptable after the final wind fix.

## Promotion / publish
- [x] Commit ecology/density implementation and regressions on `fixes/agent-5`.
- [ ] Commit the wind fix and its focused regression on `fixes/agent-5`.
- [ ] Move only this feature `open -> pending` after all mandatory built-player visual gates are satisfied; the ticket forbids moving to pending on source/tests alone.
- [ ] Run every required workflow gate for the resulting exact feature SHA.
- [ ] Use only `ci-test/fixes/agent-5` for the final targeted-CI request for the final feature SHA; never edit `.github/test-request.json` on the feature branch.
- [ ] Require green targeted CI for the exact feature SHA; if code changes afterward, repeat required gates/CI according to repository rules.
- [ ] Complete pending metadata/FIX_EVIDENCE and every acceptance/checklist item.
- [ ] Move `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-5`; fetch/merge/retry if master advanced.
- [ ] Push that exact feature head non-force to `origin/master` and verify `master == fixes/agent-5`.
