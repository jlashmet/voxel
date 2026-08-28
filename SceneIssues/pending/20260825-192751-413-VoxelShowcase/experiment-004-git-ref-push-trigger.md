# Experiment 004 — low-level Git ref push still does not trigger targeted CI

**Hypothesis** — The connector's Contents API might suppress GitHub Actions while a low-level Git commit plus ref update on `ci-test/fixes/agent-2` could still emit the repository's required `push` event.

**What was performed** — Confirmed `fixes/agent-2` is current with `master` (`master` is its merge base; feature is 21 commits ahead and 0 behind). Against source commit `793dfffed3cb890b8d1eb69b3152f46c729f6f36` (which contains production fix `0fcaf3b98b92f4906c2027dd0b9104d664e01f90` and only SceneIssue documentation afterward), force-reset `ci-test/fixes/agent-2` to that source, created request commit `9ce7b8049f65108bf2134679f35f26a98f1cc161` for `VoxelEngine.Tests.EditMode.GpuLod2CutoverPolicyTests.SceneIssue20260825192751413ProductionGpuCutoverIsEnabledForExactRings`, and advanced the CI ref with the Git refs API rather than the Contents API.

**Result** — The request commit has no `ci/single-test` status, and the Actions runs query for its exact `head_sha` returns `total_count: 0` after repeated checks. Thus both connector write paths available in this session fail to emit the push-triggered `Tests (single)` workflow.

**What was learned** — The blocker is not request JSON shape, branch naming, source ancestry, or the Contents API specifically. This session lacks a write path that GitHub Actions treats as the required push event, and no workflow-dispatch action is exposed by the connected GitHub capability.

**Next** — Keep the capture open. Resume only when an authenticated git push path or workflow-dispatch capability that actually starts `.github/workflows/tests-single.yml` is available; then require a green `ci/single-test`, perform any needed GPU oracle/arena coverage, and replay/benchmark the original capture before terminal bookkeeping.
