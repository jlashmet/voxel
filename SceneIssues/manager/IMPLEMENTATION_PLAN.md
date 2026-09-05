# Astra manager supervisor implementation plan

## Acceptance

- [x] Manager role is review/SceneIssue creation only; no implementation or CI polling.
- [x] A deterministic collector produces a compact delta, active-agent summary, open-issue index, and selective completion packets.
- [x] No-change/routine checks consume zero Astra; normal reviews batch across a configurable window.
- [x] Fresh Astra sessions use progressive disclosure and bounded review budgets.
- [x] Runtime cursor/backlog survives across checks without dirtying `master`.
- [x] Astra decisions can create standard follow-up SceneIssues and compact audit history without production-code edits.
- [x] Unit coverage proves bootstrap, no-op progress, closure review, acceptance-change signal, and follow-up creation.

## Architecture

`tools/astra_manager.py` is deterministic orchestration. `SceneIssues/manager/*.md` is the stable manager contract. Machine-local `SceneIssues/manager/runtime/` is ignored and holds only generated cursor/digest/decision state. Existing agents and SceneIssue workflow remain authoritative and unchanged.

## Selected approach

Use event accumulation plus a five-hour default batch window rather than a persistent Astra loop. The collector reads Git/SceneIssue metadata and optional `gh` CI summaries; Astra is invoked only when `signal.json.wakeAstra` is true. It starts fresh, reviews only pending high-signal work, emits a bounded decision, then exits.

## Validation

The Python tooling is covered by `tools/tests/test_astra_manager.py`. No Unity/player behavior changes, so no module-local Unity validation scene applies. Final integration still follows the protected-master PR gate.
