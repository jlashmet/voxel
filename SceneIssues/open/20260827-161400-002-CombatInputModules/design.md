# Combat and Input Module Integration Design

**Status:** Proposed  
**Scope:** Promote the existing combat prototype into production `Game` modules, add a first-class input module, and integrate combat with the normal world, characters, enemies, and environment without violating module API boundaries.

## 1. Summary

The current combat prototype contains useful deterministic combat logic, but it is packaged as a standalone experiment under `Assets/CombatPrototype` and mixes several responsibilities:

- deterministic combat simulation;
- combat orchestration;
- enemy AI;
- combat presentation;
- direct mouse/UI input handling;
- demo/lab scenario content;
- limited world integration.

The production design promotes Combat into a normal Game module:

```text
Assets/Game/Combat/
    Api/
    Runtime/
    Editor/
    Tests/
```

and introduces a separate game-level Input module:

```text
Assets/Game/Input/
    Api/
    Runtime/
    Editor/        # optional
    Tests/
```

The central architectural rule is:

> Feature modules communicate through their API assemblies. Runtime assemblies are private implementation details. Composition roots are the explicit place where concrete runtimes may be referenced and wired together.

Combat remains authoritative and deterministic. The normal game world and normal character/enemy objects remain present during combat. Combat does not create a separate combat world or duplicate combat-only character models.

Input is separated from Combat. The Input module owns device and Unity Input System concerns and exposes device-neutral player intent. Combat interprets that intent into deterministic combat commands. Input never knows about combat turns, combat cells, abilities, or board mutation.

---

## 2. Current-State Findings

### 2.1 Combat is outside the normal module structure

The prototype currently lives at:

```text
Assets/CombatPrototype/
```

Important files include:

```text
CombatCore.cs
ChainCombatBoard.cs
ChainExecutionPlan.cs
ChainExecutionPlanner.cs
ChainEnemyTacticalAI.cs
ChainReactionReservationCoordinator.cs
ChainRoundReadinessCoordinator.cs
ChainPlanApprovalCoordinator.cs

ChainCombatMotionPlayback.cs
ChainCombatEventMarker.cs
ChainCombatActivationOverlay.cs
ChainEnemyIntentOverlay.cs

ChainCombatVegetationBridge.cs

ChainCombatDemoScenario.cs
ChainCombatDemoGuide.cs
ChainCombatLabController.cs
ChainCombatSetupActionsPanel.cs
CombatPrototypeController.cs

Editor/CombatPrototypeMenu.cs
```

The prototype already has an important desirable property: combat authority is integer/grid/deterministic, while motion smoothing and visual event markers are presentation-only and do not feed Unity transforms back into the simulation.

That principle must be preserved.

### 2.2 The repository already has a strong API/Runtime module pattern

A clean existing example is:

```text
VoxelEngine/Characters/
    Api/
    Runtime/
    Editor/
```

Combat and Input should follow this model.

There are existing places where one Runtime assembly directly references another Runtime assembly. Those should be treated as existing exceptions/leakage, not copied as the model for new modules.

### 2.3 Composition already provides the necessary escape hatch

The repository already contains combat/environment composition code that references both the combat prototype and concrete Vegetation runtime implementation.

This is the correct architectural idea:

- Combat Runtime may depend on `VoxelEngine.Vegetation.Api`.
- Vegetation Runtime implements that API.
- Composition may know both concrete runtimes when it constructs and connects them.

The production Combat migration should preserve and generalize this pattern.

### 2.4 Input is currently mixed into the combat presentation/controller

`ChainCombatLabController` currently performs several jobs at once:

- draws prototype UI;
- reads `Event.current`;
- interprets mouse buttons;
- converts pointer coordinates into board/grid coordinates;
- chooses contextual combat operations;
- directly calls board mutation methods such as movement and abilities.

The current coupling is effectively:

```text
Unity GUI input
    -> combat lab controller
        -> combat board mutation
```

The target is:

```text
hardware / Unity Input System
    -> Game.Input.Runtime
        -> Game.Input.Api
            -> CombatInputController
                -> CombatCommand
                    -> combat validation / simulation
```

The combat prototype therefore confirms at least one concrete case where input is in the wrong architectural place. A broader repository-wide audit should be performed during migration before claiming that every other input path has the same problem.

---

## 3. Goals

### 3.1 Combat goals

1. Promote the combat prototype into a production `Game.Combat` module.
2. Preserve deterministic combat authority.
3. Use normal game characters and enemies as combat participants.
4. Run combat in the normal world and scene.
5. Project tactical movement/targeting information onto actual world geometry.
6. Integrate environmental effects through owning module APIs.
7. Make Combat independently testable without Unity input or presentation.
8. Keep the existing combat lab usable during migration.
9. Expose a small Combat API rather than exposing the internal board/simulation.
10. Prepare the architecture for eventual replay, networking, and AI command sources without prematurely implementing them.

