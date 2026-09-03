# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters where Characters, Encounters, Vitality, and Input meet Combat. Do **not** create a second combat runtime.

## Acceptance / current state

- Combat binds real `CharacterId` and `EncounterId`, keeps only combat team/session/result semantics, and leaves role-to-team, winner-to-`EncounterResolution`, spawning, cleanup, and presentation policy in composition.
- Vitality must be the sole production life-state authority. Combat retains round/readiness, tactical-intent, reaction, plan/history, board/motion, and combat-resolution state. Input remains semantic through `Game.Input.Api`.
- Reusable seams are implemented: `CombatParticipant.FromCharacter`, `CombatStartRequest`/`CombatStartResult`, `CombatResolved`, `EncounterCombatCoordinator`, and `CombatService(IVitalityService)`. Independent Character, Encounter, and `IVitalityService` fixtures cover those boundaries.
- Production-bearing feature baseline `d8d6bd560e2eb7cd0950f3283ee25806e2d2653a` merges master `b18d470f66221c7cb6091249f4683c2d994bffec`.
- `KentridgeForestBanditEncounter` still constructs the parameterless legacy `CombatService`. Current master has `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`, but no `Assets/Game/Vitality/Runtime`; production cutover is externally blocked and agent-3 will not invent or copy System 02 Runtime ownership.
- Dependency inspection now shows System 02 has implemented `Game.Vitality.Runtime.VitalityRegistry : IVitalityService` on `fixes/agent-9`, including registration, authoritative damage, defeat events, capture, and restore. It is not yet published to `master`, so agent-3 will not merge/cherry-pick another assignment branch; the blocker is specifically publication/integration availability on master, not uncertainty about the expected runtime shape.

## Hypotheses / discriminating results

1. **A production Vitality Runtime is required before duplicate Kentridge HP authority can be removed.** Current-master inspection confirms the Runtime is absent, while the dependency branch confirms the intended concrete implementation exists but has not landed. Therefore T01-002/010/011/012/014/015 and their assembled follow-ons remain blocked on publication of System 02 through normal ownership flow.
2. **The reusable Combat changes are compatible with the current affected module surface.** Exact request for feature `d8d6bd560e2eb7cd0950f3283ee25806e2d2653a` ran as CI run `33714366352`: CharacterAI, Combat, Continuity, GameplayReplication, and Vitality Unity tests passed; the Kentridge player built, produced seven timed captures, and logged `HARNESS done after 80.0s, assertion failures 0`. During shutdown, renderer teardown threw `NullReferenceException` in `GpuSurfaceMirrorCoordinator.DetachPageArena`, followed by Mono exit 139/SIGSEGV. That is a demonstrated product teardown defect outside Combat, not proven transport infrastructure, so an unchanged retry is not justified.

## Selected approach / boundaries

- Preserve master contracts/GUIDs and add only Combat-required Vitality damage/service semantics.
- Do not patch renderer teardown, create a fake Vitality implementation, copy System 02 Runtime, or merge/cherry-pick another assignment branch into agent-3.
- When the real Vitality Runtime is published to master, merge current master, inject it in Kentridge composition, register/bind production CharacterIds, remove production use of the parameterless Combat health path, and keep Encounter/game-result policy in composition.
- Do not refactor the older experimental CombatPrototype chain; its orchestration state is outside this production life-authority migration.

## Remaining gates

- Real Vitality Runtime publication to master and Kentridge production cutover.
- Final bypass/one-authority audit and independent reuse confirmation in assembled production.
- Fresh exact-SHA automatic module plus Kentridge player validation after required upstream prerequisites/fixes are present; inspect durable built-player evidence.
- Complete T01-023/025/030/031/032, populate closure fields, move `open/` directly to `closed/`, merge current master, and non-force push the exact feature head to master only after every required gate is green.
