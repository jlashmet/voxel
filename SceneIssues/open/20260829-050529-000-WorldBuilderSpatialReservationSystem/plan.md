# Plan — WorldBuilder Spatial Reservation System

## Goal

Provide one deterministic, engine-free spatial claim/query substrate for WorldBuilder without replacing road solving, architecture compatibility, ecology policy, hidden-space topology, or presentation ownership. Close only after production consumers, focused regressions, built runtime evidence, cost/blast-radius checks, and exact-SHA CI are all green.

## Current state — 2026-08-30

Current `origin/master` remains unchanged at `ebdc2e4f63ef73153cd4e0ff5c62efe604f35470` as of the 16:59 PDT re-check. `fixes/agent-7` remains fully reconciled with master (0 behind) and contains only assignment-scoped reservation/core integration, intended Kentridge consumers, gallery evidence, focused regressions, and SceneIssue bookkeeping. There is no new master integration to merge yet.

Implemented acceptance seams remain:
- engine-free deterministic 3D reservations, bounded snapshots, diagnostics and precedence;
- Kentridge settlement, macro-road/handoff, production structure, vegetation and hidden-space consumers;
- composition-configured road-clearance yielding plus an independent non-Kentridge reuse fixture;
- presentation-only gallery inspection/overlay with deliberate rejected candidate evidence;
- capture-less gallery audit support that records reservation query/build metrics and Unity allocated/reserved memory plus region residency.

The `master...fixes/agent-7` blast-radius review changes no global/device/region budget files or CharacterMotor/world-generation tolerances. The delta remains limited to the reservation core/adapters, intended Kentridge consumers, gallery evidence, focused tests, and this SceneIssue bookkeeping.

Original acceptance criterion (7) additionally requires a production typed-`StructuralSocket` consumer. Re-checked at 2026-08-30 16:59 PDT: prerequisite `SceneIssues/open/20260829-034505-000-WorldBuilderTypedStructuralSocketComposition` remains open on unchanged current master, and its execution checklist remains entirely unchecked on master. This is an external blocker, not grounds to narrow acceptance. Agent-7 will not import or implement another assignment's socket system; re-check and integrate the canonical production seam after that feature lands.

Current SceneIssue workflow has no separate scene classifier/highlight artifact gate. Its player-visible contract is direct exact-SHA built-player validation with durable evidence. The existing production smoke test proves the required evidence classes are present/visible and the capture-less audit harness emits the screenshot plus cost/memory data; visual inspection of that final durable evidence remains required.

`ci-test/fixes/agent-7` remains reserved for the final targeted request and was re-verified unchanged at stale prior head `8cc6ff94dcbbca46b1c522d08752235b891b1851` at 16:59 PDT. Do not advance it while the production typed-socket acceptance seam is unavailable.

## Validation hypotheses / discriminator

1. **Likely:** after the typed-socket prerequisite lands, the narrow production adapter can consume `SpatialReservationSnapshot.Query`/clearance semantics without moving socket compatibility/topology ownership; focused reservation/Kentridge/vegetation/hidden-space regressions and the incoming foundation-surface regression remain green.
2. **Alternative:** the socket integration or master's foundation-depth changes expose an ownership/bounds defect in the production structure path or scene evidence.

Discriminator: merge the prerequisite only from current `master`, add the smallest production socket-to-reservation seam plus regression if acceptance is not already satisfied by the landed implementation, then issue one exact-SHA CI request containing the narrow focused test plus the required built-player SceneIssue capture. Any product failure is fixed before another CI request; repeated identical acceptance failure twice requires a minimal repro/root-cause isolation.

## Remaining gates

1. Re-check `origin/master` for the typed-structural-socket prerequisite; integrate its canonical production clearance seam only after it lands on master.
2. Merge any newer master before final validation and re-review the assignment-only diff.
3. Verify `ci-test/fixes/agent-7` is still idle, then use it only for the final exact-SHA request covering reservation/Kentridge/vegetation/hidden-space/foundation regressions and built-player evidence as narrowly as repository tooling permits.
4. Record final `SPATIAL_RESERVATION_COST`, allocation/memory/region metrics, and directly inspect durable Worldbuilding Gallery + Kentridge runtime evidence.
5. After every acceptance gate is green, complete metadata, move `open/` directly to `closed/`, merge current master if it advanced, revalidate affected work, and non-force promote the exact feature head.

No unchecked task remains independently completable before step 1: remaining unchecked items either require the canonical typed-socket production seam or the final exact-SHA Unity/runtime evidence. Do not manufacture intermediate CI or weaken acceptance while blocked.

## Cost / ownership guardrails

No global registry, Unity Physics authority, per-claim authoritative GameObjects/colliders, duplicate road solver, duplicated structural socket solver, or duplicated ecology/hidden-space policy. Keep one shared source snapshot with bounded views and preserve existing deterministic/device budgets.