### 3.2 Input goals

1. Introduce `Game.Input` as a first-class game module.
2. Centralize Unity Input System and hardware/device knowledge.
3. Expose device-neutral player input through `Game.Input.Api`.
4. Support multiple local players without assuming a global singleton input state.
5. Support input contexts such as exploration, combat, and UI without feature modules fighting over controls.
6. Keep feature semantics out of Input.
7. Allow Combat to be driven by synthetic input or direct commands in tests.
8. Avoid direct `Input`, `Event.current`, `Keyboard`, `Mouse`, `Gamepad`, or `PlayerInput` usage in feature simulation code.

---

## 4. Non-Goals

The first migration does not require:

- networking or server replication;
- simultaneous online multiplayer;
- full campaign integration;
- every recruit or ability;
- final enemy AI;
- final animation/VFX/audio;
- large-scale pathfinding;
- complete voxel destruction integration;
- a universal world-interaction abstraction;
- a final serialized network command protocol;
- a complete input rebinding UX.

The architecture should leave room for these features without designing all of them now.

---

## 5. Architectural Principles

### 5.1 API assemblies are the external boundary

For any normal module:

```text
Module.Api
    ^
    |
Module.Runtime
```

Other feature modules may reference `Module.Api`.

They do not reference `Module.Runtime`.

Allowed exceptions are explicit composition roots, tests, and narrowly controlled editor/dev tooling.

### 5.2 Runtime folders are implementation subdivisions, not public modules

Combat may internally use folders such as:

```text
Simulation
Commands
Planning
Coordination
AI
World
Input
Presentation
Content
```

These are implementation areas inside one `Game.Combat.Runtime` assembly. Do not create separate public assemblies for each subdivision without a demonstrated cross-module need.

### 5.3 Public CLR visibility is not the module API

Unity may require some component classes to be public. That does not make them supported cross-module APIs.

Assembly references define the primary module boundary. Use `internal` by default where practical, but do not distort Unity component implementation solely to achieve CLR-level internal visibility.

### 5.4 The owner of a concept owns its semantics

**Voxel/world/character modules own generic capabilities**, for example:

- terrain sampling;
- solid-volume queries;
- structure geometry;
- vegetation lookup/damage;
- generic world traces;
- character rendering/animation capabilities.

**Combat owns combat semantics**, for example:

- combat cells;
- reachable cells;
- movement costs;
- attack/reaction ranges;
- turn/round state;
- combat commands;
- combat overlays;
- combat AI decisions.

**Input owns input concerns**, for example:

- devices;
- button/axis sampling;
- local input sources;
- action-map/context activation;
- rebinding/device pairing;
- device-neutral player control state.

Input does not own "move combatant", "uppercut", "select combat cell", or "end turn".

### 5.5 Composition owns concrete wiring, not feature logic

Composition may know concrete runtimes only as required to create objects and bind interfaces.

Combat rules do not belong in Composition.

Input interpretation does not belong in Composition.

### 5.6 Simulation is authoritative; presentation follows

The combat simulation determines logical position, action resolution, force/momentum, collisions/events, damage, reactions, and round state.

Presentation consumes authoritative results. Unity transforms and animation state never become combat truth.

---

## 6. Target Module Topology

```text
Assets/
  Game/
    Combat/
      Api/
        Game.Combat.Api.asmdef
      Runtime/
        Game.Combat.Runtime.asmdef
      Editor/
        Game.Combat.Editor.asmdef
      Tests/

    Input/
      Api/
        Game.Input.Api.asmdef
      Runtime/
        Game.Input.Runtime.asmdef
      Editor/                       # optional
        Game.Input.Editor.asmdef
      Tests/

    Composition/
      CombatRuntime/
        Game.Composition.CombatRuntime.asmdef
```

The exact Composition folder name may be adjusted to match repository conventions.

---

## 7. Target Dependency Graph

```text
                         external game features
                     Campaign / Encounters / etc.
                                |
                                v
                        +-----------------+
                        | Game.Combat.Api |
                        +-----------------+
                                ^
                                |
                        +---------------------+
                        | Game.Combat.Runtime |
                        +---------------------+
                         |      |      |     |
                         |      |      |     +------> Game.Input.Api
                         |      |      |
                         |      |      +------------> VoxelEngine.Vegetation.Api
                         |      |
                         |      +-------------------> VoxelEngine.Characters.Api
                         |
                         +--------------------------> world/terrain/structure APIs


                        +----------------+
                        | Game.Input.Api |
                        +----------------+
                                ^
                                |
                       +--------------------+
                       | Game.Input.Runtime |
                       +--------------------+
                                |
                                v
                        Unity Input System


             +-----------------------------------------+
             | Game.Composition.CombatRuntime          |
             |                                         |
             | allowed to reference concrete runtimes  |
             +-----------------------------------------+
                  |          |          |          |
                  v          v          v          v
               Combat      Input    Characters  Vegetation/World
               Runtime     Runtime    Runtime       Runtime
```

