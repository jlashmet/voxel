# Tasks — Kentridge vegetation meadow density

## Investigation
- [x] Fetch current repository state and resume `fixes/agent-5`.
- [x] Read `AGENTS.md`, the canonical SceneIssue workflow, and the assigned issue; the requested `SceneIssues/feature-readme.md` is absent, so use `SceneIssues/README.md` as the repository workflow source.
- [x] Maintain separate `plan.md` and `tasks.md` before implementation.
- [x] Inspect Kentridge definition/runtime vegetation, point-cloud placement, procedural grass renderer, and existing tests.
- [x] Reject renderer-capacity hypothesis: the current renderer packs blades into mesh chunks (`36000` blades/chunk); the stale 1,023-instance notes no longer describe production.
- [x] Identify acceptance-proof gaps: semantic instance count is not rendered blade count; `excludedPlacements=0` is hard-coded; required exclusion classes are not all represented by reusable policy.
- [ ] Confirm the shared grass material/shader and render-config wind path is time-varying before deciding whether production wind code needs modification.
- [ ] Inspect Kentridge/world APIs for cultivated/water/building classification so runtime policy mapping uses existing authored semantics where available.

## Implementation
- [x] Keep the existing additive per-region ecology policy with density multipliers, palette metadata, meadow limits, route clearance, and slope controls.
- [ ] Add explicit reusable meadow exclusion classes/policy for building, path/route, cultivated, water/wet, steep/cliff, and other-invalid surfaces.
- [ ] Route Kentridge meadow sampling through that reusable exclusion policy and measure rejection/leakage by class.
- [ ] Extract the renderer's deterministic 5–15 blades-per-seed calculation into shared rendering API code used by `ProceduralGrassBatch`, diagnostics, and regressions.
- [ ] Change Kentridge acceptance/diagnostics to report renderer-equivalent visible blade count `>= 3000` while retaining semantic grass-instance count for cost visibility.
- [x] Keep one deterministic connected primary meadow authored from Kentridge regional configuration rather than scene-local grass objects.
- [x] Keep denser edge/undergrowth synthesis driven by the regional ecology profile.
- [ ] Preserve the existing shared grass wind path; modify production wind code only if exact built-player evidence proves it broken.

## Regression coverage
- [x] Prove Kentridge definition exposes the regional profile, denser-than-baseline multipliers, palette, and meadow limits.
- [ ] Prove the reusable policy rejects every required surface class individually: building, path, cultivated, water, steep/cliff, and other-invalid.
- [ ] Prove production-path deterministic meadow placement reaches `>= 3000` renderer-equivalent blades in one connected field and places zero grass on excluded samples.
- [x] Prove density multipliers affect deterministic candidate counts and richer undergrowth remains deterministic.
- [ ] Prove the shared blade-count contract stays deterministic and bounded at 5–15 blades per semantic grass instance.
- [ ] Anchor wind regression/diagnostic to nonzero bend plus the shared wind-enabled render path; built-player time-separated frames remain authoritative.
- [ ] Preserve/verify current packed-chunk high-density renderer regressions; remove stale >1023 batching assumptions from feature evidence.

## Blast radius / cost
- [x] Keep the world-builder API additive and Kentridge runtime changes confined to the Kentridge playable composition seam.
- [x] Confirm non-Kentridge regions retain existing behavior unless they opt into an ecology policy.
- [ ] Record exact semantic grass count, deterministic rendered blade count, and renderer mesh-chunk count for the primary meadow.
- [ ] Confirm no new per-frame allocations, material churn, grass GameObjects, scene serialization, or shader fork.
- [ ] Review final diff for assignment-only scope and confirm `.github/test-request.json` is absent from `fixes/agent-5` changes.

## Workflow validation / artifacts
- [ ] Run required canonical pre-merge validation scripts/checklists for the changed module set.
- [ ] Refresh required validation hashes/reports and feature-local validation evidence.
- [ ] Run focused EditMode behavioral regressions for Kentridge vegetation and procedural grass rendering.
- [ ] Complete blast-radius/cost report before closure.

## Built-player visual gate
- [ ] Validate exact Kentridge scene in the built application without startup/runtime exceptions.
- [ ] Capture dense gameplay approach view and close player-height meadow view.
- [ ] Record durable diagnostic proving one connected meadow has `>= 3000` rendered blades and zero excluded-surface leakage.
- [ ] Capture at least two time-separated frames from the same stationary view proving visible wind motion.
- [ ] Store concise human-inspectable verification evidence beside the feature.

## Acceptance
- [ ] (1) World-builder callers can select a vegetation density/palette policy per area/region.
- [ ] (2) One connected Kentridge meadow has `>= 3000` rendered grass blades and reads materially denser/region-specific.
- [ ] (3) Roads, building footprints/interiors, water, cliffs, cultivated plots, and other invalid surfaces receive zero meadow placements.
- [ ] (4) Placement is deterministic and existing shared grass motion/wind produces a subtly animated field.
- [ ] (5) Durable nonvisual regression/diagnostic proves the rendered-blade threshold and zero excluded-surface placements.
- [ ] (6) Blast radius/cost is measured and acceptable.

## Promotion / publish
- [ ] Commit implementation and regressions on `fixes/agent-5`.
- [ ] Move only this feature `open -> pending` with implementation/test notes after implementation validation.
- [ ] Run every required workflow gate for the resulting exact feature SHA.
- [ ] Use only `ci-test/fixes/agent-5` for the single final targeted-CI request; never edit `.github/test-request.json` on the feature branch.
- [ ] Require green targeted CI for the exact feature SHA; if code changes afterward, repeat required gates/CI according to repository rules.
- [ ] Complete pending metadata/FIX_EVIDENCE and every acceptance/checklist item.
- [ ] Move `pending -> closed`, set `status=fixed` and `resolvedUtc`.
- [ ] Merge current `origin/master` into `fixes/agent-5`; fetch/merge/retry if master advanced.
- [ ] Push that exact feature head non-force to `origin/master` and verify `master == fixes/agent-5`.
