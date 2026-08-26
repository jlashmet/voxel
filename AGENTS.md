# Voxel development instructions

These instructions apply throughout `jlashmet/voxel`. Inspect the relevant implementation, tests,
specs, and existing plan before editing. Prefer proven causes and durable invariants over speculative
changes.

## Architecture

The active feature is [World Feature Authoring](specs/002-world-feature-authoring/plan.md), built on
the [destructible voxel engine](specs/001-destructible-voxel-engine/plan.md). The project
[constitution](.specify/memory/constitution.md) is binding, and the
[device matrix](specs/001-destructible-voxel-engine/device-matrix.md) is authoritative for numeric
budgets.

- Authoritative state is deterministic integer CPU/Burst work; never derive it from GPU output or
  floating point.
- Visuals and collision derive from the same voxel cells. Collision uses discrete occupancy;
  curvature is presentation only.
- The server is authoritative; client prediction is presentation.
- Device tiers may change presentation, never world truth, interest radius, tick rate, collision,
  or `Core` jobs. Supported mobile targets are high-end only.
- Use Burst, Collections, Jobs, and custom Unity Transport replication. Do not add
  `com.unity.entities` or Netcode for GameObjects.

## Planning

For nontrivial work, keep one durable Markdown plan beside the work. Resume it instead of creating
a duplicate. Keep it short and current—normally no more than 500 words—with:

- observed behavior and acceptance criteria;
- two plausible hypotheses and the next discriminating experiment;
- material results, including falsified hypotheses;
- selected fix and remaining validation gates.

Replace obsolete detail with a one-line result instead of growing an investigation diary. Chat is
not the durable record. SceneIssue plans and evidence follow the canonical
[SceneIssue workflow](SceneIssues/README.md).

## Branches and CI

For ordinary work, use one feature branch and `ci-test/<feature-branch>` for its targeted request.
Reuse those refs for the task; do not create retry, baseline, temporary, probe, or no-op branches.
Do not create custom workflows or pull requests merely to trigger CI.

Create the request commit directly on the exact feature SHA, changing
`.github/test-request.json` only on the CI branch, then force-update that CI ref once. Monitor the
exact request SHA. Leave queued or running work alone. A missing run may be replaced only after the
documented admission window, and only once. Product failures require a fix; runner contention,
delayed admission, an open interactive editor, or native import crashes are infrastructure results.

Targeted tests must finish within five minutes after starting. Use the smallest behavioral test
that proves the invariant; a source-string assertion or a zero-test run is not sufficient evidence.
Never call a failed, cancelled, or timed-out run successful because it produced an intermediate
artifact.

Coordinator-assigned SceneIssues have stricter branch, evidence, closure, and batched-promotion
rules. Follow [SceneIssues/README.md](SceneIssues/README.md); it is the sole workflow authority for
those tasks.

## Running Unity locally

Never invoke Unity directly. Use `tools/unity-run.sh`, which prevents a second editor from freezing
the shared-memory Mac and enforces memory/time limits. Ask before running Unity when the developer's
editor might be open.

Batchmode PlayMode tests do not cover editor lifecycle behavior such as repeated `OnEnable`, domain
reloads, or renderer-feature creation. Test those cases in EditMode by looping the lifecycle (see
`Assets/Tests/EditMode/RenderResourceLifetimeTests.cs`). Remote SceneIssue workers use targeted CI
and do not assume local Unity access.

## Completion

Review the final diff, confirm relevant tests actually passed, and report exactly what was
validated. Do not weaken performance budgets or unrelated assertions to make a test pass.