Forbidden normal dependencies:

```text
Game.Combat.Runtime -> Game.Input.Runtime
Game.Combat.Runtime -> VoxelEngine.Characters.Runtime
Game.Combat.Runtime -> VoxelEngine.Vegetation.Runtime

Game.Input.Runtime -> Game.Combat.Api
Game.Input.Runtime -> Game.Combat.Runtime

Any normal feature -> Game.Combat.Runtime
Any normal feature -> Game.Input.Runtime
```

Input must not depend on Combat.

---

## 8. `Game.Combat` Design

### 8.1 Combat API

The Combat API describes what the rest of the game can ask Combat to do. It should not expose the internal board.

Initial API concepts may include:

```text
ICombatService
CombatSessionId
CombatEncounterRequest
CombatParticipant
CombatParticipantId
CombatEncounterResult
CombatLifecycleState
```

Conceptually:

```csharp
public interface ICombatService
{
    bool IsActive { get; }

    CombatSessionId BeginCombat(CombatEncounterRequest request);
}
```

Completion may be represented by an event, session object, task, callback, observable state, or an existing game-flow mechanism. Select the form during implementation based on repository conventions rather than forcing a new async pattern here.

The API may expose:

- starting an encounter;
- identifying participants;
- observing high-level combat lifecycle;
- receiving the encounter result;
- possibly high-level cancellation/termination if normal gameflow needs it.

The API should not initially expose:

```text
ChainCombatBoard
Grid implementation details
ChainExecutionPlanner
ReactionReservationCoordinator
RoundReadinessCoordinator
EnemyTacticalAI
Reachability algorithm
CombatGridOverlay
Unity GameObjects
Animators
InputAction
PlayerInput
```

An internal type should only move into `Game.Combat.Api` when another real module needs it.

---

## 9. Combat Runtime Internal Structure

Recommended structure:

```text
Game/Combat/Runtime/
    Simulation/
    Commands/
    Actions/
    Planning/
    Coordination/
    AI/
    World/
    Input/
    Presentation/
    Content/
```

### 9.1 Simulation

Owns deterministic state and rules.

Likely contents:

```text
CombatBoard
CombatState
CombatUnit
GridPos
Force / momentum types
PhysicalEvent
Damage resolution
environmental combat event generation
round progression primitives
```

`CombatCore.cs` should be split into coherent files rather than moved intact.

`ChainCombatBoard` should be stripped of demo roster/setup assumptions.

### 9.2 Commands

Introduce a command boundary between control sources and authoritative mutation.

Conceptually:

```text
CombatCommand
MoveCommand
UseAbilityCommand
PassCommand
ReadyCommand
EndTurnCommand
SelectReactionCommand
```

Commands enter a single validation/execution boundary:

```text
CombatCommand
    -> validate against authoritative CombatState
        -> apply deterministic transition
            -> emit deterministic result/events
```

This prevents input/UI code from calling arbitrary board mutation methods directly.

Eventually all of these may feed the same command boundary:

```text
local input
combat UI
enemy AI
network command
replay
automated test
```

Do not expose or serialize `CombatCommand` through `Game.Combat.Api` until an actual external producer requires that stable contract.

### 9.3 Planning

Move `ChainExecutionPlan` and `ChainExecutionPlanner` here. Keep them internal.

### 9.4 Coordination

Move:

```text
ChainReactionReservationCoordinator
ChainRoundReadinessCoordinator
ChainPlanApprovalCoordinator
```

here and keep them internal.

### 9.5 AI

Move `ChainEnemyTacticalAI` here.

Prefer having AI produce the same validated Combat commands used by human control rather than mutating the board through a second authority path.

### 9.6 World

Owns Combat's interpretation of generic world information.

Examples:

```text
CombatGrid
CombatCell
CombatWorldProjection
CombatOccupancy
combat-specific environment event adaptation
```

Combat may consume generic APIs from terrain, structures, vegetation, or spatial systems.

Avoid immediately inventing one giant `ICombatWorld` interface containing every possible world system. Add narrow abstractions only where Combat owns the abstraction.

### 9.7 Input

This folder is not the global Input system. It is where Combat interprets `Game.Input.Api`.

Likely contents:

```text
CombatInputController
CombatInputState
CombatCommandFactory
CombatCommandDispatcher
```

Responsibilities:

- read device-neutral player intent;
- interpret that input based on current Combat state;
- resolve pointer targeting into combat cells;
- produce Combat commands;
- never mutate the board directly.

### 9.8 Presentation

Move visual-only functionality here:

```text
ChainCombatMotionPlayback
ChainCombatEventMarker
ChainCombatActivationOverlay
ChainEnemyIntentOverlay
CombatGridOverlay
combat cursor / targeting visuals
```

Presentation observes deterministic state/events. It does not become authority.

