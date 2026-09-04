# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters where Characters, Encounters, Vitality, and Input meet Combat. Do **not** create a second combat runtime.

## Acceptance / final state

- Combat binds real `CharacterId` and `EncounterId`, keeps only combat team/session/result semantics, and leaves role-to-team, winner-to-`EncounterResolution`, spawning, cleanup, and presentation policy in composition.
- Vitality is the production life-state authority. `CombatService` requires injected `IVitalityService`; Combat reads HP/alive state and applies damage only through Vitality while retaining positioning, turns, tactical execution, team/winner semantics, and combat-resolution state.
- Kentridge composes the landed `VitalityRegistry`, registers real Encounter-member `CharacterId`s, injects it into Combat, and creates participants through `CombatParticipant.FromCharacter`.
- Input remains semantic through `Game.Input.Api`; no Combat raw key/button polling was found in the final audit.
- Encounter integration remains semantic and exactly-once through `EncounterCombatCoordinator`; scene-specific role/team and winner/result policy remains in composition.
- Independent Character binding, Encounter registry, and Vitality fixtures prove the boundaries outside Kentridge.
- Current master `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a` is the merge base/ancestor of verified feature `4789867eb2aefc2eae96cc0b5ad75236b6bc0a82`; no later master changes were pending at closure.

## Validation evidence

- Baseline exact-SHA run `33800856291` proved the renderer-restored Kentridge path and clean teardown before final Vitality cutover.
- First post-cutover run `33811046206` failed before tests with CS7036 because an independent Encounter fixture retained the removed parameterless `CombatService` constructor. Commit `7db3fa76599ca7ed4b9e68b3db27e73fc588f4fe` migrated that fixture to real `VitalityRegistry` injection and registered its CharacterIds.
- Final exact-SHA run `33812677873`, request commit `c4c90b298d98adf5494982cf1eee6a39ecc48302`, validated feature `4789867eb2aefc2eae96cc0b5ad75236b6bc0a82` successfully.
- Required persistent EditMode assemblies all passed: `Game.CharacterAI.Tests`, `Game.Combat.Tests` (11 passed), `Game.Continuity.Tests`, and `Game.GameplayReplication.Tests`.
- The canonical `KentridgePlayableSlice` player built and completed its 80-second scenario with `assertion failures 0`, produced seven durable captures, and shut down cleanly.
- Final bypass audit found no parameterless `new CombatService()` on current master; production `CombatService` has only the injected Vitality constructor.

## Final authority audit

- **Vitality:** health, alive/defeated state, accepted combat damage.
- **Combat:** positioning, turns, combat actions, team/winner and terminal combat result.
- **Encounters:** participation, encounter lifecycle, terminal encounter resolution and cleanup facts.
- **Characters:** identity/kinematics and downstream lifecycle projection; Combat does not consult Character lifecycle as health authority.
- **Input:** semantic player snapshots/context rather than raw key/button policy inside Combat.

All implementation, reuse, blast-radius, built-player, and one-authority gates are complete. Closure bookkeeping is the only change after the verified feature SHA.
