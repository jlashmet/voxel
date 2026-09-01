# 24. Production-composed built-player vertical slice

**Status:** Approved

## Purpose

Make `KentridgePlayableSlice` the canonical built-player proof of the production application/gameplay composition rather than a parallel showcase runtime that manually constructs simplified versions of gameplay systems.

The defining rule is:

> The vertical slice proves the production application composition by playing through real public system boundaries in a standalone player; it may simplify content, but it may not substitute simplified runtime implementations.

The repository's validation architecture already designates `KentridgePlayableSlice` as the canonical assembled-game standalone-player integration gate. System 24 strengthens that existing slice rather than adding another top-level integration scene.

## 1. Reuse Kentridge as the canonical assembled-game slice

Do not create a second production integration scene such as `FullGameValidation`, `IntegratedSlice2`, or another alternate top-level game shell.

Kentridge remains the existing canonical player-facing composition and CI integration location.

Module-local validation scenes prove individual player-visible modules. Kentridge proves that the production modules still work together in the assembled game.

## 2. Production application path is the path under test

The built-player slice must enter gameplay through the production application/session architecture:

```text
standalone player
    -> system 23 application frontend
        -> New Game / Continue
            -> system 14 game-session orchestration
                -> normal authoritative runtime graph
                    -> GameplayReady
```

Validation must not retain a separate path that manually constructs a campaign runtime, combat service, input service, or other duplicate gameplay graph merely because it is convenient for a scene test.

There should be one meaningful production composition path.

## 3. Kentridge remains content/composition, not a second application runtime

Kentridge-specific facts remain in Kentridge composition/content, including demonstrated authored concerns such as:

- world seed and generated settlement/corridor composition;
- opening campaign/cutscene content;
- Kentridge/Hightown authored relationship;
- pub/site realization;
- forest encounter placement and local presentation choices;
- Kentridge-specific NPC/objective/campaign refs.

Shared runtime creation, lifecycle, input, combat, interaction, persistence, and presentation must flow through their owning production systems.

System 14 must not absorb Kentridge-specific rules merely to make the slice work.

## 4. Evolve the existing slice instead of accepting legacy direct construction

The current Kentridge scene/runtime already exercises substantial real production code, but some scene components directly construct or tick lower-level runtimes.

System 24 treats such wiring as transitional where it bypasses the approved system boundaries.

Examples to eliminate from the accepted production proof include:

- scene-local construction of a second `CombatService`;
- scene-local construction of an independent `InputContextService`;
- manually constructing a parallel gameplay graph instead of system 14;
- hard-coded physical input handling that bypasses the shared semantic input path;
- scene-local replacements for HUD/audio/VFX/interaction systems.

Useful Kentridge-specific content/placement logic may remain or be refactored behind the correct owner boundaries.

## 5. One shared local input stack

System 24 is the assembled-game proof of the input-device architecture.

Conceptually:

```text
LocalPlayerId
    -> Unity Input System local user/device pairing
        -> Game.Input.Runtime
            -> Game.Input.Api semantic actions/contexts
                -> exploration / combat / interaction / UI
```

Exploration, combat, dialogue, interaction, HUD, and menus must not each create independent device readers or context services.

Combat temporarily pushes `Combat` onto the same shared context stack. Menus push `Ui` onto that same stack. Closing/removing the owner restores the previous context.

Kentridge-specific gameplay must not depend on hard-coded `KeyCode` or mouse-button checks as the accepted production route.

## 6. Encounter/combat integration uses systems 05 and 01

The existing Kentridge forest-bandit content is a useful real acceptance fixture, but its production lifecycle should use the approved ownership chain:

```text
Kentridge authored encounter intent/placement
    -> system 12 realization bridge where applicable
        -> system 05 encounter activation/membership/lifecycle
            -> system 01 production combat integration
                -> existing Game.Combat runtime
```

Kentridge composition owns the authored forest encounter policy. It does not own a second generic encounter or combat implementation.

## 7. Character vitality uses the production character/vitality path

Damage and defeat exercised in the slice must update the authoritative character/vitality state owned by systems 02/03 and flow through ordinary replication/presentation adapters.

Do not treat combat-prototype health as the final assembled-game character authority if that bypasses the production vitality contract.

