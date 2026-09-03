# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime`; add only thin composition adapters where Encounters/Characters/Vitality/Input meet Combat. Do **not** create a second combat runtime.

## Dependencies

02 Vitality, 03 Characters, 04 autonomous character intent, 05 Encounters, existing `Game.Input.Api/Runtime` migration.

## Combat direction

Combat must support large parties without serial per-character turns. Organize controllable characters into player squads and resolve combat in **beats**:

- each beat, Combat deterministically selects one active member per participating player squad;
- the player chooses one deliberate action for that selected member;
- all players' accepted beat actions resolve in the same authoritative beat instead of as a 20–30-character turn queue;
- non-active squad members continue autonomous/basic behavior and may participate through configured combo interactions;
- the current and upcoming active-member sequence is authoritative/readable far enough ahead for players to plan squad builds around it.

Combos are **event-driven**, not primarily a large taxonomy of status-triggered procs. Actions expose transient semantic opportunities such as movement/launch/fall, projectile travel, impact, guarding, displacement, collision, spell casting, ally actions, or terrain/world alteration. Equipped combo behavior may **join**, **redirect**, or **transform/escalate** an in-flight action. Statuses such as burning/frozen/wet may be ingredients, but they are not the sole reaction grammar. Ally-to-ally, cross-player, spatial, and destructible-world interactions are valid.

Combat owns deterministic beat sequencing, active-member eligibility/selection, command acceptance, event ordering, and bounded chain resolution. Reaction graphs must have explicit recursion/work limits so a build cannot create an unbounded proc loop. Clients may preview/predict presentation; authority owns accepted commands and outcomes.

## Integration

1. Keep Character/Vitality/Encounter authority boundaries: Combat binds participants, routes damage through Vitality, and emits `CombatResolved`; Encounters owns encounter lifecycle.
2. Extend Combat API with renderer/UI-neutral beat, squad, active/upcoming member, action-choice, transient combo-opportunity and resolved-interaction facts as needed.
3. Keep combat input semantic through `Game.Input.Api`; no physical binding knowledge in Combat.
4. Replace scene-local combat/bootstrap ownership with production composition.

## Tests / proof

Prove deterministic beat selection/resolution, one deliberate action per player per beat, simultaneous multiplayer resolution, bounded event-driven chains, cross-character/cross-player interaction, vitality/encounter authority, and reusable non-Kentridge composition. #24 owns assembled built-player proof; #25 owns separate-process multiplayer proof.

## Do not build

No second combat runtime, final-boss/game-victory policy, scene-specific combat policy, or giant hardcoded status/reaction matrix.