### 9.9 Content

Production abilities, recruit archetypes, enemy combat definitions, or authored combat content may eventually live here if those concepts belong specifically to Combat.

Demo scenario setup does not belong in the production simulation core.

---

## 10. `Game.Input` Design

### 10.1 Why Input belongs under `Game`

The desired module handles player-control policy for this game:

- exploration controls;
- combat controls;
- UI navigation;
- local player/device assignment;
- game action maps.

Those are game concepts, not generic voxel-engine concepts.

Therefore the initial location is:

```text
Assets/Game/Input/
```

Do not create `VoxelEngine.Input` merely because the implementation uses Unity. If a truly engine-generic input utility later emerges from demonstrated reuse, it can be extracted separately.

### 10.2 Input API responsibilities

`Game.Input.Api` should expose device-neutral player controls.

Potential concepts:

```text
LocalPlayerId
IPlayerInputReader
PlayerInputSnapshot
IInputContextService
InputContextId
IInputContextLease
```

Illustrative shape only:

```csharp
public readonly struct PlayerInputSnapshot
{
    public Float2 Move;
    public Float2 Look;
    public Float2 PointerPosition;

    public bool PrimaryPressed;
    public bool SecondaryPressed;
    public bool ConfirmPressed;
    public bool CancelPressed;
}
```

The exact field set must be derived from real controls rather than freezing this example as the final contract.

Do not leak Unity Input System types through `Game.Input.Api` unless there is a compelling reason:

```text
InputAction
InputActionMap
InputActionAsset
PlayerInput
Keyboard
Mouse
Gamepad
InputControl
```

The API should ideally be usable by tests without loading Unity's Input System.

### 10.3 Input Runtime responsibilities

`Game.Input.Runtime` owns:

- Unity Input System integration;
- `InputActionAsset` and action maps;
- keyboard/mouse/gamepad implementation;
- action sampling;
- input-device pairing;
- local-player association;
- control-scheme switching;
- rebinding implementation;
- context/action-map activation;
- translation from Unity input into API-level snapshots/events.

### 10.4 Multiple local players

Do not design the module around a global static input singleton.

The combat prototype is already multiplayer-oriented, even if currently hot-seat. The contract should make player/input-source identity explicit:

```text
LocalPlayerId
    -> player input source
```

This avoids a later rewrite for:

- multiple controllers;
- shared/split-screen local play;
- hot-seat control;
- mixed keyboard/controller;
- per-player rebinding.

Online players will eventually enter Combat through a network/command path rather than `Game.Input`.

### 10.5 Input contexts

The game needs one place to decide which input consumer currently owns controls.

Typical situations:

```text
exploration
combat
inventory/menu
dialogue
modal UI
cutscene
disabled
```

The Input API should expose a context mechanism without forcing Input to understand feature internals.

Possible model:

```csharp
IInputContextLease PushContext(InputContextId context);
```

with stack/priority semantics.

Conceptually:

```text
Exploration input
    |
Combat begins
    v
Combat context pushed
    |
Menu opens
    v
UI context pushed
    |
Menu closes
    v
Combat context resumes
    |
Combat ends
    v
Exploration context resumes
```

The exact mechanism may instead use explicit enable/disable handles if that matches repository conventions better.

Important invariant:

> Two unrelated feature controllers must not independently consume the same gameplay input merely because both MonoBehaviours happen to be enabled.

### 10.6 Input does not own feature semantics

Wrong:

```text
Game.Input.Api:
    MoveCombatant()
    SelectCombatCell()
    UppercutPressed
    EndCombatTurnPressed
```

Right:

```text
Game.Input.Api:
    pointer position
    primary action
    secondary action
    confirm
    cancel
    navigation/movement
```

Then:

```text
CombatInputController:
    current combat mode + player intent
        -> MoveCommand / UseAbilityCommand / etc.
```

This keeps dependency direction clean:

```text
Combat -> Input.Api
Input -X-> Combat
```

---

## 11. Pointer and Tactical-Grid Input

Pointer handling is a useful responsibility-separation example.

### Input Runtime owns

```text
mouse/touch/controller pointer
screen-space position
press/release state
device source
```

### Combat presentation/input owns

```text
which camera is relevant
screen point -> world ray
world ray -> terrain/world hit
world hit -> combat cell
current selection/targeting mode
whether the cell is a valid target
which Combat command the input represents
```

Input should never know about `GridPos`, terrain cells, or combat ranges.

The current prototype's board-mouse handling should therefore be decomposed rather than moved wholesale.

---

## 12. Character Integration

Combat should use normal production actors.

Conceptually:

```text
Normal player actor
      <-> CombatParticipantId

Normal enemy actor
      <-> CombatParticipantId
```

Combat creates deterministic participant state but does not replace the world actor with a combat-only visual object.

During an encounter:

