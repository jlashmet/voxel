# Plan — Kentridge world-builder ownership

## Goal
Consolidate Kentridge so the showcase and other consumers author the town through one World Builder API, with the legacy Mountain Force World Generation implementation owned by the Game/Voxel Engine architecture rather than a parallel package path.

## Scope
- Assigned capture only: `20260825-040805-194-VoxelShowcase`.
- Inventory the current Kentridge, Mountain Force World Generation, and World Builder call paths before changing production code.
- Add a focused regression that proves the canonical authoring path/ownership invariant.
- Make the smallest production change that removes the duplicate/parallel path without changing unrelated scene behavior.

## Constraints
- Work only on `fixes/agent-1` and `ci-test/fixes/agent-1`.
- Do not create or start another scene capture.
- Preserve the original capture evidence unchanged.
- Connector-only validation uses the repository's targeted CI workflow; no local Unity execution.
- A verified fix is closed only after the focused CI request succeeds and the final diff/architecture is reviewed.

## Acceptance criteria
- Kentridge has one canonical town-authoring implementation/path.
- Legacy Mountain Force World Generation code needed by Kentridge is relocated/owned under the Game/Voxel Engine World Builder architecture rather than remaining a competing package-level authoring system.
- Scene/content consumers invoke town construction through the World Builder API rather than direct legacy package entry points.
- A focused regression guards the ownership/API invariant and passes `ci/single-test`.
- Terminal `issue.json` records the verified fix commit and regression test, and the capture moves from `SceneIssues/open/` to `SceneIssues/closed/` in a separate bookkeeping commit.

## Tasks
- [x] Read repository workflow and assigned capture metadata.
- [x] Confirm the assigned persistent feature branch is usable.
- [ ] Inventory Kentridge/Mountain Force/World Builder implementations and callers.
- [ ] Record the baseline architecture experiment.
- [ ] Add or extend a focused regression.
- [ ] Implement the smallest ownership/API consolidation.
- [ ] Run targeted CI and iterate until green.
- [ ] Review the final diff against `CLAUDE.md` and relevant specs.
- [ ] Record verification evidence and resolution details.
- [ ] Move the verified capture to `SceneIssues/closed/` in terminal bookkeeping.

## Initial findings
- The capture contains one frame and no circled sub-region; the issue note defines an architectural acceptance condition rather than a localized rendering blemish.
- No prior plan or experiment file was present in the capture directory when this assignment started.
