# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Preserve the source-backed Mounting Force/Kentridge graph while physically realizing settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded slopes, semantic constrained routes, lake/ridge realization, feature-aware vertical residency/readiness, and production evidence sequencing.
- Experiments 019-025 ruled out missing planned structures, coarse visibility/readiness, and settlement survey framing. Experiment 023 replaced the costly solid blockout fallback with bounded hollow wall shells and retained an independent cost regression.
- Experiment 026 exact run `33417092425` is decisive: Moordell and Rossdam have all four authored building centres in the 90-degree survey with stable production coverage, but authoritative `TryFindTopSolid` reports `delta=0` from procedural terrain at every sampled centre. The surviving defect is therefore runtime geometry/publication cardinality, not camera, timing, readiness, or renderer-only presentation.
- Agent 7's shared WorldBuilder spatial reservation system is now integrated without changing ownership: `TopDownWorldPhysicalPlanner` still owns geography and hard-route solving, while the resolved settlement envelopes, generic building footprint/clearance, resolved road segments, and settlement-arrival handoffs are published and conflict-checked through `SpatialReservationSnapshot`. The independent non-Kentridge `alpha`/`beta` fixture proves a road-through-building conflict is rejected without Kentridge coordinates.
- Spatial-reservation integration is exact-SHA green at source `7033ee755ae6bac7cc3c1cc3c83bb4ee2e7d5f5e`: run `33441865025`, job `99651749224`, artifact `9776935720`. The focused independent fixture, automatic `kentridge-integration` and `spatial-reservations` validation, required EditMode suites, both real-player module validations, and the 60-second SceneIssue replay passed.
- Repaired master `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e` is merged into this branch by two-parent merge `eb97dc5678a86a4031edd14d2682824a84943bc0`; the branch was verified 0 commits behind master at that point.

## Root-cause gate
Experiment 026 reproduced the same settlement-visibility symptom after two materially different remediation attempts. Per assignment rules, no third speculative geometry correction is allowed until a minimal runtime discriminator selects the failing boundary.

Generic `FEATUREGEN_TRACE` instrumentation now traces `FeatureKind.Structure` through shared production feature generation while remaining silent during ordinary player launches. Existing SceneIssue diagnostics enable it through `-voxel-scene-issue` without Kentridge-specific shared-runtime policy or CI workflow changes. It records:
- `candidate`: an authored structure placement reached a streamed region after footprint intersection.
- `rejected`: instance evaluation failed, including the exact `EvaluationResult`.
- `accepted`: evaluation succeeded, including primitive count.
- `completed`: rasterization/mutation completed, including whether anything rasterized and the per-instance voxel-write delta.

Experiment 027 will correlate that trace with final settlement `MACROEVIDENCE` on one exact source SHA. The discriminator is:
1. no candidate -> catalogue/rule selection or region scheduling;
2. candidate then rejected -> evaluator/footprint/shape semantics;
3. accepted then `voxels=0` -> primitive rasterization/mutation;
4. accepted then `voxels>0` while the final survey remains terrain -> later overwrite, store replacement, or publication ordering.

Only after Experiment 027 selects one of those owners will a focused regression and minimal correction be made.

## Remaining gates
1. Run Experiment 027 on the exact post-documentation feature SHA using the focused production-storage settlement test plus the 60-second SceneIssue replay.
2. Inspect `FEATUREGEN_TRACE` together with settlement `MACROEVIDENCE`, document the selected owner, add a focused behavioral regression, and fix only that owner.
3. Re-run Moordell/Rossdam settlement surveys and require authored settlement mass above ground at the required building locations; then re-verify Fairy/Orc settlement readability.
4. Re-check Rossdam water/constrained route, Southern Ridge/pass, differentiated countryside, macro-network overview, and CharacterMotor route traversal in durable built-player evidence.
5. Measure vertical residency and world-build/route/feature/CPU/FPS/memory/streaming/render/far-field cost against budgets.
6. Re-fetch/merge current master if it advanced, pass final exact-SHA focused + automatic module + SceneIssue gates, then close/move the assignment and non-force promote the exact feature head per workflow.
