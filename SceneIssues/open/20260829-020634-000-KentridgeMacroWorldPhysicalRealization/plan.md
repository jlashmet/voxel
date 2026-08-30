# Plan

## Observed behavior / acceptance
`captures` is empty, so `issue.json` is the acceptance contract. The source-backed Kentridge macro graph must physically realize every settlement, continuous terrain-aware hard routes, reusable regional geography, a substantial lake/ridge with semantic route response, and real CharacterMotor traversal. Closure also requires exact built-player visual evidence and measured cost. `SceneIssues/feature-readme.md` is absent; `SceneIssues/README.md` is the workflow authority.

## Material results / hypotheses
1. Macro generation is missing — rejected. Production acceptance repeatedly yields 20 hard routes, 16 generic buildings, 5 constrained routes, and max road rise 2 voxels.
2. Persisted settlement payload never reaches rendering — rejected. The real boundary is two-stage terrain then feature publication; the presentation-column readiness regression proves the later feature commit/invalidation and persisted Moordell timber.
3. Broad residency readiness is acceptable — rejected. It delayed opening ~40 s and was too expensive.
4. Scoped readiness plus survey framing is enough — partly confirmed. Run `33289185080` made Moordell shells visible but only two were readable at 22 m and the 60 s replay stalled before later targets.
5. Serially pinning each building centre will prestream evidence efficiently — **rejected by run `33290154012` / artifact `9725740286` (source `8cab72cc...`)**. CI/test/player all report success, but the 60 s player log reaches only `moordell index=0`; screenshots are opening/pub plus road traversal, not the required settlements/geography.
6. The 12x validation opening boost shortens the dialogue — **rejected**. `TryAdvanceDialogue` intentionally uses `Time.realtimeSinceStartup`, so `Time.timeScale=12` does not shorten auto-dialogue waits. The player remains in the opening until ~40 s.

## Selected fix / next discriminator
- Keep normal gameplay, streaming radius, generation budgets, physical plan, roads, settlement geometry, and 60 s replay unchanged.
- In the dormant `kentridge-macro-world` evidence driver only, dismiss pending opening dialogue as soon as each line is presented; retain the existing accelerated cutscene timeline and restore `Time.timeScale=1` before CharacterMotor evidence.
- Remove serial content-centre demand churn. Hold the production-derived settlement survey demand and wait for all four building-centre presentation columns to settle under ordinary `ShowcaseWorld.StepStreaming`; then require four stable renderer-coverage frames before capture.
- Preserve the production readiness regression and exact physical/storage acceptance.

## Blast radius / cost
Current `fixes/agent-6` includes master `0901be5a...` and only this feature/tests/docs in the feature diff. The remediation is validation-driver-only; no runtime budgets/radius, CharacterMotor, world topology, or unrelated SceneIssue changes. Existing measurements: 20 routes, 833 route tiles, 16 generic buildings, 1,090,380 feature voxels in the scoped readiness regression; failed player peak 5859 MB RSS, zero swap growth. Final exact player telemetry remains mandatory.

## Remaining gate
Implement the validation scheduling correction, update focused evidence if needed, refresh from current master, issue one exact-SHA request on `ci-test/fixes/agent-6`, and inspect full-resolution artifacts. Close only if all four generic settlements, roads/network, lake, ridge/pass, constrained route, CharacterMotor traversal, no runtime exceptions, and budget evidence are visibly/behaviorally proven.