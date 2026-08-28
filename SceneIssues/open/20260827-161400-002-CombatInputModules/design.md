# Combat and Input Module Integration Design

**Status:** Reopened — full implementation required  
**Scope:** Complete the production migration of reusable/authoritative combat behavior from `Assets/CombatPrototype` into `Game.Combat`, with `Game.Input` remaining the device-facing input boundary. Analysis, documentation, scaffolding, or a narrow vertical slice alone do **not** satisfy this SceneIssue.

## 1. Why this issue was reopened

The original `design/combat-input-modules` plan described a staged promotion of the combat prototype into production Game modules. A later Kentridge SceneIssue implemented the first reusable seams (`Game.Combat.Api/Runtime`, `Game.Input.Api/Runtime`) and one normal-world encounter. This SceneIssue was then closed after refreshing the design and adding a boundary regression, even though that refreshed design explicitly documented that the richer combat rules still remained in `Assets/CombatPrototype`.

The prior closure therefore represents useful completed analysis and baseline work, not completion of the migration. Commit `84bf1b82001356046e1af103fcbe207a8a2be3ff` and its regression remain historical evidence only.

**Binding closure rule:** this SceneIssue must remain open until the full implementation migration is complete and verified. A future agent must not close it merely because the architecture is documented, the module folders exist, or one production encounter works.

## 2. Current repository state

Production boundaries already exist:

```text
Assets/Game/Combat/
  Api/
    Game.Combat.Api.asmdef
    CombatContracts.cs
  Runtime/
    Game.Combat.Runtime.asmdef
    CombatRuntime.cs

Assets/Game/Input/
  Api/
    Game.Input.Api.asmdef
    InputContracts.cs
  Runtime/
    Game.Input.Runtime.asmdef
    InputRuntime.cs
```

`Game.Input.Api` exposes device-neutral player intent. `Game.Input.Runtime` owns concrete Unity input. `Game.Combat.Runtime` consumes the input API and currently provides a narrow encounter lifecycle plus simple grid movement. Kentridge composition proves these seams can be used in the normal world with normal actors.

The migration is still incomplete because `Assets/CombatPrototype` remains a separate combat implementation. Its files include the richer board/rules state, execution planning, reactions, round readiness, tactical AI, environmental integration, and lab controller paths. The lab still directly mutates prototype combat state and therefore represents a second combat authority rather than merely presentation tooling.

## 3. Ownership model

### `Game.Input.Api`
Own only device-neutral input concepts such as local-player identity, input contexts, snapshots, and reader/service contracts. It must not know combat cells, turns, abilities, targets, damage, or command legality.

### `Game.Input.Runtime`
Own Unity/device sampling and input-context arbitration. Hardware-specific code stays here, not in Combat.

### `Game.Combat.Api`
Own stable cross-feature combat lifecycle/contracts/read models/events needed by real consumers. Expand this API only where a production consumer requires it. Do not expose mutable prototype internals or Unity presentation state.

### `Game.Combat.Runtime`
Own deterministic authoritative combat state and rules: command validation, movement, attacks, damage, force/knockback, reactions, chain execution, round/turn coordination, and combat-owned tactical decision logic. Keep simulation authority independent from Unity transforms and device APIs.

### Composition / presentation
Concrete wiring belongs in composition. Presentation consumes immutable/read-only state and combat events. Lab/showcase UI may remain as tooling, but it must become a consumer/adapter of production combat rather than a second authoritative rules engine.

## 4. Mandatory prototype inventory

Before moving code, inspect **every** substantive class under `Assets/CombatPrototype` and classify it as one of:

1. **Authoritative reusable combat** — must migrate behind `Game.Combat`.
2. **Presentation/demo-only tooling** — may remain temporarily, but must consume production combat and contain no duplicate authoritative rules.
3. **Adapter/composition glue** — move to the correct composition boundary or replace with owning-module APIs.

At minimum, explicitly classify and account for:

- `CombatCore.cs`
- `ChainCombatBoard.cs`
- `ChainExecutionPlan.cs`
- `ChainExecutionPlanner.cs`
- `ChainReactionReservationCoordinator.cs`
- `ChainRoundReadinessCoordinator.cs`
- `ChainPlanApprovalCoordinator.cs`
- `ChainEnemyTacticalAI.cs`
- `ChainCombatVegetationBridge.cs`
- `ChainCombatLabController.cs`
- motion playback/event markers/overlays
- demo scenario/guide/setup tooling

No file may be silently ignored merely because the production slice does not currently use it.

## 5. Migration sequence

