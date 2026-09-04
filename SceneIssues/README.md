# SceneIssues workflow

`SceneIssues` tracks captured defects and acceptance-driven features.

Use the assignment-specific guide:
- `SceneIssues/issue-readme.md` — capture-driven defects.
- `SceneIssues/feature-readme.md` — feature work.

`AGENTS.md` and this file define common rules. This file is the canonical coordinator-assigned SceneIssue workflow; automation prompts should point here instead of duplicating it.

## Queue state

`origin/master` is authoritative:
- `open/` — available or active work.
- `closed/` — completed work on master.

Keep blocked or unfinished work in `open/`. Do not invent intermediate queue states.

## Assignment and branches

Each worker uses:

```text
fixes/agent-N
ci-test/fixes/agent-N
```

Work only on the assigned task. Fetch first; create the feature branch from current `origin/master` or resume valid work. Merge current master when compatibility requires it and always before final promotion. Do not self-select work or modify another SceneIssue.

For captured defects, publish new captures to `master` before assignment; never introduce them on worker or CI branches.

## Module-local test and validation ownership

Every affected module/assembly owns its focused regression surface. Module-local EditMode/unit tests belong under that module's test assemblies, and every module with meaningful player-visible/runtime behavior must own one or more focused test/validation scenes physically under its own `<Module>/Validation/` directory.

- The scene must exercise the owning module through the real production authoring, composition, rendering, material, interaction, and runtime path that the shipped game uses.
- Keep the scene minimal and deterministic. Test-only setup may supply bounded inputs, camera/player setup, instrumentation, and assertions; it must not create a parallel renderer, fake art stack, or duplicate production logic.
- Add a module-local `*.player-scenario.json` when the scene needs runtime actions, timing, captures, or runtime assertions. The scenario describes behavior only; repository structure owns registration.
- A top-level showcase, gallery, `VoxelShowcase`, `KentridgePlayableSlice`, or another module's validation scene is integration evidence and does not satisfy this module-local ownership requirement.
- If a SceneIssue changes player-visible/runtime behavior in a module that lacks a suitable local validation scene, creating that scene is required in-scope work, not an opportunistic enhancement.
- A pure headless/domain module with no meaningful scene behavior may rely on module-local EditMode/unit coverage instead; the worker must state that exception and its rationale in the SceneIssue plan.

## Targeted CI

Commit production/test work to `fixes/agent-N`. `ci-test/fixes/agent-N` is that worker's only targeted-CI transport. Build each request from the exact feature SHA; change `.github/test-request.json` only on the CI branch.

Never replace queued/running CI. After a completed failure, inspect evidence, fix the product failure or retry proven infrastructure failure, then reuse the same transport. Do not create alternate CI branches, no-op commits, custom workflows, or permission probes.

The explicit request remains the exact-SHA trigger and optional focused regression. For production diffs, targeted CI may additionally derive repository-owned module validation from repository structure. Agents must not manually enumerate automatically required module/player targets. Required zero-match tests, missing/ambiguous scenes/scenarios, missing captures, skipped required player targets, or failed required artifact proof are failures.

Use the smallest regression that proves the change. Player-visible acceptance requires exact-SHA built-player validation and durable evidence; editor/unit tests are supplemental. PlayMode screenshots or RenderTextures are not visual acceptance evidence.

## Final pull-request gate

`master` is protected. Do not push SceneIssue feature heads directly to `master` and do not attempt to bypass the ruleset.

After the assignment-specific exact-SHA validation is green and closure bookkeeping is committed, the worker must open a pull request from `fixes/agent-N` to `master` and enable auto-merge. The required PR gate is the `affected` job in `.github/workflows/tests-pr.yml`; it must run:

1. the affected EditMode/unit-test assemblies selected from the production diff; and
2. the canonical standalone `KentridgePlayableSlice` full-application test through the real player-build path.

General automatic PlayMode suites are intentionally not part of the merge gate; the old suites were removed because they were stale. Add or run a narrowly scoped PlayMode regression only when a specific acceptance criterion requires it. The standalone full-application test is the repository-wide high-level gate.

Do not create a PR merely as an alternate targeted-CI transport. The PR is the final integration mechanism after the feature branch has already satisfied its issue-specific validation.

## Completion and merge

Before closure, satisfy every acceptance criterion and required checklist item. Complete `resolutionSummary`, `regressionTest`, and `fixCommit` when supported by the verified result.

After all required exact-SHA gates pass, move only the assigned task from `open/` to `closed/`, set `status: fixed` and `resolvedUtc`, and commit the bookkeeping on the feature branch.

Then:

1. Fetch current `origin/master` and merge it into the feature branch; resolve only in-scope conflicts.
2. Push the feature branch.
3. Open or update a pull request from that feature branch to `master`.
4. Enable auto-merge immediately; do not wait for the coordinator to merge it manually.
5. Monitor the PR until the required `affected` gate completes. If `master` advances and strict checks require an updated branch, merge current master again, push, and let the PR checks rerun.
6. Treat the assignment as complete only after the PR is merged and the closed SceneIssue is visible on `origin/master`.

Never force-push `master`. A failed required PR check is a real blocker: inspect and fix the cause, or retry only a proven infrastructure failure according to the CI rules above.