## 8. Interactions use the authoritative WorldObject bridge

At least one representative interaction in the slice must flow through system 13 rather than a Kentridge-only `if input then call object/NPC method` path.

The player provides semantic interaction intent. The authoritative interaction owner validates and executes it. Campaign/progression observes the resulting semantic fact where content requires it.

## 9. Exercise a representative cross-system chain, not every feature independently

System 24 is not another giant module test matrix.

A compact real gameplay chain should naturally cross many production boundaries, for example:

```text
Boot
-> frontend
-> New Game
-> production session composition
-> generated Kentridge
-> opening cutscene
-> GameplayReady

explore
-> movement/input/HUD
-> interaction
-> progression change

traverse generated world
-> streaming/collision/rendering
-> encounter activates
-> combat resolves
-> vitality/presentation feedback updates

loot/inventory interaction
-> authoritative inventory transaction
-> inventory presentation

progression observes resulting facts
-> objective/journal presentation updates

open/close in-game UI
-> shared input contexts restore correctly
```

The exact authored route may evolve with Kentridge content, but the proof must use production ownership boundaries.

## 10. Systems 17-22 use production presentation paths

Where the corresponding production capabilities exist, the slice must consume their actual presentation implementations:

- system 17 gameplay HUD;
- system 18 inventory UI;
- system 19 progression presentation;
- system 20 multiplayer/session presentation where relevant;
- system 21 audio;
- system 22 VFX.

Do not add Kentridge-only fallback health text, local effect calls, audio sources, or interaction prompts solely to make the vertical slice appear complete.

A missing production integration should fail/expose the gap rather than be hidden by a test substitute.

## 11. One compact persistence/continue round trip

System 24 should prove that the application shell can resume through the normal production graph.

A representative flow is:

```text
New Game
-> reach known semantic progress
-> system 16 save
-> tear down the gameplay runtime
-> system 23 Continue
-> system 14 creates the normal fresh runtime graph
-> system 16 restores authoritative state
-> GameplayReady
-> continue gameplay
```

This is integration proof, not exhaustive persistence testing.

There must not be a separate loaded-game runtime.

## 12. Built standalone player is mandatory acceptance evidence

Editor/PlayMode tests remain useful for fast behavioral regressions, but system 24 specifically proves the assembled application.

Its canonical acceptance therefore runs in the real standalone player through the repository's shared player build/capture harness.

Visual acceptance uses standalone-player output. PlayMode screenshots cannot substitute for it.

## 13. Reuse the repository's single shared player harness

Do not create a Kentridge-specific build runner or second standalone-player harness.

The generic harness remains feature-agnostic. Kentridge/module-owned scenario configuration may define its player actions, semantic checkpoints, camera/capture needs, and assertions.

The common harness must not contain hidden Kentridge-specific logic.

## 14. Automation acts like a player/operator, not a privileged runtime

A scenario driver may:

- request New Game or Continue through the production frontend seam;
- provide semantic/input-system player input;
- navigate/move/interact;
- operate production UI;
- wait for public semantic milestones;
- capture frames;
- assert public observable state.

It must not advance the scenario by directly mutating authoritative internals such as:

- marking objectives completed;
- forcing a combat winner;
- assigning inventory quantities directly;
- setting vitality directly;
- forcing WorldObject state;
- invoking private Kentridge progression hooks.

Tests drive the application. They do not become a cheat implementation of the game.

## 15. Prefer semantic milestones over arbitrary sleeps

Built-player automation should synchronize on meaningful observable milestones where practical, such as:

- `FrontEndReady`;
- session composing/ready lifecycle;
- `GameplayReady`;
- opening cutscene completed;
- controlled `CharacterId` established;
- objective active/completed;
- interaction accepted;
- encounter active/resolved;
- combat resolved;
- inventory transaction committed;
- restore/resynchronization completed.

Time bounds remain necessary to fail hung execution, but arbitrary sleeps must not be the primary correctness contract.

## 16. Fail on missing production integration instead of falling back

The slice must not silently replace missing production features with scene-local substitutes.

Examples:

