# Plan

## Evidence / target
- No marked circles; whole-frame acceptance is the note: implement the `design/combat-input-modules` migration seam, then put three forest bandits in Kentridge that enter combat on player proximity.
- Refreshed master has no `Assets/Game/Combat` or `Assets/Game/Input`; combat remains under `Assets/CombatPrototype`, so the production migration never landed.
- The live Kentridge controller is one continuous world and still reads WASD/mouse directly. Its authored `RegionThemeMap` places PineForest from 142 m to 362 m Z; the captured player pose at 155.2 m is inside that band.

## Competing hypotheses / discriminator
1. **Supported — missing production migration.** Combat/Input modules are absent, while the prototype remains; Kentridge therefore has no production lifecycle/input boundary to compose.
2. **Rejected — existing modules, missing scene wiring only.** Direct path probes on refreshed master return absent for both production modules.

## Fix / behavioral regression
- Add device-neutral `Game.Input.Api` plus Unity-owned `Game.Input.Runtime`; contexts are exclusive, and Combat samples then suppresses the legacy Unity frame so exploration cannot consume the same intent.
- Add `Game.Combat.Api` lifecycle contracts and deterministic `Game.Combat.Runtime` command validation; simulation never reads Unity input.
- Compose three persistent bandit actors at a semantic PineForest anchor derived from the authored Kentridge→Hightown theme corridor, not captured coordinates. Proximity begins one in-place Combat session with the same bandit identities and pushes Combat input ownership.
- Regression loads the real slice, requires exactly three PineForest bandits, moves the existing lead bandit into player proximity, and asserts active production Combat + Combat context + same scene + same actor identity + three enemy participants.

## Blast radius / cost
- New Combat/Input modules plus one Kentridge composition component and one PlayMode regression; no other scene/capture changes.
- Before encounter start: three planar squared-distance checks/frame and ground probes only while the player is within 96 m. Active Combat input/command dispatch is O(1); no steady-state collection allocation.

## Verification
- Fresh exact-SHA PlayMode CI must compile/pass the regression and saved-pose replay. Visually compare replay with the 1928×836 original: clean Kentridge forest, three readable bandits ahead of the player, no debug/editor/replay overlays.
