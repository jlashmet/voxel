# Astra manager supervisor implementation plan

## Acceptance

- [x] Manager role is review/SceneIssue creation only; no implementation or CI polling.
- [x] A deterministic collector produces a compact delta, active-agent summary, open-issue index, and selective completion packets.
- [x] The canonical loop synchronizes its dedicated working tree to `origin/master` before file-based inspection.
- [x] No-change/routine checks consume zero Astra; normal reviews batch across a configurable window.
- [x] Fresh Astra sessions use progressive disclosure and mechanically bounded review windows.
- [x] A large pending backlog cannot expand Astra bootstrap context; excess reviews remain local and deferred.
- [x] The deterministic finish boundary rejects decisions about review keys outside the current bounded window.
- [x] Runtime cursor/backlog survives across checks without dirtying `master`.
- [x] Astra decisions can create standard follow-up SceneIssues and compact audit history without production-code edits.
- [x] Manager-created follow-ups are published through dedicated protected-master PRs with auto-merge and no Astra waiting.
- [x] Follow-up publication is retry-safe; the cheap outer loop retries transport without waking Astra.
- [x] Pending local follow-ups remain visible to duplicate prevention until merged.
- [x] PR CI runs the manager Python tests when manager tooling/config changes.
- [x] Normal wake-up invokes Codex CLI directly with `gpt-6-astra`; no external launcher is required.
- [x] Codex manager sessions are ephemeral and low-reasoning.
- [x] Astra reviews the synchronized local checkout in a `read-only` sandbox and cannot implement or create SceneIssue files.
- [x] Astra's final manager decision is constrained by `decision.schema.json` and captured by Codex `--output-last-message`.
- [x] The outer controller, not Astra, materializes and publishes follow-up SceneIssues after Codex exits.

## Architecture

`tools/astra_manager.py` owns deterministic collection, persistent local review state, decision application, and follow-up SceneIssue generation. `tools/astra_manager_loop.py` is the canonical synchronization/budget-enforcing wake-up entrypoint. `tools/astra_manager_codex.py` validates Codex CLI compatibility and launches one ephemeral read-only `gpt-6-astra` `codex exec` pass. `tools/astra_manager_finish.py` enforces the selected-review boundary before applying an Astra decision. `tools/astra_manager_publish.py` is deterministic protected-master transport for manager-created SceneIssues. `SceneIssues/manager/*.md` plus `decision.schema.json` are the stable manager contract. Machine-local `SceneIssues/manager/runtime/` is ignored and holds generated cursor/digest/review-window/decision/publication state. Existing agents and SceneIssue workflow remain authoritative and unchanged.

## Selected approach

Use event accumulation plus a five-hour default batch window rather than a persistent Astra loop. The outer loop first fast-forwards a dedicated clean local `master` to `origin/master`, retries any pending deterministic follow-up transport, then collects Git/SceneIssue metadata and optional `gh` CI summaries. It mechanically selects at most the configured suspicious/routine review counts and writes `review-window.md`.

When review is due, launch `codex exec` directly with model `gpt-6-astra`, an ephemeral session, low reasoning effort, `read-only` sandbox, approval policy `never`, web search disabled, the checked-in decision output schema, and `--output-last-message` targeting ignored runtime `decision.json`. Astra may inspect local files and Git history as needed but cannot write repository content. After Codex exits, the controller validates the structured decision, materializes any approved follow-up SceneIssues through the standard repository shape, and publishes them through protected-master PRs with auto-merge. Follow-up PRs proceed independently through normal repository gates and are subsequently handled by normal agents.

## Validation

The Python tooling is covered by `tools/tests/test_astra_manager.py`, `tools/tests/test_astra_manager_loop.py`, `tools/tests/test_astra_manager_codex.py`, `tools/tests/test_astra_manager_finish.py`, and `tools/tests/test_astra_manager_publish.py`. `.github/workflows/tests-pr.yml` runs these tests only when manager tooling/configuration is affected. The Codex launcher test uses a fake CLI to prove version gating, Astra model/options, read-only sandboxing, output-schema/output-file wiring, decision production, and no repository mutation without spending Astra usage in CI. No Unity/player behavior changes, so no module-local Unity validation scene applies. Final integration still follows the protected-master PR gate.
