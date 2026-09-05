# Investigation and coordinator protocol

## First-wave boundaries

These assignments answer a question, not fix the renderer. Change only the assigned SceneIssue’s documentation and evidence. No product/test/scene/budget/configuration edits, new workflows, custom player-build commands or changes to another issue. Reuse existing production players, orchestration, diagnostics and tests. A missing production-faithful fixture is a prerequisite to report, not permission to make fake terrain/props/materials. Coordinator creates that implementation issue only after reviewing the gap.

Start with `git fetch origin`, record master SHA, and use the assigned `fixes/agent-N` / `ci-test/fixes/agent-N` refs per `SceneIssues/README.md`. Do not use the coordinator’s divergent local feature branch or share a worker ref. Parallel means independent analysis; do not start competing Unity editors or replace queued/running CI. Use the existing request workflow only when an actual experiment requires it. Targeted tests must finish within five minutes after starting. Local Unity requires developer-editor checks/permission and `tools/unity-run.sh`.

Each issue targets 30–60 minutes active work, excluding CI admission/build waits. At the timebox, preserve useful results and the exact blocker; do not broaden into an implementation epic. Missing experiments keep the issue open. A valid experiment proving a defect is a completed investigation; it is not a repaired product or a green failed run. Required PR gates still must pass before merge. For documentation-only closure, mark runtime regression as not applicable and describe the actual document/evidence checks; never invent a passing test.

## Evidence contract

`findings.md` should contain: question; measured/observed/inferred status; source commit and relevant code path; exact command/scenario/pose/configuration; result and falsified hypothesis; limitations; smallest next experiment or repair scope. Keep the plan under 500 words. For player evidence retain build/transport SHA, workflow/run/artifact links, device/API/resolution/FOV, effective backend, source revisions and timestamps. Commit durable critical captures/log excerpts in the issue or use the repository’s durable artifact convention; record filenames and provenance. Do not treat expired links or failed/timed-out run artifacts as passing acceptance.

Inspect exact-player images and classify `production-quality`, `acceptable but improvable`, `prototype/blockout quality`, or `unacceptable`. Only production-quality passes product visual acceptance. An investigation can successfully document an unacceptable product. Existing unit tests, counters and semantic signatures support diagnosis but cannot establish visual finish. Fog, reduced radius, CPU fallback or empty scenes cannot hide the tested failure.

Use device-matrix numbers for budget comparisons. Report frame and renderer time separately, percentiles from actual samples, queue/upload/committed memory where relevant, and unknown measurements explicitly. Short runs cannot prove two-hour memory flatness or Mobile-HE 20-minute thermal sustain. A static scene cannot prove destruction/traversal performance or km-scale density.

## Thirty-minute coordinator review

At each review fetch current master. Compare against the last reviewed SHA; identify newly closed R issues, changed open findings/blockers, and changes to the existing GPU restoration issue. Review blocked open results too so a missing fixture or instrumentation can receive a prerequisite issue without waiting for impossible closure. Review only this rendering work, not unrelated game tasks. Record reviewed master/issue/fix SHAs, evidence paths, verdict and any successor ID in `review-log.md`. Skip already-reviewed unchanged results.

For each closure inspect merged source changes, checklist, full CI conclusions/nonzero tests, exact-player provenance, production fidelity, artifacts and applicable budgets. Distinguish reusable architecture from rendered quality. If evidence is unavailable, record review blocked. If a claim is inadequate, publish a new uniquely identified open SceneIssue with plan/tasks, concrete defect, two hypotheses, evidence links, acceptance and a 30–60-minute scope. Do not change old closure history, duplicate an open follow-up, weaken checks or implement fixes as coordinator.

After sufficient reviewed results, write the backend/far-representation decision and create only the now-supported next independent tasks. Unresolved prerequisites block their dependent tasks. Keep authority, discrete collision and simulation/interest policies unchanged.

Scheduling status at creation: not installed. No scheduling tool is exposed in this session; computer-use access to the ChatGPT/Codex app is denied. This document is a resumable review procedure, not a running timer. The coordinator must not claim recurring reviews are active until a real scheduler or continuing execution is verified.
