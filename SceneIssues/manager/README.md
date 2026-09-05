# Astra manager supervisor

This directory implements a low-context, stateless management loop around the existing SceneIssue agent system.

> Scripts determine what changed. Astra determines what it means. Agents implement the work.

The manager runtime is intentionally untracked at `SceneIssues/manager/runtime/`; it is durable in the manager checkout but does not dirty `master` or require a PR merely to advance a review cursor. Use a dedicated persistent checkout for the manager loop.

## One iteration

Run:

```bash
python3 tools/astra_manager.py run --fetch
```

The deterministic collector:
1. fetches `origin`;
2. compares current `origin/master` with the last collected SHA;
3. summarizes active SceneIssues and `fixes/agent-N` branches;
4. optionally reads recent GitHub Actions status through the authenticated `gh` CLI;
5. generates completion packets only for closed issues changed since the cursor;
6. detects high-signal events (closures, acceptance changes, core/shared deltas, stale branches, repeated CI failures, declared known limitations);
7. appends those signals to a persistent pending-review queue;
8. exits without Astra when there is nothing meaningful to review or the normal batching window has not elapsed.

The first run bootstraps at current master and intentionally does **not** enqueue the historical backlog. To review from an older point, use `bootstrap --from-sha <sha>` before the next check.

## Waking Astra

When a review is due and no launcher is configured, `run` exits with code `10` and prints the paths Astra needs. That is the integration point for your existing Astra/Work launcher.

To make the loop autonomous, set `ASTRA_MANAGER_WAKE_COMMAND` to your external launcher. The command runs only when `signal.json.wakeAstra` is true and receives:

- `ASTRA_MANAGER_PROMPT`
- `ASTRA_MANAGER_DIGEST`
- `ASTRA_MANAGER_OPEN_ISSUE_INDEX`
- `ASTRA_MANAGER_DECISION_OUTPUT`
- `ASTRA_MANAGER_MASTER_SHA`

The launcher should start a **fresh** Astra session using `WAKEUP_PROMPT.md`; do not resume a long manager conversation.

Example scheduler shell behavior:

```bash
cd /path/to/persistent/voxel-manager-checkout
python3 tools/astra_manager.py run --fetch
case $? in
  0)  ;; # no Astra needed
  10) ;; # wake required but no launcher configured
  *)  ;; # operational error
esac
```

Running this cheap check hourly is reasonable; `config.json` defaults normal Astra batching to five hours. Event accumulation happens in runtime state, so several completions become one manager wake-up.

## Astra output and follow-ups

Astra writes `runtime/decision.json` and runs:

```bash
python3 tools/astra_manager.py apply-decision
```

The apply step validates reviewed keys, advances local manager state, writes a compact untracked audit record, and creates any approved follow-up under `SceneIssues/open/<id>/` using the repository's normal `issue.json + plan.md + tasks.md` shape. It never edits production code.

If follow-up SceneIssues were created, publish **only those SceneIssue files** through the repository's normal protected-master PR path. The normal agent allocator then owns implementation. Do not ask Astra to implement the work it created.

## Runtime files

`SceneIssues/manager/runtime/` contains:

- `state.json` — collection/review cursor and pending queue;
- `signal.json` — whether a manager wake is currently due;
- `digest.md` — compact current/delta summary;
- `open-issue-index.md` — duplicate-prevention index;
- `packets/*.md` — compact completion packets generated only when needed;
- `decision.json` — one Astra pass's bounded output;
- `history/*.md` — compact local audit records.

No chain-of-thought or investigation diary is stored.

## Useful commands

```bash
# cheap collection only; returns 10 when Astra should wake
python3 tools/astra_manager.py check --fetch

# ignore optional CI queries when gh is unavailable/unwanted
python3 tools/astra_manager.py check --fetch --no-ci

# reset/bootstrap the collection cursor
python3 tools/astra_manager.py bootstrap --fetch
python3 tools/astra_manager.py bootstrap --from-sha <sha>

# apply Astra's decision
python3 tools/astra_manager.py apply-decision
```

## Tuning

Start with the checked-in defaults. After several days, tune `batchHours`, stale-agent threshold, review budgets, large-diff threshold, and core path patterns based on observed wake frequency. Prefer improving deterministic filtering over giving Astra a larger bootstrap context.
