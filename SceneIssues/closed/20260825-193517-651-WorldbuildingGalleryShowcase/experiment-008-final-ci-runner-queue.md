# Experiment 008 — final targeted CI runner queue

**Hypothesis**

The complete durable grass fix at `92515619e714ce3aa899f8eca884d342fadb890b` should pass the focused authored-grass regression after live character interactor wiring is included.

**What was performed**

- Force-reset `ci-test/fixes/agent-8` to production/test fix commit `92515619e714ce3aa899f8eca884d342fadb890b`.
- On that CI branch only, requested `VoxelEngine.Tests.EditMode.ProceduralVegetationGrassStyleTests.FoliageShaderImplementsAuthoredGrassMotionAndToonVariationContract` with request id `agent-8-20260826-grass-final-003`.
- Request commit: `949363fe0c62e39abc5c53f55e89c3edf2c28cfe`.
- Workflow run: `32933720105`; job: `98070821947`.
- Repeatedly inspected both the workflow job and `ci/single-test` commit-status context instead of treating a queued job as green.

**Result**

The request has not executed. GitHub reports the job as `queued`, with labels `self-hosted` and `macOS`, an empty step list, `runner_id: 0`, and no runner name. The request commit has no `ci/single-test` status because the workflow has not reached its first status-publishing step. After the previous self-hosted run released `Jasons-MacBook-Pro`, this agent-8 request remained the repository's sole queued workflow with no in-progress workflow visible.

**What was learned**

This is an infrastructure/runner-availability blocker, not a test failure and not evidence that the fix is green. Repository policy explicitly treats a missing status as queued/unstarted, so terminal issue closure and master promotion cannot be performed from this state.

**Next**

Keep the authoritative request unchanged so latest-request-wins does not cancel it. Once the self-hosted macOS runner claims it, inspect the non-zero Unity test result. If green, continue with the smallest affected existing character-controller regression, replay this same saved capture after the complete fix (without creating another capture), restore a clean final CI request, and only then perform terminal open-to-closed bookkeeping and master promotion.
