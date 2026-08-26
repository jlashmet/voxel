# Experiment 009 — master 22dc revalidation

## Hypothesis
The floating-tower fix remains valid after integrating the current `master` tip, even though newer production/test changes landed for grass rendering and live character interaction.

## What was performed
- Prior verified terminal source: `5207dd98aa0dfa92f00473bf68ce3db008e91789`.
- Current master source: `22dc141b3374940d6d8b1ffde3b79085f10fda8d`.
- `master` was 21 commits ahead; the compare showed production/test changes in character equipment and procedural vegetation, but no changes to the Kentridge/WorldBuilder generation route, `VoxelShowcase` scene data, or `WorldBuilderAuthoringVisibilityTests` regression.
- Fast-forwarded persistent `fixes/agent-1` to `22dc141b3374940d6d8b1ffde3b79085f10fda8d` without discarding history.
- Force-reset only `ci-test/fixes/agent-1` to that exact source commit, then added request commit `b5e7fa14f2526bfebbcdc6c38cb697e28c4e4490`.
- Requested `VoxelEngine.Tests.EditMode.WorldBuilderAuthoringVisibilityTests.KentridgeTownAuthoringUsesOnlyWorldBuilderPublicBoundary`.

## Result
GitHub Actions run `32943261347` completed successfully. The `Run requested test` step passed and commit status `ci/single-test` is `success`.

The earlier exact-pose A/B replay evidence remains the visual evidence for the tower itself; the intervening commits did not change Kentridge/WorldBuilder generation or the VoxelShowcase scene input, so no new capture was created.

## What was learned
Hypothesis confirmed. The single WorldBuilder-authored Kentridge route remains protected on the current integrated repository state, and the resolved floating-tower capture remains valid after the newer grass/character changes.

## Next
Promote this durable revalidation bookkeeping to current `master`, verify the original fix and terminal bookkeeping remain ancestors of the final master tip, confirm the capture exists only under `SceneIssues/closed/`, and stop without starting another capture.
