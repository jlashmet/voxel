# Combat and Input Module Integration Design

**Status:** Current-state implementation design, refreshed 2026-08-28  
**Scope:** Analysis and executable boundary verification only. This SceneIssue does **not** change production code.

## 1. Current repository state

The original analysis snapshot is no longer the repository state. A later Kentridge production slice (`be1b8664aaa172b94b563c7793cd367644e52e04`) introduced the first reusable Combat/Input modules, so the migration should build on them rather than recreate them.

### Existing production boundaries

```text
Assets/Game/Combat/
  Api/
    Game.Combat.Api.asmdef        # no engine references
    CombatContracts.cs
  Runtime/
    Game.Combat.Runtime.asmdef    # Game.Combat.Api + Game.Input.Api only; no engine references
    CombatRuntime.cs

Assets/Game/Input/
  Api/
    Game.Input.Api.asmdef         # no engine references
    InputContracts.cs
  Runtime/
    Game.Input.Runtime.asmdef     # Game.Input.Api + UnityEngine
    InputRuntime.cs
```

`Game.Input.Api` owns device-neutral `PlayerInputSnapshot`, local-player identity, input context, and `IPlayerInputReader`. `Game.Input.Runtime` owns the concrete Unity reader and input-context stack. `Game.Combat.Runtime` consumes only `IPlayerInputReader`; it does not read Unity input or reference UnityEngine.

The Kentridge composition already proves the intended runtime direction: normal-world encounter composition creates `InputContextService`, `UnityPlayerInputReader`, `CombatService`, and `CombatInputController`, then keeps the same world actors present while combat becomes active.

### What is still prototype-owned

`Assets/CombatPrototype/MountingForce.CombatPrototype.asmdef` remains a separate lab assembly. It references Vegetation only and therefore cannot directly couple to Game Combat/Input runtime implementations. `ChainCombatLabController` still owns presentation plus IMGUI `Event.current` interaction and directly operates `ChainCombatBoard`; the reusable `Game.Combat` slice does not yet replace that larger prototype ruleset.

That means "the modules exist" is not equivalent to "the prototype migration is finished."

## 2. Evidence and competing hypotheses

### H1 — Move the prototype wholesale into `Game.Combat`
Rejected. Renaming/moving files would carry lab presentation, IMGUI interaction, scenario content, AI, and direct board mutation into the production module. The dependency direction would look cleaner while the behavioral coupling stayed intact.

### H2 — The Kentridge slice means the migration is complete
Rejected. The new modules currently implement a deliberately narrow lifecycle/grid-move slice. The lab still has a much richer independent board, chain/reaction planning, UI, and presentation path.

### H3 — Keep the existing public seams and migrate behavior incrementally
Selected. The existing engine-free Combat/Input APIs are the correct starting boundary. Add capability behind those seams, prove parity with behavioral tests, and leave the lab runnable until equivalent production composition exists.

## 3. Required ownership model

### `Game.Input.Api`
Own only semantic, device-neutral input:
- `LocalPlayerId`
- `InputContextId`
- `PlayerInputSnapshot`
- `IPlayerInputReader`
- input-context lease/service contracts

It must not know combat cells, turns, abilities, actors, target validation, or command legality.

### `Game.Input.Runtime`
Own Unity device sampling and context arbitration. The current `UnityPlayerInputReader` is correctly placed here. Any future Input System package adapter also belongs here, never in Combat.

The current `SuppressLegacyReadersForCurrentFrame` method is transitional debt. Remove it only after all legacy consumers have moved behind the input-context service; do not move that suppression logic into Combat.

### `Game.Combat.Api`
Own stable cross-feature combat concepts only. The existing lifecycle/session/participant contracts are a valid first slice. Expand the API only when a real consumer requires it, with public commands/read models/events rather than exposing mutable prototype state.

Recommended future additions as the lab migrates:
- semantic combat commands (`Move`, `SelectAbility`, `SelectTarget`, `Confirm`, `Cancel`)
- immutable combat read snapshot
- presentation/domain events needed by composition/presentation
- explicit command result/rejection data

Do not expose `ChainCombatBoard`, writable phases, planner internals, renderer objects, or Unity transforms.

### `Game.Combat.Runtime`
Own use-case sequencing and deterministic combat state. Keep the assembly engine-free. For the next stages, organize internals under Runtime folders first:

```text
Assets/Game/Combat/Runtime/
  Session/
  Commands/
  Model/
  Rules/
  Movement/
  Attacks/
```