1. normal actors remain in the scene;
2. Combat becomes authoritative for combat-relevant logical movement/state;
3. Combat presentation drives the normal actor's visual movement/animation;
4. the normal character system remains responsible for rendering, skeleton, animation, equipment, and other character-owned concerns;
5. when combat ends, the same actors continue in exploration with resulting state/positions.

If Combat needs a character capability that is not currently in the Characters API, extend the Characters API with a generic character capability, not a combat-specific method.

Prefer concepts such as:

```text
character motion target
action animation playback
presentation binding
```

over:

```text
MoveToCombatGridCell
PlayUppercutForCombat
```

---

## 13. World and Terrain Integration

Combat owns tactical interpretation. The voxel/world modules own geometry.

Combat may need generic queries such as:

```text
sample surface
query floor height
test headroom
test solid occupancy
trace a volume
query slope
query structure obstruction
query vegetation/environment objects
```

Combat then decides:

```text
is this a valid combat cell?
what is its movement cost?
is it reachable?
is it in attack range?
can this reaction cross it?
```

Do not put combat reachability into VoxelEngine.

---

## 14. Tactical Grid Rendering

The tactical grid is a Combat presentation feature.

Logical flow:

```text
Combat computes logical valid/reachable cells
             |
             v
CombatGridOverlay projects cells onto live world geometry
             |
             v
renderer draws the overlay
```

The visual grid should conform to terrain/structures rather than assuming a flat board.

For example:

```text
logical cell
    -> sample supporting world surface
    -> determine display polygon/mesh
    -> render slightly above actual surface
```

The renderer may consume generic world/rendering APIs but remains owned by `Game.Combat.Runtime/Presentation`.

The voxel renderer should not know that an overlay means "movement range".

---

## 15. Environmental Effects

The prototype already has a useful pattern around vegetation.

Combat produces semantic effects such as:

```text
tree impact
tree felled
force/direction
```

Vegetation owns actual tree lookup, damage, mutation, and rendering consequences.

The current vegetation bridge should be split by responsibility:

### Combat side

Combat-owned semantic contract/event representation.

### World/vegetation side

Adapter from Combat semantics to the existing Vegetation API.

### Composition

Concrete construction when both runtime implementations must be known.

Do not immediately replace this with a universal `ICombatWorld` mega-interface. Add additional environment integrations incrementally as real requirements appear.

---

## 16. Composition Design

The existing combat/environment composition area should evolve into the production Combat composition root.

Possible target:

```text
Assets/Game/Composition/CombatRuntime/
```

Its responsibility is construction:

```text
create Combat runtime
obtain/bind Input implementation
obtain/bind Character implementation
obtain/bind Vegetation implementation
obtain/bind Terrain/World implementation
connect encounter lifecycle
publish ICombatService to callers
```

It should not contain movement rules, attack rules, input interpretation, tactical-grid algorithms, or AI decisions.

---

## 17. Existing File Migration Map

| Existing file | Target | Migration notes |
|---|---|---|
| `CombatCore.cs` | `Game/Combat/Runtime/Simulation/` | Split into coherent deterministic types |
| `ChainCombatBoard.cs` | `Runtime/Simulation/` | Keep authority; remove demo roster/setup assumptions |
| `ChainExecutionPlan.cs` | `Runtime/Planning/` | Internal |
| `ChainExecutionPlanner.cs` | `Runtime/Planning/` | Internal |
| `ChainEnemyTacticalAI.cs` | `Runtime/AI/` | Prefer command production rather than direct board mutation |
| `ChainReactionReservationCoordinator.cs` | `Runtime/Coordination/` | Internal |
| `ChainRoundReadinessCoordinator.cs` | `Runtime/Coordination/` | Internal |
| `ChainPlanApprovalCoordinator.cs` | `Runtime/Coordination/` | Internal |
| `ChainCombatMotionPlayback.cs` | `Runtime/Presentation/` | Presentation follows authority |
| `ChainCombatEventMarker.cs` | `Runtime/Presentation/` or debug tooling | Decide based on production need |
| `ChainCombatActivationOverlay.cs` | `Runtime/Presentation/` | Combat-owned visual |
| `ChainEnemyIntentOverlay.cs` | `Runtime/Presentation/` | Combat-owned visual |
| `ChainCombatVegetationBridge.cs` | Split between Combat world semantics + adapter/composition | Do not keep interface and concrete cross-system adapter conflated |
| `ChainCombatDemoScenario.cs` | Temporary lab/dev content | Do not move into production simulation |
| `ChainCombatDemoGuide.cs` | Temporary lab/dev content | Delete eventually |
| `ChainCombatLabController.cs` | Split | Lab view remains temporary; input and direct mutation are extracted |
| `ChainCombatSetupActionsPanel.cs` | Temporary lab UI | Route actions through Combat command boundary |
| `CombatPrototypeController.cs` | Temporary bootstrap | Replace with production composition/lifecycle |
| `Editor/CombatPrototypeMenu.cs` | `Game/Combat/Editor` temporarily | Delete or retain intentionally as dev tooling |
| `CHAIN_COMBAT_VERSION` | Prototype-only | Remove when prototype shell is retired |

