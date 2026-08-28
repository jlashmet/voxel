# Plan

## Evidence / target
- No marked circles; whole-frame acceptance is the note: implement the `design/combat-input-modules` migration seam, then put three forest bandits in Kentridge that enter combat on player proximity.
- Refreshed master has no `Assets/Game/Combat` or `Assets/Game/Input`; combat remains under `Assets/CombatPrototype`, so the production migration never landed.
- The live Kentridge controller is one continuous world and still reads WASD/mouse directly. Its authored `RegionThemeMap` places PineForest from 142 m to 362 m Z; the captured player pose at 155.2 m is inside that band.

## Competing hypotheses / discriminator
1. **Supported — missing production migration.** Combat/Input modules are absent, while the prototype remains; Kentridge therefore has no production lifecycle/input boundary to compose.
2. **Rejected — existing modules, missing scene wiring only.** Direct path probes on refreshed master return absent for both production modules.

## Fix / behavioral regression
- Add device-neutral `Game.Input.Api` plus Unity-owned `Game.Input.Runtime`; Combat samples then suppresses the legacy Unity frame so exploration cannot consume the same intent.
- Add `Game.Combat.Api` lifecycle contracts and deterministic `Game.Combat.Runtime` command validation; simulation never reads Unity input.
- Compose three persistent bandits at a semantic PineForest anchor derived from the authored Kentridge→Hightown corridor. Proximity begins one in-place Combat session with the same actors and Combat input ownership.
- Regression loads the real slice and proves exactly three PineForest bandits, proximity activation, Combat context, same scene, same actor object, and three enemy participants.
- Exact request `fa782d338872cf053bb3aab78f9e47abd70e4b8d` failed before tests/replay only because Unity 6000.5 makes test-only `GetInstanceID()` a CS0619 error. Replace the identity assertion with direct reference identity; production code had no compiler diagnostic.

## Blast radius / cost
- New Combat/Input modules plus one Kentridge composition component and one PlayMode regression; no other scene/capture changes.
- Before encounter: three planar squared-distance checks/frame and ground probes only within 96 m. Active Combat input/command dispatch is O(1); no steady-state collection allocation.

## Verification
- Fresh exact-SHA PlayMode CI must compile/pass the regression and saved-pose replay. Compare replay with the 1928×836 original: clean Kentridge forest, three readable bandits, no debug/editor/replay overlays.