- missing system-17 HUD does not justify a Kentridge-only health widget;
- missing system-21 integration does not justify direct Kentridge audio playback as acceptance;
- missing system-22 integration does not justify a scene-local particle call;
- missing system-13 interaction does not justify accepting direct input-to-NPC calls;
- missing system-14 composition does not justify keeping a separate Kentridge gameplay graph indefinitely.

System 24 is valuable precisely because it exposes these integration holes.

## 17. Keep module-local validation and assembled-game validation distinct

The repository validation model remains:

```text
focused module tests
    -> affected module-local built-player validation where player-visible
        -> Kentridge built-player assembled-game integration
```

Do not turn Kentridge into a replacement for focused tests or module-local visual scenes.

Likewise, module validation scenes do not prove that the entire application composes correctly.

## 18. System 24 is not the complete game loop

System 24 answers:

> Can the approved production systems actually run together through the production application path in a built player?

System 26 separately answers:

> Does the game have a coherent beginning-to-terminal-outcome full session/game progression loop?

The vertical slice therefore does not need to prove every campaign branch, final game outcome, complete content volume, or target session duration.

## Acceptance / reuse proof

### Production New Game path

1. Launch the standalone player into the system-23 frontend.
2. Request New Game through the production application seam.
3. Verify system 14 composes the ordinary authoritative graph.
4. Reach `GameplayReady`.
5. Verify the player is controlling the expected production character in generated Kentridge.

### Input/context integration

1. Drive movement and interaction through the shared semantic input path.
2. Enter combat and verify the shared context stack transitions to `Combat` rather than creating another input service.
3. Open an in-game UI and verify `Ui` owns input locally.
4. Close nested UI/combat ownership and verify the correct previous context resumes.

### Cross-system gameplay

1. Complete the opening/cutscene transition into player control.
2. Perform at least one authoritative interaction.
3. Observe a real progression change.
4. Activate a Kentridge encounter through the normal encounter lifecycle.
5. Resolve combat through production combat/vitality ownership.
6. Exercise a representative authoritative loot/inventory transaction.
7. Verify the relevant production presentation models update.

### Persistence/Continue

1. Reach known cross-domain semantic state.
2. Save through system 16.
3. Tear down the gameplay runtime completely.
4. Continue through system 23.
5. Recompose through system 14 and restore through system 16.
6. Verify current semantic state and stable identities are restored without replaying historical one-shot effects.
7. Continue normal gameplay.

### Shared-harness proof

Run the production Kentridge scenario through the same repository standalone-player harness used by module validation/SceneIssue player execution. No Kentridge-specific build harness is introduced.

### Headless/runtime boundary

The authoritative systems used by the slice remain capable of running without Kentridge UI/camera/presentation objects. The slice is composition and proof, not a new dependency required by reusable domains.

## Out of scope

- replacing module-local tests/validation scenes;
- creating another standalone-player harness;
- complete multiplayer end-to-end coverage — system 25;
- complete full-game/session progression and terminal pacing — system 26;
- Kentridge-specific content expansion merely to increase test breadth;
- redesigning renderer/world generation/individual gameplay systems when no integration defect requires it;
- test-only substitutes for missing production systems;
- privileged scenario commands that directly mutate gameplay authority.

## Architectural constraints

- Reuse `KentridgePlayableSlice` as the canonical assembled-game built-player location rather than adding a second top-level integration game.
- The accepted player path goes through system 23 and system 14 rather than a parallel manually constructed gameplay graph.
- Kentridge-specific policy remains in Kentridge content/composition.
- There is one shared local input user/context stack across exploration, combat, interaction, and UI.
- Production encounter/combat/vitality/interaction/inventory/progression ownership remains in systems 01-13 rather than scene-local replacements.
- Production presentation systems 17-22 are used where their corresponding capabilities exist; scene-local fallbacks cannot satisfy acceptance.
- Continue/restore uses systems 23 -> 16 -> 14 and the same normal runtime graph.
- The canonical proof is a real standalone-player run through the repository's single shared harness.
- Scenario automation drives public player/application seams and observes semantic milestones; it does not mutate authoritative internals directly.
- Module validation proves modules; Kentridge proves assembled-game integration.
- System 24 proves production composition, while system 26 owns complete game/session progression-loop design.
