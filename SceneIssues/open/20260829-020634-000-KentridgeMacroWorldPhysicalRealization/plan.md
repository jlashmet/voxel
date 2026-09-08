# Plan

## Acceptance and ownership
`issue.json` remains the contract. This is the resumed original assignment `20260829-020634-000-KentridgeMacroWorldPhysicalRealization`; no replacement SceneIssue is being created. Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: module-local macro-physical validation.
- Showcase: module-local feature-residency validation plus `ShowcaseFeatureResidencyTests`.
- Kentridge Playable: module-local macro-world validation.
- Rendering integration: current production GPU renderer only; do not restore retired renderer internals.

## Current reconciliation state — 2026-09-07
Current `origin/master=c5fdd3af09c9ae1bf27b2ee06ba6876ebe91dcbd` is already integrated. The branch contains only this prior Kentridge/WorldBuilder assignment.

Current code/test head before this plan refresh is `dfe0cb64087cf5c67d949aa4dc26893086a797a3`. Reconciliation now restores a semantic, read-only `ShowcaseWorld.IsPresentationColumnContentSettled` contract using the current runtime's combined terrain/authored-feature `SurfaceLayerSpan` and feature-publication queues. Kentridge evidence no longer reflects into private Showcase streaming state. The owned Showcase regression proves a generated presentation column remains unsettled while authored feature publication is pending. Historical per-bounds renderer telemetry is retained only as explicitly unavailable/non-authoritative diagnostics; actual acceptance still requires canonical published-near-surface coverage plus durable built-player evidence.

Current master includes the merged GPU checkpoint, but that checkpoint did not claim visual/coverage completion. Do not assume renderer acceptance or weaken readiness, residency, quality, or budgets.

The preserved exact request `03ee78265fd4d5f52d50855eed5296043868805b` (run `34172038737`) targets superseded source `8475e3df831b5962dd8478f3841ab5ae1b613699` and remains queued on the self-hosted macOS runner. Per `AGENTS.md`/`SceneIssues/README.md`, do not replace or cancel it. Its result cannot be final closure proof for the current feature head. After it becomes terminal, submit one new exact request from the then-current feature SHA through the same `ci-test/fixes/agent-6` transport.

## Remaining gates
1. Run repository-derived exact-SHA validation: affected owned tests, every required module-local standalone player, and the 180-second Kentridge SceneIssue replay.
2. Inspect full-resolution durable evidence for Moordell, Rossdam, Fairy Village, Orc Village, lake/constrained route, Southern Ridge/pass, macro network, differentiated terrain, and real CharacterMotor traversal. Only production-quality evidence passes.
3. Record per-target convergence plus CPU/GPU/streaming/process/managed/native/GPU-memory cost against existing budgets.
4. Fix only demonstrated in-scope defects. If renderer correctness remains an external blocker, record exact evidence rather than changing acceptance.
5. Complete every task/acceptance item. Then move this same issue `open -> closed`, set fixed metadata, merge current master again if it advanced, open `fixes/agent-6 -> master`, enable auto-merge, and monitor required PR gates until merged.
