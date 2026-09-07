# Experiment 002 — cold standalone build memory guard

## Hypothesis

The corrected VoxelShowcase replay is failing in the capture harness before product validation because the cold Unity player build exceeds an overly tight process-tree RSS guard, rather than because the project has a C#/shader/Burst compile failure.

## Exact source and request

- Feature source under test: `3d6e605786d10275d3bcc0ff7f736f84a0a7d127`
- CI request: `cd4c595b392c3279168bdd86fef9a78e6342d8ea`
- Workflow run/job: `34151335102` / `101834086315`
- Unity: `6000.5.6f1`
- SceneIssue path: `SceneIssues/open/20260907-145211-000-VoxelShowcaseGpuCorrectnessAndPerformance/issue.json`

## Evidence

The request path was accepted and `Replay SceneIssue through standalone player` entered the real production player-build path. The runner reported about 28,348 MB free before launch. `tools/unity-run.sh` started with a 12,288 MB process-tree RSS ceiling and the repository's 8,192 MB free-memory floor. After about 82 seconds of cold import/build work, while the build log was still compiling project shaders including `VoxelBrickMesher`, the wrapper terminated the process tree at 12,311 MB. The job exited 5 before a player executable or screenshots existed.

No C#, shader, or Burst compiler error preceded the termination. The automatically run repository tooling suite completed 97/97 tests before the player replay. This run therefore establishes a bounded harness/resource failure, not renderer correctness or incorrectness.

## Selected repair

Raise only the standalone capture build's default `UNITY_MAX_RSS_MB` from 12,288 MB to 14,336 MB. This remains guarded by `tools/unity-run.sh`, preserves the 8,192 MB free-memory floor and swap-growth protection, and matches the 14,336 MB envelope already used by the same targeted workflow for focused Unity test execution. It does not change player/runtime memory budgets, rendering capacity, content, or quality.

Add a tooling regression that intercepts the existing `unity-run.sh` invocation and proves the capture harness supplies the 14,336 MB default while retaining the production build arguments and frame-timing flag.

## Verdict and next step

H0 (product compile failure caused this replay) is not supported by this run; the process was deliberately killed by the harness guard first. The narrow harness fix is committed on `fixes/agent-3`. Request a new exact-SHA replay from the resulting feature head on `ci-test/fixes/agent-3`; only that replay can establish fresh compile and standalone baseline evidence.
