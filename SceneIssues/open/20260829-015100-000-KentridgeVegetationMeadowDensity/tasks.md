# Tasks — Kentridge vegetation meadow density

## Investigation
- [x] Fetch current repository state and resume `fixes/agent-5`.
- [x] Read `AGENTS.md`, the canonical SceneIssue workflow, and the assigned issue; the requested `SceneIssues/feature-readme.md` is absent, so use `SceneIssues/README.md` as the repository workflow source.
- [x] Maintain separate `plan.md` and `tasks.md` before implementation.
- [x] Inspect Kentridge definition/runtime vegetation, point-cloud placement, procedural grass renderer, and existing tests.
- [x] Reject renderer-capacity hypothesis: the current renderer packs semantic grass into 32 m mesh chunks rather than creating per-blade GameObjects or relying on the old 1,023-instance path.
- [x] Identify acceptance-proof gaps: semantic instance count was not rendered blade count and required exclusion classes were not represented by reusable policy.
- [x] Confirm the shared grass wind path is time-varying: `ProceduralVegetationGrass.shader` bends tips from `_GrassTime`, and `ProceduralVegetationMaterials.ApplyGrassState` republishes `Time.time` each draw.
- [x] Inspect Kentridge/world classification evidence: route distance, `HasBuiltContentAbove`, riverbank theme, and terrain normals provide existing route/building/water/slope evidence; the current theme map does not separately identify cultivated plots, so do not misclassify all farmland meadow terrain as cultivated.
- [x] Inspect failed final CI run `33234652795`: classify it as a product/branch compile failure, not infrastructure; no player build or screenshots were produced.
- [x] Root-cause the compile failure: `Game.Kentridge.PlayableSlice.KentridgeDefinition` shadows the imported WorldGen definition and its compatibility facade did not forward `CountrysideEcology`.
- [x] Inspect final CI run `33236269717` and its one permitted infrastructure retry: targeted PlayMode acceptance passes and the built player runs 60 seconds with zero assertions, but the visual replay gate rejects this ticket as having no replayable camera snapshot.
- [x] Compare a known-good Kentridge SceneIssue capture: `poseAnchor` may be null, while the replayable player camera is identified by `FirstPerson-AIO/FirstPersonCharacter/Capsule/PlayerCamera`; this ticket has the matching player-camera capture intent but an empty `camera.hierarchyPath`.

## Implementation
- [x] Keep the existing additive per-region ecology policy with allowed vegetation, density, deterministic seed salt, sample spacing, route clearance, and slope controls.
- [x] Add explicit reusable ecology exclusion classes/policy for building, path/route, cultivated, water/wet, steep/cliff, and other-invalid surfaces.
- [x] Route Kentridge meadow sampling through that reusable exclusion policy and measure rejection/leakage by concrete class.
- [x] Move the renderer's deterministic 5–15 blades-per-seed calculation into shared `VoxelEngine.Vegetation.Api` code used by `ProceduralGrassBatch`, Kentridge diagnostics, and regressions.
- [x] Change Kentridge diagnostics to report renderer-equivalent visible blade count and connected-primary-meadow blade count while retaining semantic grass-instance count for cost visibility.
- [x] Keep one deterministic connected primary meadow authored from Kentridge regional configuration rather than scene-local grass objects.
- [x] Keep denser undergrowth synthesis driven by the regional ecology profile.
- [x] Preserve the existing shared grass wind path; no second animation system or Kentridge shader fork is introduced because source tracing shows the production time binding is present.
- [x] Forward the authored WorldGen `CountrysideEcology` policy through the playable WorldBuilder compatibility facade so the local `KentridgeDefinition` shadow remains an intentional adapter rather than a duplicated policy owner.
- [x] Repair only this assignment's recorded camera replay metadata by restoring the known Kentridge player-camera hierarchy identity; no scene serialization or gameplay code changed for this replay defect.

## Regression coverage
- [x] Prove Kentridge definition exposes the grass-only dense regional policy and empty tree/ambient-animal allowlists.
- [x] Prove the reusable policy can author every required exclusion class individually: building, path, cultivated, water, steep/cliff, and other-invalid.
- [x] Prove production-path deterministic meadow placement reaches `>= 3000` renderer-equivalent blades in one connected field and generated grass originates only from eligible samples.
- [x] Prove density/kind filtering remain deterministic through the production vegetation-placement path.
- [x] Prove the shared blade-count contract stays deterministic and bounded at 5–15 blades per semantic grass instance and is the exact contract used by the packed renderer.
- [x] Preserve the shared wind shader/material path in production; built-player time-separated frames remain the authoritative animation regression.
- [x] Preserve the current packed-chunk renderer and remove stale >1023 batching assumptions from feature evidence.

