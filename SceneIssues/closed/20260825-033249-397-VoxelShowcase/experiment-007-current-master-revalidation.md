# Experiment 007 — current-master integration revalidation

## Hypothesis

Merging current `master` into the persistent feature branch does not reintroduce the legacy VoxelShowcase Kentridge catalogue route or invalidate the focused authoring-boundary regression that guards the captured floating-tower fix.

## What I performed

- Integrated current `master` (`76928b0990c1d9bb125eabac0df2dc095de960cf`) into `fixes/agent-1` with normal merge commit `2ee781d38164a3cb4c41f5174618cbbccfefb83d`, preserving both histories.
- Confirmed the integrated feature head still contains `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` and the WorldBuilder-owned VoxelShowcase authoring path.
- Force-reset only the assigned request branch `ci-test/fixes/agent-1` to exact source head `2ee781d38164a3cb4c41f5174618cbbccfefb83d` as required by the repository CI protocol.
- Requested exactly `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary` on EditMode.
- Targeted request commit: `2b29d80643444b9140fcf866efc97edbc0df185b`.
- Actions run: `32927945599`; status context: `ci/single-test`.

## Result

**Passed.** `ci/single-test` reached `success`, and the requested Unity test step completed successfully on the integrated source head. The master refresh therefore did not invalidate the causal fix or focused regression.

The previously recorded exact-pose replay A/B remains the visual verification evidence for this same production fix. No new capture was started during this continuation.

## Next

Promote the verified terminal feature branch to current `master`, verify remote `master` contains fix commit `416522e1816fd4e6a315f9831e523156304e1c18`, the terminal closure commit, and this revalidation record, confirm the capture exists only under `SceneIssues/closed/`, then stop without starting another capture.