1. **Lock the existing module boundary.** Preserve the engine-free Combat API/Runtime and device-neutral Input API direction already proven by Kentridge.
2. **Inventory and baseline prototype behavior.** Add behavioral fixtures for the authoritative mechanics before moving them.
3. **Migrate deterministic movement and board state.** Production Combat becomes authority for positions, occupancy, legality, and command results.
4. **Migrate attacks, damage, force/knockback, and reactions.** Preserve deterministic ordering and rejection semantics.
5. **Migrate chain execution/planning and round coordination.** Remove duplicate prototype authority as each behavior reaches parity.
6. **Migrate combat-owned tactical AI.** AI should consume combat snapshots/commands; world/campaign navigation remains outside Combat when appropriate.
7. **Migrate environmental combat integration through owning APIs.** Vegetation/world changes must go through the owning module's API, with composition binding concrete runtimes.
8. **Convert the lab/showcase.** Lab input/presentation must drive production Combat commands/read models/events. It may remain as a demo harness, but not as an independent combat engine.
9. **Remove compatibility authority.** Delete or reduce prototype classes once no production behavior depends on them. Any intentionally retained prototype code must be demonstrably presentation/demo-only.

## 6. Required behavioral verification

The existing regressions are baselines, not completion evidence:

- `CombatInputModuleBoundaryTests.SyntheticReader_DrivesCombatMoveThroughDeviceNeutralBoundary`
- `KentridgeCombatEncounterTests.ForestBandits_ApproachBeginsInPlaceCombatThroughProductionModules`

The completed migration must add focused production-path regressions for every migrated authoritative category, including as applicable:

- movement/occupancy parity;
- attack legality, damage, and knockback/force parity;
- reaction reservation and chain ordering parity;
- plan/execution and round-readiness parity;
- deterministic replay from the same command stream;
- tactical AI deterministic choice where its inputs are deterministic;
- environment interaction through owning module APIs;
- local-player/input-context isolation;
- normal-world actors persisting through combat enter/exit;
- dependency tests proving Combat simulation does not depend on Unity input/device APIs or `MountingForce.CombatPrototype`.

Tests must exercise production computation, not source-string assertions.

## 7. Architectural invariants

1. Combat is a `Game` module, not a VoxelEngine module.
2. Input is a separate `Game` module; Input never depends on Combat.
3. Feature modules depend on `Game.Combat.Api` / `Game.Input.Api`, not concrete Runtime assemblies except in explicit composition roots.
4. `Game.Combat` must not reference `MountingForce.CombatPrototype`.
5. Combat simulation remains deterministic authoritative state; Unity transforms/animations never become truth.
6. Environment mutation uses the owning module's API.
7. The prototype/lab may remain only as a presentation/demo harness consuming production Combat.
8. No two-way synchronization between duplicate mutable prototype and production combat state is allowed.

## 8. Blast radius and cost

The main risks are duplicate authority, contract inflation, and accidentally moving presentation/device concerns into Combat. Migrate one behavior at a time and retire the old authority immediately after parity is established. Keep per-frame input reads allocation-free and avoid creating command objects every frame when no semantic action occurs. Do not proliferate new assemblies without a demonstrated ownership/build benefit.

## 9. Definition of complete — binding closure criteria

**All of the following are required before this SceneIssue can move to `pending` or `closed`:**

1. Every substantive `Assets/CombatPrototype` class has a documented disposition.
2. Every reusable authoritative combat mechanic currently implemented by the prototype has been migrated to production `Game.Combat`, or there is concrete evidence that it is intentionally demo/presentation-only.
3. Production combat scenes/composition do not depend on prototype-owned authoritative combat state or rules.
4. The lab/showcase, if retained, drives production combat rather than directly mutating an independent `ChainCombatBoard` rules engine.
5. No duplicate mutable authority remains for migrated mechanics.
6. Required parity and deterministic regressions are green through production code paths.
7. Combat/Input dependency boundaries remain clean, including no Combat dependency on Unity device APIs and no `Game.Combat` dependency on `MountingForce.CombatPrototype`.
8. Existing Kentridge combat integration remains functional through the production modules.
9. Focused exact-SHA targeted CI is green and the repository's required built-application scene validation passes for the affected production combat scene(s), with durable evidence stored beside the issue.
10. Only after those gates pass may normal SceneIssue pending/fixed metadata and closure bookkeeping be performed.

**Not sufficient for closure:** documentation updates, a refreshed design, module scaffolding, one synthetic boundary test, one simple movement implementation, or one Kentridge encounter by themselves.

## 10. Architectural reference

The original `Docs/combat-input-module-design.md` on branch `design/combat-input-modules` remains the broad architectural reference. This reopened document is the current implementation/closure contract and supersedes any earlier wording that described this SceneIssue as analysis-only.
