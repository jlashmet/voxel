# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters where Characters, Encounters, Vitality, and Input meet Combat. Do **not** create a second combat runtime.

## Acceptance / current state

- Combat binds real `CharacterId` and `EncounterId`, keeps only combat team/session/result semantics, and leaves role-to-team, winner-to-`EncounterResolution`, spawning, cleanup, and presentation policy in composition.
- Vitality must be the sole production life-state authority. Combat retains round/readiness, tactical-intent, reaction, plan/history, board/motion, and combat-resolution state. Input remains semantic through `Game.Input.Api`.
- Reusable seams are implemented: `CombatParticipant.FromCharacter`, `CombatStartRequest`/`CombatStartResult`, `CombatResolved`, `EncounterCombatCoordinator`, and `CombatService(IVitalityService)`. Independent Character, Encounter, and `IVitalityService` fixtures cover those boundaries.
- `fixes/agent-3` has merged current master `f5593cc1236ba3963fc5713a11df35292628e97d`, including the GPU renderer restoration.
- Exact feature `0669bd2ed9981fdba6bff9c8c0abb9ba3290a8e0` passed exact-SHA run `33800856291`: all five affected automatic EditMode assemblies passed; Kentridge built, completed its 80-second player scenario, produced seven real-player captures, and exited cleanly. The previous renderer teardown failure no longer reproduces on this baseline.
- `KentridgeForestBanditEncounter` still constructs the parameterless legacy `CombatService`. Current master has `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`, but no `Assets/Game/Vitality/Runtime`; production cutover is externally blocked and agent-3 will not invent or copy System 02 Runtime ownership.
- Dependency inspection shows System 02 has implemented `Game.Vitality.Runtime.VitalityRegistry : IVitalityService` on `fixes/agent-9`, including registration, authoritative damage, defeat events, capture, and restore. It is not yet published to `master`, so agent-3 will not merge/cherry-pick another assignment branch; the blocker is publication/integration availability on master.

## Hypotheses / discriminating results

1. **A production Vitality Runtime is required before duplicate Kentridge HP authority can be removed.** Current-master inspection confirms the Runtime is absent, while the dependency branch confirms the intended concrete implementation exists but has not landed. Therefore T01-002/010/011/012/014/015 and their assembled follow-ons remain blocked on publication of System 02 through normal ownership flow.
2. **The renderer restoration resolves the prior assembled-player teardown blocker for agent-3's current baseline.** Confirmed: exact run `33800856291` passed affected CharacterAI, Combat, Continuity, GameplayReplication, and Vitality tests and the Kentridge player completed and exited cleanly. No Combat change is warranted for the old teardown symptom.

## Selected approach / boundaries

- Preserve master contracts/GUIDs and add only Combat-required Vitality damage/service semantics.
- Do not patch renderer teardown, create a fake Vitality implementation, copy System 02 Runtime, or merge/cherry-pick another assignment branch into agent-3.
- Keep the current green run as baseline evidence only; it cannot validate the not-yet-landed production Vitality cutover.
- When the real Vitality Runtime is published to master, merge current master, inspect the landed API/runtime, inject it in Kentridge composition, register/bind production CharacterIds, remove production use of the parameterless Combat health path, and keep Encounter/game-result policy in composition.
- Do not refactor the older experimental CombatPrototype chain; its orchestration state is outside this production life-authority migration.

## Remaining gates

- Real Vitality Runtime publication to master and Kentridge production cutover.
- Final bypass/one-authority audit and independent reuse confirmation in assembled production.
- Fresh exact-SHA automatic module plus Kentridge player validation after the production cutover; inspect durable built-player evidence.
- Complete T01-023/025/030/031/032, populate closure fields, move `open/` directly to `closed/`, merge current master, and non-force push the exact feature head to master only after every required gate is green.
