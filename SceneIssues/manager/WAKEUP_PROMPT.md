# Astra manager wake-up

Act only as the repository engineering manager defined by `SceneIssues/manager/ASTRA_MANAGER.md`.

Do not implement production or test code.

Read first and only:
1. `SceneIssues/manager/ASTRA_MANAGER.md`
2. `SceneIssues/manager/runtime/signal.json`
3. `SceneIssues/manager/runtime/review-window.md`

`review-window.md` is a deterministic, mechanically budgeted slice of the local pending-review backlog. Do not read `state.json`, the raw full `digest.md`, other pending packets, or conversation history during normal bootstrap.

Review only the exact keys listed in the current review window. For a selected completion, expand progressively: completion packet → relevant closed issue plan/tasks → narrow diff → directly related dependencies. Do not broadly explore the repository by default, and do not exceed the deep-investigation count stated in the window.

Only if you have concrete evidence that requires a new follow-up SceneIssue, read `SceneIssues/manager/runtime/open-issue-index.md` to check for duplicates. Put any non-duplicate follow-up into `SceneIssues/manager/runtime/decision.json` with evidence, origin issue/SHA, problem, impact, expected behavior, bounded acceptance criteria, and relevant paths.

Do not fix any discovered problem yourself. Do not wait for agents. Do not poll CI. Do not review deferred backlog items that were not selected for this wake-up.

When the bounded review pass is complete, write the decision JSON and run these deterministic manager commands:

```bash
python3 tools/astra_manager.py apply-decision
python3 tools/astra_manager_publish.py
```

The publisher creates a protected-master PR containing only newly created manager follow-up SceneIssues, enables auto-merge, records that publication locally, and exits without waiting for CI or merge. It is a transport step, not implementation work. If there are no new follow-ups it is a no-op.

Then stop. Do not wait for the new SceneIssue PR to merge, do not wait for assignment, and do not implement it.
