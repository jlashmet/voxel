# 24 Production-composed built-player vertical slice — implementation plan

**Target ownership:** Kentridge production composition plus shared standalone-player validation. No new generic gameplay module.

## Observed behavior / acceptance

The branch has completed the initial consumer cleanup: one production `UnityPlayerInputReader` can now be injected into the playable slice, HUD and forest encounter; the well/inventory presenter is read-only; and the slice supplies a `KentridgeSessionRuntimeGraphFactory` instead of constructing its own orchestrator. Current `origin/master` is `d46e24f05337553883636b4f5b35228830269530`; it is two commits ahead and will be merged before final promotion as required.

The production vertical slice is still incomplete. No scene composition root yet owns Application, persistence or the single run-update loop. The slice still calls `IGameSessionControl.Tick`, so adding a root as-is would double-advance gameplay. Continue also needs fresh per-run character authority: encounter cleanup retires stable bandit `CharacterId`s, so reusing the same registry across a newly composed run can reject those IDs.

Acceptance remains FrontEnd -> New Game -> `GameplayReady` -> real movement/interaction/progression/encounter/combat/loot/presentation -> save -> ordered teardown -> Continue -> restored live gameplay, through public production boundaries and the shared built-player harness.

## Hypotheses / result

1. **The consumer cleanup alone establishes Application ownership.** Falsified: lifecycle/tick/persistence ownership is still absent.
2. **A thin Kentridge root can own Application/input/session/persistence while each run composes fresh gameplay authority.** Selected. Repository inspection confirms #14 orchestration already supports ordered graph lifetime and #16 persistence already supports section contributors/restore graphs/file storage.

## Selected composition

Add a Kentridge-specific root with earlier execution order than the slice. It owns one input context/reader, `ApplicationFlowCoordinator`, production frontend/preferences, one `GameSessionOrchestrator`, and a persistence bridge over `SessionPersistenceService`. It injects input before slice enable, binds session control after world/session-factory readiness, and is the only in-game session tick owner.

Each `Compose` starts a fresh Kentridge character authority before building the graph. Persistence contributors capture/restore campaign progression, inventory, character semantic state and encounter outcome. Resume is explicitly marked on the graph so the opening one-shot cutscene is not replayed.

## Validation gates

Affected roots: `Assets/Game/Composition/Kentridge/{Runtime,Playable}`, `Assets/Game/Application`, `Assets/Game/Input`, `Assets/Game/Persistence`, plus standalone `Assets/Scenes/KentridgePlayableSlice`. Preserve distinct module-local Validation coverage. Complete T24-003 onward, run affected editor/module validation, then exact-SHA targeted CI with standalone Kentridge built-player evidence. Only after every task/criterion passes: close open -> closed, merge latest master, open/update the feature PR, enable auto-merge, and monitor required PR checks until merged.