---

## 18. `ChainCombatLabController` Decomposition

Treat this as a deliberate refactoring task rather than simply moving the file.

Current responsibilities:

```text
lab/demo rendering
prototype UI
mouse event reading
board hit testing
combat action selection
direct board mutation
status/debug presentation
```

Target:

```text
ChainCombatLabView / dev harness
    - draws temporary lab UI only
    - invokes normal Combat command/control surfaces

Game.Input.Runtime
    - reads mouse/controller/device input

CombatInputController
    - converts neutral player intent into combat selection/commands

CombatGridProjection / targeting helper
    - converts pointer/world hit into combat cell

CombatCommandDispatcher
    - validates and applies commands

Combat presentation
    - renders status/grid/intent/markers
```

This decomposition is one of the first useful migration steps because it removes a major coupling while the lab still provides a fast test harness.

---

## 19. Migration Plan

### Phase 0 — Audit and freeze architecture rules

Before moving code:

- [ ] inventory all Combat prototype files and dependencies;
- [ ] inventory current direct Unity input usage across `Assets/Game`, `Assets/VoxelEngine`, and the prototype;
- [ ] identify existing Input System assets/action maps if any;
- [ ] identify existing character/world APIs required for the first vertical slice;
- [ ] document current Runtime-to-Runtime dependency exceptions encountered.

Deliverable: dependency inventory and agreed module rules, with no behavior change.

### Phase 1 — Create `Game.Input`

- [ ] create `Game/Input/Api`;
- [ ] create `Game/Input/Runtime`;
- [ ] create Input tests;
- [ ] move or wrap existing global/device-level input behavior behind the new Runtime;
- [ ] keep feature semantics out of Input;
- [ ] add a context/ownership mechanism so Combat and exploration cannot accidentally consume the same controls simultaneously.

### Phase 2 — Create `Game.Combat`

- [ ] create `Game/Combat/Api`;
- [ ] create `Game/Combat/Runtime`;
- [ ] create `Game/Combat/Editor`;
- [ ] create Combat tests;
- [ ] establish asmdef dependencies before large code movement so the compiler enforces the intended boundaries.

### Phase 3 — Move deterministic Combat core

Move/refactor:

```text
CombatCore
ChainCombatBoard
execution planning
coordination
enemy AI
```

- [ ] split oversized mixed files;
- [ ] remove demo-specific roster/setup from simulation;
- [ ] make Runtime implementation private by default;
- [ ] preserve deterministic behavior;
- [ ] keep the old lab running against the newly located production Runtime.

No normal-world integration is required yet.

### Phase 4 — Add Combat command boundary

- [ ] introduce Combat commands, validation, and dispatch/execution;
- [ ] stop UI/input/AI from invoking arbitrary board mutation methods directly;
- [ ] route lab buttons through commands first;
- [ ] preserve current lab behavior while changing authority boundaries.

Target flow:

```text
lab click/button
    -> command
        -> validated Combat mutation
```

### Phase 5 — Separate Input from the lab

Replace `Event.current` and direct click-to-board mutation with:

```text
Game.Input.Api
    -> CombatInputController
        -> targeting/projection
            -> CombatCommand
```

- [ ] route hardware/device input through `Game.Input.Runtime`;
- [ ] route Combat interpretation through `CombatInputController`;
- [ ] keep prototype `OnGUI` buttons only as temporary dev UI if still useful;
- [ ] ensure temporary dev UI uses the same Combat command path rather than direct board mutation;
- [ ] ensure Combat simulation has no dependency on Unity input.

### Phase 6 — Bind normal characters/enemies

- [ ] integrate existing production actor objects;
- [ ] replace prototype-only visible combatants with bindings to normal actors;
- [ ] keep Combat authoritative for deterministic combat state;
- [ ] keep character systems responsible for presentation capabilities;
- [ ] retain the lab as an encounter launcher temporarily if useful.

### Phase 7 — Live-world tactical grid

- [ ] replace flat prototype board assumptions with projection onto the real world;
- [ ] add only the generic world APIs actually needed for surface, occupancy, headroom, slope, and obstruction;
- [ ] implement `CombatGrid` / world projection;
- [ ] render a Combat-owned grid overlay on actual terrain;
- [ ] support the minimum world geometry needed for the first vertical slice before generalizing.

### Phase 8 — Environment integration

- [ ] migrate existing vegetation/tree behavior through production module boundaries;
- [ ] integrate additional environment systems only when required.

Possible later integrations include voxel terrain destruction, structures, doors, props, water, and other destructibles.

### Phase 9 — Normal encounter lifecycle

Connect ordinary gameflow:

