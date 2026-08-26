# Experiment 003 — targeted CI push trigger is unavailable from connector-authored commits

**Hypothesis** — Reissuing the required single-test request on the persistent `ci-test/fixes/agent-2` branch would trigger `Tests (single)` and provide `ci/single-test` evidence for the restored GPU cutover.

**What was performed** — Against source commit `0fcaf3b98b92f4906c2027dd0b9104d664e01f90`, force-reset `ci-test/fixes/agent-2` to that exact commit and created fresh request commit `6c0317b29b13f165235c936f31565e113f0eab25` for `VoxelEngine.Tests.EditMode.GpuLod2CutoverPolicyTests.SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings`. The earlier request commit `1ad552ec0d42187eb660525848b8815bc3aa7297` was also checked.

**Result** — Neither connector-authored request commit produced a `ci/single-test` commit status or a GitHub Actions workflow run. Repository history on the same branch shows older human-authored push commits did trigger `.github/workflows/tests-single.yml`, so the request branch/workflow itself is valid; the missing event is specific to this connector-authored push path.

**What was learned** — The production fix cannot be declared verified from this session. The required remote Unity test and subsequent replay/performance verification cannot be truthfully completed until a push event that GitHub Actions accepts is made on the mandated CI branch.

**Next** — Keep the capture under `SceneIssues/open/` with `status: open`. Resume by issuing the same targeted request through an authenticated git push path that triggers Actions, then inspect `ci/single-test`, run the GPU oracle/arena coverage as needed, and replay/benchmark the original VoxelShowcase capture before terminal bookkeeping.
