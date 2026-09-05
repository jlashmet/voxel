# Astra manager supervisor implementation plan

## Acceptance

- [x] Manager role is review/SceneIssue creation only; no implementation or CI polling.
- [x] A deterministic collector produces a compact delta, active-agent summary, open-issue index, and selective completion packets.
- [x] The canonical loop synchronizes its dedicated working tree to `origin/master` before file-based inspection.
- [x] No-change/routine checks consume zero Astra; normal reviews batch across a configurable window.
- [x] Fresh Astra sessions use progressive disclosure and mechanically bounded review windows.
- [x] A large pending backlog cannot expand Astra bootstrap context; excess reviews remain local and deferred.
- [x] Runtime cursor/backlog survives across checks without dirtying `master`.
- [x] Astra decisions can create standard follow-up SceneIssues and compact audit history without production-code edits.
- [x] Manager-created follow-ups are published through dedicated protected-master PRs with auto-merge and no Astra waiting.
- [x] Follow-up publication is retry-safe and pending local follow-ups remain visible to duplicate prevention until merged.
- [x] Unit coverage proves bootstrap, no-op progress, closure review, acceptance-change signal, follow-up creation, review-window budgeting, and publisher invariants.

## Architecture

`tools/astra_manager.py` owns deterministic collection, persistent local review state, decision application, and follow-up SceneIssue generation. `tools/astra_manager_loop.py` is the canonical synchronization/budget-enforcing wake-up entrypoint. `tools/astra_manager_publish.py` is deterministic protected-master transport for manager-created SceneIssues. `SceneIssues/manager/*.md` is the stable manager contract. Machine-local `SceneIssues/manager/runtime/` is ignored and holds generated cursor/digest/review-window/decision/publication state. Existing agents and SceneIssue workflow remain authoritative and unchanged.

## Selected approach

Use event accumulation plus a five-hour default batch window rather than a persistent Astra loop. The outer loop first fast-forwards a dedicated clean local `master` to `origin/master`, then the collector reads Git/SceneIssue metadata and optional `gh` CI summaries. The loop mechanically selects at most the configured suspicious/routine review counts, writes `review-window.md`, and invokes Astra only when `signal.json.wakeAstra` is true. Astra starts fresh, reads only that bounded window, expands context progressively for selected evidence, emits a bounded decision, runs deterministic apply/publication commands, then exits. Follow-up PRs auto-merge independently through normal repository gates and are subsequently handled by normal agents.

## Validation

The Python tooling is covered by `tools/tests/test_astra_manager.py`, `tools/tests/test_astra_manager_loop.py`, and `tools/tests/test_astra_manager_publish.py`. No Unity/player behavior changes, so no module-local Unity validation scene applies. Final integration still follows the protected-master PR gate.
