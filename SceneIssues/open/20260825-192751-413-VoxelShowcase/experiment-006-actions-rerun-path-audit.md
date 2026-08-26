# Experiment 006 — audit Actions reruns as a current-head validation path

**Hypothesis** — GitHub's available Actions rerun operation might execute an existing workflow against the current `ci-test/fixes/agent-2` head, providing legitimate Unity evidence even though connector-authored pushes do not emit a new workflow event.

**What was performed** — Audited the Actions controls and relevant workflow definitions from feature head `9ce0f11f8bf74805cc934ae11c7eabea21586f2d` with current request head `9ce7b8049f65108bf2134679f35f26a98f1cc161`. The connector exposes rerun operations but no workflow-dispatch/start operation. The historical `Tests (single)` workflow uses the event checkout with no branch override, so a rerun remains pinned to its old request SHA. One historical replay workflow explicitly checks out current `ci-test/fixes/agent-2`, but it is hardcoded to capture `20260824-221508-896-VoxelShowcase`. The historical GPU oracle one-shots explicitly check out the retired shared `fixes` branch and write evidence into the old `20260823-014011-920-VoxelShowcase` capture.

**Result** — No existing rerunnable job can validate this capture's current source/request while staying on the coordinator-assigned branches and capture. Reusing the old replay/oracle jobs would either test another capture, test the retired shared branch, or attach status to an old event SHA. No rerun was started.

**What was learned** — Actions rerun is not a valid substitute for the missing push-triggered `ci/single-test` run. The blocker is specifically the inability to create a new accepted Actions event for request `9ce7b8049f65108bf2134679f35f26a98f1cc161`, not lack of read access to workflows or lack of an existing runner.

**Next** — Keep this issue open. Resume only when `ci-test/fixes/agent-2` can receive a normal authenticated push that emits `Tests (single)`, or when an authorized workflow-start capability becomes available. Then require `ci/single-test=success`, run current-head GPU oracle coverage as warranted, and replay/benchmark this exact capture before terminal bookkeeping.
