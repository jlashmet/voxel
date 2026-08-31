# Plan

## Acceptance authority
`captures` is empty, so `issue.json` is the acceptance contract. Keep the source-backed Mounting Force/Kentridge graph authoritative through shared WorldBuilder APIs; physically realize settlements, terrain-aware hard routes, reusable geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, readable built-player evidence, and bounded cost. `AGENTS.md`, `SceneIssues/README.md`, and `SceneIssues/feature-readme.md` govern this feature.

## Proven results
- Foundation is implemented: 20 hard routes, 6 settlements, 16 generic buildings, deterministic geography, bounded road slopes, substantial lake/ridge, semantic constrained routes, and two-stage presentation readiness. Shared planner/catalogue APIs contain no Kentridge/Rossdam policy; the independent non-Kentridge `a`/`b` blocked-water fixture proves reuse.
- Exact runs `33346099006` and `33354287850` prove feature-aware vertical residency and readiness include authored upper Y-layers without widening horizontal interest or changing device/scheduler policy.
- Experiments 019/020 proved Rossdam structures exist in authoritative storage, are grounded, intersect the survey frustum, and have ready visible production geometry. Experiments 021/022 isolated framing after two failed camera corrections and replaced the insufficient scalar test with exact projected 3D envelope containment.
- Experiment 023 replaced the demonstrated-cost solid fallback building body with four bounded exterior wall boxes; independent synthetic regression proves four walls, hollow centre, and <25% former body volume. Exact run `33405010658` passes this regression and real-player startup; Rossdam publication falls from ~1.03–1.20M to ~100k–120k ready indices per blockout, but full-resolution settlement/lake evidence remains closure-red.
- Experiment 024 ensures the validation-only 90° settlement lens runs after the evidence survey pose. Experiment 025 adds a later read-only capture diagnostic. Initial run `33409727597` was a product compile failure from one missing namespace and was corrected rather than retried unchanged.
- Exact feature `e1f734cbd9a6436fff5c7620d9557773b957d9b7`, CI request `1477dce4a1ecf01ce2e22b9953e650e4255fecbb`, run `33410426650` is workflow-green. Actual capture FOV is 90° and all four authored centres are inside the frame for Moordell and Rossdam. Full-resolution evidence is still closure-red: large pale regions follow terrain contours and multiple authored centres do not read as roofs/buildings. Framing is therefore rejected as the surviving owner.

## Current discriminator
Two plausible owners remain. H1: at capture time an unreadable building centre has no authoritative roof-height top voxel despite coarse readiness/coverage, so publication/readiness granularity remains wrong. H2: authoritative roof-height voxels exist, but production surface extraction/LOD/presentation shows terrain-like material instead.

Experiment 026 will extend validation-only capture observation without mutation. For each authored settlement centre, use the existing semantic `ShowcaseWorld.SurfaceQuery.TryFindTopSolid` over a bounded Y range and log top Y/material/delta from procedural terrain, paired with existing `RenderingSurfaceCoverageDiagnostics` over that building's semantic bounds. Scene target policy stays in Kentridge composition; storage and renderer ownership stay behind existing shared query APIs. No geometry/material/camera change until this discriminator selects an owner.

## Remaining gates
1. Prove all four readable blockouts at Moordell, Rossdam, Fairy Village, and Orc Village with streets/open space and road arrival/exit in full-resolution built-player evidence.
2. Preserve player-height road/CharacterMotor evidence; capture substantial Rossdam water plus constrained route, Southern Ridge/pass, differentiated countryside, and final network overview inside the fixed 60 s replay.
3. Measure vertical-residency count and final world-build/route/feature/CPU/FPS/memory/streaming/render/far-field cost against budgets.
4. Re-fetch current master and re-check exact feature diff before final targeted CI.
5. Only after every checkbox and exact-SHA gate is green: move `open -> closed`, set `status=fixed`/`resolvedUtc`, merge current master, and non-force push that exact feature head to master; fetch/merge/retry if master advances.