```text
exploration
    -> encounter detected
        -> ICombatService.BeginCombat(...)
            -> Combat input context
            -> combat
            -> result
        -> release Combat input context
    -> exploration resumes
```

- [ ] keep the same scene;
- [ ] keep the same actor objects;
- [ ] preserve resulting positions/state;
- [ ] ensure input context switches automatically with lifecycle.

### Phase 10 — Retire `Assets/CombatPrototype`

Once the production vertical slice works:

- [ ] delete obsolete prototype bootstrap/controllers;
- [ ] delete demo-only guide/version artifacts;
- [ ] move intentionally retained tooling into `Game/Combat/Editor` or an appropriate dev-tool location;
- [ ] remove the old prototype asmdef;
- [ ] remove obsolete composition glue.

---

## 20. First Production Vertical Slice

The first integration milestone should be deliberately small.

Acceptance flow:

```text
player explores normal world
    ->
encounters one normal enemy
    ->
no scene change
    ->
ICombatService starts an encounter
    ->
same player/enemy actors become combat participants
    ->
Combat input context becomes active
    ->
reachable cells display on actual voxel terrain
    ->
player selects a reachable cell
    ->
CombatInputController emits MoveCommand
    ->
Combat validates and updates authoritative state
    ->
normal character presentation follows the result
    ->
player performs one existing combat ability
    ->
enemy resolves enough behavior to complete the encounter
    ->
combat ends
    ->
grid disappears
    ->
Combat input context is released
    ->
same actors continue normal gameplay
```

This proves the architecture before porting the entire chain-reaction feature set.

---

## 21. Architectural Acceptance Criteria

### Module boundaries

- [ ] `Game.Combat.Api` exists.
- [ ] `Game.Combat.Runtime` exists and references its own API.
- [ ] `Game.Input.Api` exists.
- [ ] `Game.Input.Runtime` exists and references its own API.
- [ ] Normal feature modules do not reference `Game.Combat.Runtime`.
- [ ] Normal feature modules do not reference `Game.Input.Runtime`.
- [ ] `Game.Combat.Runtime` does not reference other feature Runtime assemblies.
- [ ] `Game.Input` does not reference `Game.Combat`.
- [ ] Concrete cross-runtime wiring occurs only in Composition/editor/test exceptions.

### Input

- [ ] Combat simulation contains no `Event.current`.
- [ ] Combat simulation contains no direct `Input.*`.
- [ ] Combat simulation contains no `Keyboard.current`.
- [ ] Combat simulation contains no `Mouse.current`.
- [ ] Combat simulation contains no `Gamepad.current`.
- [ ] Combat API exposes no Unity Input System types.
- [ ] Combat can be exercised from tests using synthetic input or direct commands.
- [ ] Multiple local input-source identities can be represented without redesigning the API.
- [ ] UI/modal contexts can temporarily take ownership without multiple gameplay systems processing the same input.

### Combat authority

- [ ] Input/UI cannot mutate the combat board directly.
- [ ] AI does not need a separate unchecked mutation path.
- [ ] Commands are validated against authoritative Combat state.
- [ ] Unity transforms never feed authoritative combat position back into simulation.
- [ ] Production actors remain the visible actors during combat.

### World integration

- [ ] Combat reachability stays in Combat.
- [ ] Generic spatial/terrain queries stay in owning world/VoxelEngine APIs.
- [ ] Tactical grid rendering stays in Combat presentation.
- [ ] Environment mutations go through owning module APIs.
- [ ] No separate combat scene/world is required for the first production slice.

---

## 22. Testing Strategy

### Combat simulation tests

Operate without Unity input, GameObjects, camera, rendering, animation, or an actual terrain scene.

Test:

- command validation;
- deterministic movement;
- abilities;
- reactions;
- force/momentum;
- round coordination;
- enemy command choice where deterministic;
- invalid command rejection.

### Combat input-controller tests

Supply synthetic `Game.Input.Api` state and verify:

```text
pointer/confirm state + Combat mode
    -> expected CombatCommand
```

These tests verify interpretation without testing hardware.

### Input Runtime tests

Test Unity Input System bindings separately:

```text
device action
    -> expected PlayerInputSnapshot / API state
```

### Integration tests

Use production composition to verify:

```text
Input Runtime
    -> Input API
    -> Combat controller
    -> Combat command
    -> simulation
    -> presentation/world binding
```

The first full-world test should mirror the production vertical slice.

---

## 23. Risks and Design Traps

### 23.1 Turning `Game.Input.Api` into a giant game-event bus

Avoid exposing every feature command as an Input action. Input represents controls/player intent, not feature behavior.

### 23.2 Making Combat API mirror the entire board

The fact that a prototype class is public does not mean it belongs in the production API. Expose only cross-module lifecycle concepts.

### 23.3 Building a giant `ICombatWorld`

Do not aggregate terrain, vegetation, structures, characters, water, doors, physics, and destruction into one broad service before requirements justify it.