## Blast radius / cost
- [x] Keep the world-builder API additive: new constructor input is optional and defaults to the safe exclusion mask.
- [x] Keep Kentridge realization changes confined to the Kentridge playable composition seam; non-Kentridge callers retain existing behavior unless they opt into ecology authoring.
- [x] Record retry runtime counts: 11,478 semantic grass instances / 114,580 rendered blades total; primary connected meadow = 5,777 semantic instances / 57,589 rendered blades; renderer produced 8 grass mesh chunks; excluded-surface grass = 0.
- [x] Confirm by diff/source review that no new per-frame allocations, material churn, grass GameObjects, scene serialization, shader fork, or per-frame CPU blade animation was introduced; added collections are populated during `Populate`, while wind remains GPU vertex deformation.
- [x] Review current feature diff for assignment-only scope and confirm `.github/test-request.json` is absent from `fixes/agent-5` changes.
- [x] Confirm the compile-seam correction is a single compatibility-property forwarder with no runtime allocation or renderer cost.
- [x] Confirm the camera replay correction is assignment metadata only and adds zero production CPU/GPU/memory/build cost.

## Workflow validation / artifacts
- [ ] Run required canonical pre-merge validation scripts/checklists for the final merged feature SHA.
- [ ] Refresh required validation hashes/reports and feature-local validation evidence.
- [ ] Run focused EditMode/PlayMode behavioral regressions for Kentridge vegetation and procedural grass rendering on the final merged feature SHA.
- [ ] Complete runtime blast-radius/cost report before closure, including corrected-replay player performance evidence against existing expectations.
- [x] Retry evidence: focused PlayMode acceptance passed and built player launched/runs 60 seconds with zero assertions at source SHA `785db49394fabefe99a1dcb6628ed7fa8c169065`; this evidence is diagnostic because the visual replay gate failed.
- [ ] After repairing camera replay metadata, validate that the exact built-player artifact actually photographs the meadow viewpoint rather than the opening interior/cutscene.

## Built-player visual gate
- [ ] Validate exact Kentridge scene in the built application without startup/runtime exceptions on the final exact SHA.
- [ ] Capture dense gameplay approach view and close player-height meadow view.
- [ ] Record durable diagnostic proving one connected meadow has `>= 3000` rendered blades and zero excluded-surface leakage.
- [ ] Capture at least two time-separated frames from the same stationary view proving visible wind motion.
- [ ] Store concise human-inspectable verification evidence beside the feature.

## Acceptance
- [x] (1) WorldBuilder exposes reusable per-area controls for allowed vegetation, density/coverage, deterministic variation, exclusions, and ambient-animal allowlist.
- [ ] (2) Kentridge uses that path and one connected built-player meadow has `>= 3000` rendered grass blades and reads as a full meadow.
- [ ] (3) Roads, building footprints/interiors, water, cliffs/steep terrain, cultivated plots when semantically identified, and other invalid surfaces receive zero meadow placements.
- [ ] (4) Placement is deterministic and existing shared grass motion/wind visibly animates the built field while stationary.
- [ ] (5) Durable regression/diagnostic plus built evidence proves the rendered-blade threshold and zero excluded-surface placements.
- [ ] (6) Blast radius/cost is measured and acceptable.

## Promotion / publish
- [x] Commit implementation and regressions on `fixes/agent-5`.
- [ ] Move only this feature `open -> pending` after all mandatory built-player visual gates are satisfied; the ticket forbids moving to pending on source/tests alone.
- [ ] Run every required workflow gate for the resulting exact feature SHA.
- [ ] Use only `ci-test/fixes/agent-5` for the final targeted-CI request; never edit `.github/test-request.json` on the feature branch or create another transport.
- [ ] Require green targeted CI for the exact feature SHA; if code changes afterward, repeat required gates/CI according to repository rules.
- [ ] Complete pending metadata/FIX_EVIDENCE and every acceptance/checklist item.
- [ ] Move `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-5`; fetch/merge/retry if master advanced.
- [ ] Push that exact feature head non-force to `origin/master` and verify `master == fixes/agent-5`.
