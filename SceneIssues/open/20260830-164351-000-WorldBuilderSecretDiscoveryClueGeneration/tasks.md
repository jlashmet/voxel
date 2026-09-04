# Tasks — WorldBuilder Secret Discovery Clue Generation

## Evidence and scope

- [x] Read `AGENTS.md`, `SceneIssues/issue-readme.md`, and `SceneIssues/README.md`; keep work scoped only to this assignment.
- [x] Inspect captures/marked regions; none are present. Use the issue design contract and module-local built-player scene as acceptance evidence.
- [x] Reassess module ownership: WorldBuilder, CaveWorldBuilder, and Showcase own focused tests/production-path validation; `WorldBuilderSecretDiscoveryValidation` is the authoritative visual gate.
- [x] SceneIssue metadata targets `Assets/Game/WorldBuilder/Validation/SecretDiscovery/WorldBuilderSecretDiscoveryValidation.unity`; legacy Gallery evidence is historical only.

## Planning / behavior

- [x] Stable secret/route/clue IDs and immutable plan metadata.
- [x] Same seed/inputs produce the same plan independent of candidate enumeration order.
- [x] Standard/Major clue-count and independent-channel policy is behaviorally tested.
- [x] Required clues are pre-solve observable and circular dependencies fail validation.
- [x] Reusable semantic clue anchors avoid prefab names and capture coordinates.
- [x] Interactable-backed and natural traversal routes are both supported without duplicate interaction authority.
- [x] Multiple legal routes resolve to one canonical discovery identity; revisit/reload/repeated activation remains idempotent.
- [x] `ProtectedShell`, `AuthoredBreakablesOnly`, and `SystemicBypassAllowed` are represented and behaviorally tested, including accidental bypass prevention.
- [x] Route/mechanism-aware anomaly planning selects compatible motif families and explicit action intent (`BreakBarrier`, `OperateMechanism`, `TraverseTerrain`, `Investigate`).
- [x] Canonical WorldObject mechanisms own interaction/state restoration; WorldBuilder only plans/places/connects them.
- [x] Local-context scoring plus deterministic repetition penalties produce controlled clue variety rather than a universal marker.
- [x] `SecretClueAnomalyPlannerTests`, discovery/planner tests, generated-cave bypass tests, and WorldObject integration tests cover the semantic invariants.

## Production-path / visual acceptance

- [x] Generated cave composition, verified seal topology, clue presentation, canonical destruction, and hidden-pocket reveal run through production authoring/storage/rendering paths.
- [x] Validation enters through canonical `GameSessionOrchestrator` / `ISessionRuntimeGraph` lifecycle rather than a parallel validation lifecycle.
- [x] Natural approach uses production vegetation and `VegetationDiscontinuity/TraverseTerrain`; breakable route uses `StructuralFracture/BreakBarrier`.
- [x] Rejected run `33903218535` was correctly kept open because its natural exterior framing exposed void/crater geometry.
- [x] Fix only that evidence path: extend vegetation banks, hold exterior evidence, use gameplay-height approach framing, and suppress pre-convergence module captures with `evidenceAfterSeconds`.
- [x] Run `33912025831` proved the corrected natural framing, but its longer standalone replay exposed post-evidence BrickPool exhaustion; validation was kept open rather than accepting the wrapper's green status.
- [x] Bound the validation sequence after its final required capture and make `InvalidOperationException` fail closed in the module player scenario.
- [x] Exact request `43f168d8311ed283589fd7e2d44e2ca551225712` / run `33913283670` completed without replacement and passed automatic module validation, module players, canonical Kentridge integration, standalone SceneIssue replay, screenshot upload, and final status.
- [x] Final module-local run logs `lifecycle=Running`, `StructuralFracture/BreakBarrier`, `VegetationDiscontinuity/TraverseTerrain`, a 607-voxel production breach, and `evidence complete: commands stopped after final capture`; no `NullReferenceException`, `MissingReferenceException`, or `InvalidOperationException` occurs.
- [x] Full-resolution WorldBuilder frames at 3/6 seconds show a grounded vegetation/negative-space approach without glow/icon/signage or crater/void exposure; 12/15/18-second frames show the structural fracture; 21 seconds shows the opened authored route/connector.
- [x] Visual acceptance classification: `production-quality` for this clue-generation acceptance surface—intentional anomaly/action language is readable, production systems are used, and accepted evidence has no placeholder signs, universal glow, void/underside, stale terrain, or invalid framing.
- [x] Generic standalone SceneIssue screenshots are timing diagnostics only (`issue.json` has no replayable camera snapshot); authoritative visual proof is the same standalone scene run through its module-local player scenario, while the SceneIssue replay independently proves startup/runtime stability.

## Cost / closure acceptance

- [x] Planner/discovery/anomaly work remains one-shot/event-driven; no per-frame secret search/polling was introduced.
- [x] Cave composition and clue realization remain bounded to traversal candidates/local evidence.
- [x] Every exact CI request was left untouched while queued/running; failures were classified and fixed rather than papered over.
- [x] All issue acceptance criteria are green from exact behavioral tests, canonical full-app lifecycle evidence, module-local standalone visual evidence, production destruction, and exception-free standalone replay.
- [x] Closure metadata is supported by exact feature head `acc3df2fb95d10db45ad31777a46868457282b84`, request `43f168d8311ed283589fd7e2d44e2ca551225712`, run `33913283670`, artifact `9953139403`.

Final repository promotion (current-master reconciliation, PR + auto-merge, required `affected` gate, and merge confirmation) follows `SceneIssues/README.md` after the `open -> closed` bookkeeping commit; those are integration steps, not additional feature acceptance criteria.
