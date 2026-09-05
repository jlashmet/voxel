# Astra manager supervisor

This directory implements a low-context, stateless management loop around the existing SceneIssue agent system.

> Scripts determine what changed. Astra determines what it means. Agents implement the work.

The manager runtime is intentionally untracked at `SceneIssues/manager/runtime/`; it is durable in the manager checkout but does not dirty `master` or require a PR merely to advance a review cursor. Use a dedicated persistent checkout that stays on local `master` for the manager loop.

## One iteration

Use the budget-enforcing entrypoint:

```bash
python3 tools/astra_manager_loop.py --fetch
```

Before collecting anything, the canonical loop fetches origin and fast-forwards the dedicated local `master` checkout to `origin/master`. That keeps file-based reads of `issue.json`, `plan.md`, and `tasks.md` aligned with the Git SHA being reviewed. The loop refuses tracked local changes or unexpected untracked files rather than silently reviewing a mixed/stale checkout.

The deterministic collector then:
1. compares current `origin/master` with the last collected SHA;
2. summarizes active SceneIssues and `fixes/agent-N` branches;
3. optionally reads recent GitHub Actions status through the authenticated `gh` CLI;
4. generates completion packets only for closed issues changed since the cursor;
5. detects high-signal events (closures, acceptance changes, core/shared deltas, stale branches, repeated CI failures, declared known limitations);
6. appends those signals to a persistent pending-review queue;
7. exits without Astra when there is nothing meaningful to review or the normal batching window has not elapsed.

`astra_manager_loop.py` mechanically applies `config.json.reviewBudget` and writes `runtime/review-window.md`. Astra sees only that bounded slice. The remaining backlog stays in local `state.json` and is not loaded into the model session.

The first run bootstraps at current master and intentionally does **not** enqueue the historical backlog. To review from an older point, use `python3 tools/astra_manager.py bootstrap --from-sha <sha>` before the next loop check.

## Waking Astra

When a review is due and no launcher is configured, the loop exits with code `10` and prints only the prompt, bounded review-window, and decision-output paths. That is the integration point for your existing Astra/Work launcher.

To make the loop autonomous, set `ASTRA_MANAGER_WAKE_COMMAND` to your external launcher. The command runs only when `signal.json.wakeAstra` is true and receives:

- `ASTRA_MANAGER_PROMPT`
- `ASTRA_MANAGER_REVIEW_WINDOW`
- `ASTRA_MANAGER_DIGEST` — compatibility alias pointing to the same bounded review-window
- `ASTRA_MANAGER_DECISION_OUTPUT`
- `ASTRA_MANAGER_MASTER_SHA`

The launcher should start a **fresh** Astra session using `WAKEUP_PROMPT.md`; do not resume a long manager conversation.

Example scheduler shell behavior:

```bash
cd /path/to/persistent/voxel-manager-checkout
python3 tools/astra_manager_loop.py --fetch
case $? in
  0)  ;; # no Astra needed
  10) ;; # wake required but no launcher configured
  *)  ;; # operational error
esac
```

Running this cheap check hourly is reasonable; `config.json` defaults normal Astra batching to five hours. Event accumulation happens in runtime state, so several completions become one manager wake-up while the mechanical review budget prevents a large backlog from becoming a large Astra prompt.

## Astra output and follow-up publication

Astra writes `runtime/decision.json`, then always runs:

```bash
python3 tools/astra_manager.py apply-decision
python3 tools/astra_manager_publish.py
```

`apply-decision` validates reviewed keys, advances local manager state, writes a compact untracked audit record, and creates any approved follow-up under `SceneIssues/open/<id>/` using the repository's normal `issue.json + plan.md + tasks.md` shape. It never edits production code.

`astra_manager_publish.py` is deterministic transport. It detects only new untracked `MANAGER FOLLOW-UP` SceneIssues, creates a commit from current `origin/master` using an isolated temporary Git index, pushes a dedicated `astra-manager/followups-*` branch, opens a PR to protected `master`, enables auto-merge using an allowed repository merge method, records the PR locally, and exits immediately. It never waits for CI or merge and never includes implementation files.

Publication is retry-safe: a created PR is recorded before auto-merge is attempted, so a transient auto-merge failure is retried on the next invocation instead of creating a duplicate PR. While a follow-up PR is pending, its local untracked SceneIssue remains visible to duplicate prevention. After the PR reaches `origin/master`, the next canonical loop removes the matching local copy before fast-forwarding.

Once the follow-up reaches master, the normal agent allocator owns implementation. Astra does not wait for assignment or work on the issue itself.

## Runtime files

`SceneIssues/manager/runtime/` contains:

- `state.json` — collection/review cursor and the complete pending queue; not normal Astra input;
- `signal.json` — whether a manager wake is currently due and which keys were selected;
- `digest.md` — complete deterministic current/delta summary for diagnostics; not normal Astra input;
- `review-window.md` — the bounded minimal packet Astra reads on wake-up;
- `open-issue-index.md` — duplicate-prevention index, read only before creating follow-up work;
- `packets/*.md` — compact completion packets, read only for selected completions;
- `decision.json` — one Astra pass's bounded output;
- `history/*.md` — compact local audit records;
- `published-followups.json` — pending/published manager follow-up PR bookkeeping.

No chain-of-thought or investigation diary is stored.

## Useful commands

```bash
# canonical cheap loop; invokes Astra only when due
python3 tools/astra_manager_loop.py --fetch

# run without optional GitHub Actions queries
python3 tools/astra_manager_loop.py --fetch --no-ci

# raw collector only; diagnostic use (assumes worktree already matches origin/master)
python3 tools/astra_manager.py check --fetch

# reset/bootstrap the collection cursor
python3 tools/astra_manager.py bootstrap --fetch
python3 tools/astra_manager.py bootstrap --from-sha <sha>

# apply and publish a completed Astra management decision
python3 tools/astra_manager.py apply-decision
python3 tools/astra_manager_publish.py
```

## Scheduling

Keep scheduling outside Astra. Use cron, launchd, or another cheap local scheduler to execute `python3 tools/astra_manager_loop.py --fetch` periodically in the dedicated manager checkout. The scheduler can run hourly; Astra itself normally wakes no more often than the configured batching window unless you intentionally change the policy.

No repository scheduler is checked in because the actual Astra launcher is environment-specific. `ASTRA_MANAGER_WAKE_COMMAND` is the single deployment seam.

## Tuning

Start with the checked-in defaults. After several days, tune `batchHours`, stale-agent threshold, review budgets, large-diff threshold, and core path patterns based on observed wake frequency. Prefer improving deterministic filtering over giving Astra a larger bootstrap context.
