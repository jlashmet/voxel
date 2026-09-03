# 01 Production combat integration — implementation plan

**Target ownership:** extend existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters where Characters, Encounters, Vitality, and Input meet Combat. Do **not** create a second combat runtime.

## Acceptance / current state

- Combat binds real `CharacterId` and `EncounterId`, keeps only combat team/session/result semantics, and leaves role-to-team, winner-to-`EncounterResolution`, spawning, cleanup, and presentation policy in composition.
- Vitality is the production life-state authority. Combat retains positioning, turns, tactical execution, team/winner semantics, and combat-resolution state. Input remains semantic through `Game.Input.Api`.
- Reusable seams are implemented: `CombatParticipant.FromCharacter`, `CombatStartRequest`/`CombatStartResult`, `CombatResolved`, `EncounterCombatCoordinator`, and `CombatService(IVitalityService)`. Independent Character, Encounter, and `IVitalityService` fixtures cover those boundaries.
- System 02 has now landed on `master` at `81ffa4bbc76c3feb6e0bde2376065b4144f3f10a`, including `Game.Vitality.Runtime.VitalityRegistry`, the Combat/Vitality adapter, Kentridge production composition, module regressions, and built-scene tests.
- Agent-3 merged that master into `fixes/agent-3` as `b53a2abff95475e6030da475706b3a8478d90ef9`, resolving overlapping Combat files by retaining the landed production Vitality implementation while restoring agent-3's EncounterId semantic contracts/coordinator and independent reuse tests.
- `KentridgeForestBanditEncounter` now constructs `VitalityRegistry`, injects it into `CombatService`, registers real Encounter member `CharacterId`s in Vitality, and builds Combat participants via `CombatParticipant.FromCharacter`. The previous parameterless Combat health path is absent from current master/feature search.
- Previous exact-SHA baseline run `33800856291` remains useful renderer evidence only; a fresh exact-SHA gate is required for the merged production cutover.

## Hypotheses / discriminating results

1. **Production Vitality Runtime was the blocking prerequisite for removing Combat-owned life state.** Confirmed by the landed System 02 implementation: current Kentridge uses real `VitalityRegistry`, and current `CombatService` has only the injected `IVitalityService` constructor and routes life reads/damage through Vitality.
2. **The renderer restoration resolves the prior assembled-player teardown blocker.** Confirmed by baseline run `33800856291`; the final migrated run must still prove no regression on the new exact SHA.
3. **Agent-3's Encounter integration remains compatible with the landed System 02 Combat implementation.** The merge keeps master's production Combat runtime and restores only semantic Encounter contracts/coordinator plus their engine-free tests. Exact-SHA CI will discriminate any API/assembly incompatibility before closure.

## Selected approach / boundaries

- Treat the landed System 02 Vitality Runtime and Kentridge cutover as upstream production truth; do not duplicate or replace it.
- Preserve agent-3's EncounterId semantic API and exactly-once `EncounterCombatCoordinator` as reusable Combat/Encounter boundary proof.
- Keep role-to-team mapping, EncounterResolution mapping, authored actor realization, cleanup, input-context ownership, and presentation in Kentridge composition.
- Character lifecycle defeat markers in composition are treated only as downstream Character lifecycle projection; Combat alive/HP decisions read Vitality exclusively. Revisit only if exact-SHA acceptance demonstrates an authority conflict.
- Do not refactor the older experimental CombatPrototype chain; its orchestration state is outside this production life-authority migration.

## Remaining gates

- Fresh exact-SHA automatic module validation for the current merged feature.
- Fresh Kentridge built-player validation with durable evidence and clean process exit.
- Final one-authority/bypass audit after CI evidence, then complete T01-025/031/032.
- Populate closure fields, move `open/` directly to `closed/`, merge current master, and non-force push the exact final feature head to master only after every required gate is green.
