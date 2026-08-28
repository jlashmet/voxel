# Plan: Combat and Input Module Integration

## Observed behavior

The useful combat prototype lives under `Assets/CombatPrototype` rather than the normal Game module structure and currently combines deterministic combat simulation, orchestration, enemy AI, presentation, direct mouse/UI input, lab/demo content and limited world integration. In particular, `ChainCombatLabController` reads Unity GUI input, converts pointer coordinates to combat coordinates, chooses contextual actions and directly mutates combat state. The repository already has a preferred `Api`/`Runtime` module boundary and composition roots that can wire concrete runtimes without exposing Runtime assemblies cross-module.

## Acceptance criteria

- Production combat lives behind `Game.Combat.Api` with implementation in `Game.Combat.Runtime`.
- `Game.Input.Api` exposes device-neutral per-player intent; `Game.Input.Runtime` owns Unity Input System/device knowledge.
- Combat simulation does not read `Input`, `Event.current`, `Keyboard`, `Mouse`, `Gamepad` or `PlayerInput`.
- Input knows nothing about combat turns, cells, abilities or mutation semantics.
- Intent is translated into validated deterministic combat commands before authoritative state changes.
- Normal world, characters and enemies remain present during combat; no parallel combat-only world/model becomes authority.
- Cross-feature integration uses owning module APIs; concrete Runtime-to-Runtime wiring is confined to composition roots.
- Existing deterministic integer/grid authority is preserved; presentation transforms never feed back into simulation.
- Combat can be tested with synthetic commands/input and without Unity presentation.
- The existing combat lab remains usable until equivalent production composition is proven.

## Competing hypotheses

1. Move the prototype wholesale into `Game.Combat` and clean boundaries afterward. This minimizes initial edits but risks preserving the current input/presentation/runtime coupling under new folders.
2. Establish Combat/Input API contracts and a composition seam first, then migrate deterministic simulation and presentation incrementally while the lab remains a compatibility host.

**Selected approach:** hypothesis 2.

## Next discriminator

Extract one complete player action path: device-neutral input intent → combat input/controller translation → deterministic command validation → simulation mutation, with no Unity input dependency inside Combat. Drive the same command from a synthetic test source. If the prototype simulation cannot be invoked without lab/presentation state, isolate that coupling before moving files.

## Remaining gates

Inventory prototype dependencies and existing direct Runtime references; define minimal Combat/Input APIs; migrate deterministic core; adapt world/character/enemy/environment composition; migrate presentation and AI; remove direct feature input reads; preserve the lab during transition; then retire compatibility code. Validate deterministic command results, module-reference rules, multi-player input isolation/context switching, normal-world integration, behavioral regressions, targeted CI and final SceneIssue replay/visual evidence where applicable.
