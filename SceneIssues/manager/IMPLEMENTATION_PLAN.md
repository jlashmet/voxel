# Astra manager supervisor implementation plan

## Acceptance

- [x] Manager role is review/SceneIssue creation only; no implementation or CI polling.
- [x] A deterministic collector produces a compact delta, active-agent summary, open-issue index, and selective completion packets.
- [x] No-change/routine checks consume zero Astra; normal reviews batch across a configurable window.
- [x] Fresh Astra sessions use progressive disclosure and mechanically bounded review windows.
- [x] A large pending backlog cannot expand Astra bootstrap context; excess reviews remain local and deferred.
- [x] Runtime cursor/backlog survives across checks without dirtying `master`.
- [x] Astra decisions can create standard follow-up SceneIssues and compact audit history without production-code edits.
- [x] Unit coverage proves bootstrap, no-op progress, closure review, acceptance-change signal, follow-up creation, and review-window budgeting.

## Architecture

`tools/astra_manager.py` owns deterministic collection, persistent local review state, decision application, and follow-up SceneIssue generation. `tools/astra_manager_loop.py` is the canonical budget-enforcing wake-up entrypoint. `SceneIssues/manager/*.md` is the stable manager contract. Machine-local `SceneIssues/manager/runtime/` is ignored and holds generated cursor/digest/review-window/decision state. Existing agents and SceneIssue workflow remain authoritative and unchanged.

## Selected approach

Use event accumulation plus a five-hour default batch window rather than a persistent Astra loop. The collector reads Git/SceneIssue metadata and optional `gh` CI summaries. The outer loop mechanically selects at most the configured suspicious/routine review counts, writes `review-window.md`, and invokes Astra only when `signal.json.wakeAstra` is true. Astra starts fresh, reads only that bounded window, expands context progressively for selected evidence, emits a bounded decision, then exits.

## Validation

The Python tooling is covered by `tools/tests/test_astra_manager.py` and `tools/tests/test_astra_manager_loop.py`. No Unity/player behavior changes, so no module-local Unity validation scene applies. Final integration still follows the protected-master PR gate.