Prefer narrow owning APIs and small Combat-owned adapters where semantic translation is necessary.

### 23.4 Letting presentation become authority

Normal actor transforms may lag, interpolate, animate, or be interrupted visually. Combat state remains authoritative.

### 23.5 Hiding Runtime-to-Runtime coupling inside convenience helpers

If Combat needs a concrete Vegetation/Character/World Runtime class to work, either the owning module is missing an API capability or the construction belongs in Composition.

### 23.6 Over-designing networking now

A command boundary is worth adding now. A finalized wire protocol is not.

Keep commands internal until an actual networking/replay module needs a stable public representation.

### 23.7 Assuming one local player

Even if the first production vertical slice uses one keyboard/mouse, make input-source identity explicit. The existing combat design already assumes multiple player command groups.

---

## 24. Open Decisions for Implementation

Resolve these by inspecting existing APIs and code conventions during implementation rather than guessing:

1. **Combat lifecycle result style** — event, session object, task, callback, or existing game-flow mechanism.
2. **Stable actor identity** — which existing game/character identity type should `CombatParticipant` reference?
3. **Character presentation capability** — which existing Characters API can drive movement/action animation, and what genuinely generic capability is missing?
4. **World-query APIs** — which existing Terrain/Structures/spatial APIs already answer the first grid's surface and occupancy questions?
5. **Input System assets** — whether reusable action maps already exist elsewhere and should be moved or wrapped.
6. **Input context implementation** — stack/lease, priority ownership, or another existing lifecycle convention.
7. **Local multiplayer device assignment** — exact ownership/pairing model for keyboard, mouse, and gamepads.
8. **Combat grid geometry** — explicit rules for vertical differences, stairs, cliffs, structures, and multi-level cells.
9. **Command granularity** — how much targeting state lives in UI/controller versus the final authoritative command payload.

---

## 25. Recommended First Implementation PRs

### PR 1 — Module scaffolding

- add `Game.Input.Api` / `Game.Input.Runtime`;
- add `Game.Combat.Api` / `Game.Combat.Runtime`;
- add architecture/dependency tests if an existing mechanism supports them;
- no gameplay behavior change.

### PR 2 — Combat core promotion

- move deterministic core/planning/coordination/AI;
- remove demo roster assumptions;
- keep lab working.

### PR 3 — Combat command boundary

- add command validation/dispatch;
- route lab buttons through commands;
- no hardware-input migration yet.

### PR 4 — Input abstraction

- implement initial `Game.Input.Api`;
- wrap actual Unity input in Runtime;
- add input context ownership;
- route combat lab pointer/control input through `CombatInputController`.

### PR 5 — Normal actor binding

- use existing production characters/enemies;
- keep combat in lab/controlled encounter if needed.

### PR 6 — Live-world grid

- add required generic world queries;
- project/reachability-test cells on actual terrain;
- render grid overlay.

### PR 7 — Encounter lifecycle

- begin/end Combat from normal game flow;
- same actors and scene;
- input context switches automatically.

### PR 8+ — Expand environment/content

- vegetation migration;
- additional abilities;
- environmental destruction;
- stronger AI;
- cleanup prototype shell.

---

## 26. Final Architectural Rules

Treat these as invariants for the migration:

1. Combat is a `Game` module, not a VoxelEngine module.
2. Input is a `Game` module, not a Combat subsystem and not initially a VoxelEngine module.
3. Outside modules depend on `Game.Combat.Api`, never `Game.Combat.Runtime`.
4. Outside modules depend on `Game.Input.Api`, never `Game.Input.Runtime`.
5. Combat Runtime depends on other modules' APIs, not their Runtimes.
6. Input never depends on Combat.
7. Composition is the explicit cross-runtime wiring seam.
8. Combat owns combat rules and deterministic authority.
9. Input owns devices and device-neutral player controls, not gameplay semantics.
10. Input/UI/AI submit validated Combat commands rather than mutating the board directly.
11. Combat uses normal production characters and enemies.
12. Combat runs in the normal world/scene.
13. Voxel/world modules expose generic geometry/world capabilities; Combat interprets them tactically.
14. The tactical grid is a Combat presentation feature projected onto real geometry.
15. Unity transforms and animation never become authoritative Combat state.
16. Prototype demo content remains a migration harness until production integration works, then is removed or intentionally retained as tooling.
17. Do not generalize interfaces beyond demonstrated cross-module needs.
18. Keep the lab functioning through intermediate phases so architecture changes remain observable and testable.

---

## Decision

Proceed with two production modules:

```text
Game.Combat.Api
Game.Combat.Runtime

Game.Input.Api
Game.Input.Runtime
```

with Composition as the only normal location allowed to bind concrete runtime implementations across module boundaries.

The first implementation target is not "port every combat feature." It is to prove the architecture end-to-end with one normal-world encounter, normal actors, live-world movement cells, the new Input boundary, a validated Combat command path, and return to normal exploration.