A separate `Game.Combat.Model` assembly is optional, not required up front. Split it only if independent compilation/reuse produces a real benefit; folder-level internal ownership is enough to prevent premature assembly proliferation.

### Composition roots
Concrete Runtime-to-Runtime wiring belongs in composition, as Kentridge already demonstrates. A future combat-lab bridge should live in a composition assembly that references both the prototype and Game APIs/runtimes. Do not make `MountingForce.CombatPrototype` the long-term owner of production input/runtime wiring.

## 4. Migration sequence

1. **Lock the boundary contract.** Keep Combat Api/Runtime and Input Api/Runtime engine ownership as it is today. The regression added by this SceneIssue proves a synthetic `IPlayerInputReader` can drive a real combat mutation without a Unity device dependency.
2. **Define command/read-model parity for one prototype action.** Choose a simple existing lab action (movement first). Add the production command/result shape needed to represent it without leaking `ChainCombatBoard`.
3. **Move deterministic movement/rule code behind Combat Runtime.** Preserve integer/grid authority and deterministic validation. Add parity tests against the existing prototype behavior before changing the lab.
4. **Add a lab composition adapter.** Translate lab interaction into device-neutral Input/Combat commands outside the prototype assembly. Keep the current lab controller available as a compatibility path during transition.
5. **Migrate richer rules incrementally.** Attacks, reactions, chain planning, environmental interactions, then AI. Each step requires a behavioral parity regression before the legacy path is retired.
6. **Separate presentation from authority.** Presentation consumes snapshots/events; transforms, highlights, animation clocks, and UI never feed authoritative simulation state back into Combat.
7. **Retire direct lab input/state mutation last.** Once every interactive path is covered by production contracts and parity tests, remove IMGUI/device translation from the authoritative path and delete compatibility shims.

## 5. Likely file ownership during migration

Keep presentation/demo-only files under `Assets/CombatPrototype` until the production equivalents exist:
- `ChainCombatLabController.cs`
- overlays, demo guide/scenario, lab visual helpers

Candidates to migrate behind `Game.Combat.Runtime` as parity is established:
- deterministic grid/value types from `CombatCore.cs`
- board/rule state from `ChainCombatBoard.cs`
- deterministic movement/attack resolution
- reaction/chain state that is actual combat truth rather than UI orchestration

AI may become a Combat Runtime service only if it depends solely on combat snapshots/commands. If it needs world navigation or campaign data, keep it in composition/gameplay and communicate with Combat through API contracts.

## 6. Behavioral verification strategy

### Added by this SceneIssue
`VoxelEngine.Tests.PlayMode.CombatInputModuleBoundaryTests.SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary`

It creates a real `CombatService`, begins an encounter, injects a fake `IPlayerInputReader`, ticks the real `CombatInputController`, and proves the authoritative player grid position changes while unrelated enemy state does not. No Unity input/device API is involved in the action path.

### Existing evidence to retain
`KentridgeCombatEncounterTests.ForestBandits_ApproachBeginsInPlaceCombatThroughProductionModules` proves the real normal-world composition can enter combat through these modules without swapping scenes and while the Combat input context is active.

### Required regressions for later migration stages
- prototype-vs-production movement parity
- attack legality/damage/knockback parity
- reaction/chain ordering parity
- deterministic replay from the same command stream
- input-context isolation for local multiplayer
- no Combat assembly dependency on UnityEngine/Input packages
- normal-world actors persist through enter/exit combat

## 7. Risks and cost

- **Two authorities:** biggest risk while prototype and production state coexist. Never mirror mutable state in both directions; migrate one behavior at a time with one authoritative owner.
- **Contract inflation:** do not export prototype internals merely to accelerate file moves.
- **Runtime allocation:** `PlayerInputSnapshot` is already a value type. Keep per-frame input reads allocation-free; create commands only on semantic actions rather than every frame.
- **Input suppression debt:** `ResetInputAxes` is a compatibility gate, not the final architecture. Remove it when legacy readers are gone.
- **Assembly proliferation:** use Api/Runtime boundaries already established; add more assemblies only for measurable ownership/build benefits.
- **Prototype usability:** keep the lab runnable throughout staged migration and retire legacy paths only after production parity is demonstrated.

## 8. Acceptance for this analysis issue

This issue is complete when the design reflects the current repository rather than the old snapshot, every requested boundary has an explicit owner/migration path, the synthetic device-neutral Combat regression is green at the exact feature SHA, and no production code has been changed.
