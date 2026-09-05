# Astra manager supervisor

This directory implements a low-context, stateless management loop around the existing SceneIssue agent system.

> Scripts determine what changed. Astra determines what it means. Agents implement the work.

The manager runtime is intentionally untracked at `SceneIssues/manager/runtime/`; it is durable in the manager checkout but does not dirty `master` or require a PR merely to advance a review cursor. Use a dedicated persistent checkout that stays on local `master` for the manager loop.

## One iteration

Use the canonical entrypoint:

```bash
python3 tools/astra_manager_loop.py --fetch
```

Before collecting anything, the loop fetches origin and fast-forwards the dedicated local `master` checkout to `origin/master`. That keeps file-based reads of `issue.json`, `plan.md`, and `tasks.md` aligned with the Git SHA being reviewed. The loop refuses tracked local changes or unexpected untracked files rather than silently reviewing a mixed/stale checkout.

The deterministic collector then:
1. compares current `origin/master` with the last collected SHA;
2. summarizes active SceneIssues and `fixes/agent-N` branches;
3. optionally reads recent GitHub Actions status through the authenticated `gh` CLI;
4. generates completion packets only for closed issues changed since the cursor;
5. detects high-signal events (closures, acceptance changes, core/shared deltas, stale branches, repeated CI failures, declared known limitations);
6. appends those signals to a persistent pending-review queue;
7. exits without Astra when there is nothing meaningful to review or the normal batching window has not elapsed.

The loop mechanically applies `config.json.reviewBudget` and writes `runtime/review-window.md`. Astra sees only that bounded slice. The remaining backlog stays in local `state.json` and is not loaded into the model session.

The first run bootstraps at current master and intentionally does **not** enqueue the historical backlog. To review from an older point, use `python3 tools/astra_manager.py bootstrap --from-sha <sha>` before the next loop check.

## Direct Codex CLI wake-up

No external Astra launcher is required. When `signal.json.wakeAstra` is true, the canonical loop launches a fresh non-interactive Codex session directly:

```text
codex exec
  --ephemeral
  --ignore-user-config
  --model gpt-6-astra
  --sandbox workspace-write
  --config model_reasoning_effort="low"
  --config approval_policy="never"
  --config sandbox_workspace_write.network_access=false
  --config web_search="disabled"
```

The prompt is supplied on stdin and points Codex only at `WAKEUP_PROMPT.md` and the mechanically bounded `runtime/review-window.md`. `--ephemeral` means the manager session is not persisted/resumed. User config is ignored for the manager pass so unrelated MCP/tools/settings do not expand the launch context; normal Codex authentication is still used.

`SceneIssues/manager/config.json` owns the Codex model, minimum CLI version, reasoning effort, sandbox, approval policy, network access, and web-search policy. The checked-in default is `gpt-6-astra` with low reasoning. Astra requires Codex CLI `0.153.0` or newer. Install/update Codex and sign in with ChatGPT once before scheduling the loop.

The explicit `--wake-command` argument and legacy `ASTRA_MANAGER_WAKE_COMMAND` environment variable remain available only as override seams for testing/emergency compatibility. Normal operation uses Codex CLI directly.

## Astra output and deterministic finish

The Codex/Astra process is decision-only. It writes:

`SceneIssues/manager/runtime/decision.json`

and exits. It does **not** run finish/publish itself.

After Codex exits successfully, the outer loop:
- verifies Codex did not modify tracked or unexpected untracked repository files;
- requires a fresh `decision.json`;
- calls the deterministic `tools/astra_manager_finish.py` boundary;
- rejects duplicate reviewed keys or keys outside `signal.json.selectedReviewKeys`;
- creates only standard `issue.json + plan.md + tasks.md` follow-up metadata;
- publishes new manager follow-ups through protected-master PRs with auto-merge;
- exits immediately without waiting for CI, merge, assignment, or implementation.

`tools/astra_manager_publish.py` uses an isolated temporary Git index based on current `origin/master`, so the publication commit contains only manager-created SceneIssue files and never implementation changes. Publication is retry-safe: a created PR is recorded before auto-merge is attempted. The cheap outer loop retries unpublished or auto-merge-incomplete follow-ups on later checks without waking Astra.

While a follow-up PR is pending, its local untracked SceneIssue remains visible to duplicate prevention. After the PR reaches `origin/master`, the next canonical loop removes the matching local copy before fast-forwarding. Once the follow-up reaches master, the normal agent allocator owns implementation.

## Runtime files

`SceneIssues/manager/runtime/` contains:

- `state.json` — collection/review cursor and the complete pending queue; not normal Astra input;
- `signal.json` — whether a manager wake is currently due and which keys were selected;
- `digest.md` — complete deterministic current/delta summary for diagnostics; not normal Astra input;
- `review-window.md` — the bounded minimal packet Astra reads on wake-up;
- `open-issue-index.md` — duplicate-prevention index, read only before proposing follow-up work;
- `packets/*.md` — compact completion packets, read only for selected completions;
- `decision.json` — one Astra pass's bounded output;
- `history/*.md` — compact local audit records;
- `published-followups.json` — pending/published manager follow-up PR bookkeeping.

No chain-of-thought or investigation diary is stored.

## Useful commands

```bash
# canonical cheap loop; launches Codex + GPT-6 Astra only when due
python3 tools/astra_manager_loop.py --fetch

# run without optional GitHub Actions queries
python3 tools/astra_manager_loop.py --fetch --no-ci

# raw collector only; diagnostic use (assumes worktree already matches origin/master)
python3 tools/astra_manager.py check --fetch

# reset/bootstrap the collection cursor
python3 tools/astra_manager.py bootstrap --fetch
python3 tools/astra_manager.py bootstrap --from-sha <sha>

# deterministic decision finish (normally called automatically by the loop)
python3 tools/astra_manager_finish.py

# deterministic publication/retry only
python3 tools/astra_manager_publish.py
```

## Scheduling

Keep scheduling outside Astra. Use cron, launchd, or another cheap local scheduler to execute `python3 tools/astra_manager_loop.py --fetch` periodically in the dedicated manager checkout. The scheduler can run hourly; Astra itself normally wakes no more often than the configured batching window unless you intentionally change the policy.

No repository scheduler is checked in because the host path and desired cadence are machine-specific; Codex invocation itself is now built into the loop.

## Tuning

Start with the checked-in defaults. After several days, tune `batchHours`, stale-agent threshold, review budgets, large-diff threshold, core path patterns, and `codex.reasoningEffort` based on observed wake frequency and usage. Prefer improving deterministic filtering over giving Astra a larger bootstrap context.
