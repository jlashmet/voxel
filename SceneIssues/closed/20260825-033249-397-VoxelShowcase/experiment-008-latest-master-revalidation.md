# Experiment 008 — latest-master integration revalidation

## Hypothesis

Integrating the newer `master` state into the persistent agent-1 feature branch does not reintroduce VoxelShowcase's legacy Kentridge catalogue route or invalidate the focused WorldBuilder authoring-boundary regression that guards the captured floating-tower fix.

## What I performed

- Started from the previously terminal agent-1 head `dda85901d922f1cf0a5927c3837e5ca3cbd0d8e9`.
- Observed that `master` had advanced to `12321283d5e8efeb848c67e4d47ffa637bbb98c3`, 10 commits ahead of that head.
- The intervening master changes included production code under `Assets/Game/WorldBuilder/Generation/Voxel/KentridgeTerraceSurfaceCorrection.Program.cs`, so they were treated as relevant tested input rather than bookkeeping-only drift.
- Integrated that master tip into `fixes/agent-1` with normal merge commit `333db7f7776606596fc5306b38bf6de70744aced`, preserving both histories.
- Force-reset only the assigned request branch `ci-test/fixes/agent-1` to exact source head `333db7f7776606596fc5306b38bf6de70744aced`.
- Requested exactly `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` on EditMode.
- Targeted request commit: `72d4820e4c19909051feec2c74284077105a82a7`.
- Actions run: `32937676260`; status context: `ci/single-test`.

## Result

**Passed.** `ci/single-test` reached `success` on the integrated source head. The newer WorldBuilder production changes therefore did not invalidate the causal fix or its focused boundary regression.

The previously recorded exact-pose replay A/B remains the visual verification evidence for the same production fix. No new capture was started during this continuation.

## What was learned

The floating-tower fix remains valid after integrating current master, including the newly landed WorldBuilder terrace-generation change. The persistent feature branch can be promoted again without changing the original production fix or terminal `issue.json` fields.

## Next

Promote this terminal bookkeeping through current `master`, verify remote master contains the original fix plus this revalidation record, confirm the capture remains absent from `SceneIssues/open/` and present only under `SceneIssues/closed/`, then stop without starting another capture.
