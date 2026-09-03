# 01 Production combat integration — tasks

**Plan:** [plan.md](plan.md)
**Ownership:** existing `Game.Combat.Api` / `Game.Combat.Runtime` plus thin composition adapters.
**Execution:** work top-to-bottom unless a dependency is explicitly blocked. Do not create a second combat runtime.

## Baseline and contracts

- [x] **T01-001 — Inventory current combat ownership.** `CombatService` historically owned authoritative `_hitPoints` (6 HP, 2 damage); `CombatParticipant` owns team; `KentridgeForestBanditEncounter` locally constructs Input/Combat services, creates participant identities, spawns bandits, starts combat, and settles encounter state. Evidence recorded in `plan.md`.
- [ ] **T01-002 — Lock the module boundary.** **BLOCKED on final production composition/Vitality Runtime migration.** `Game.Combat.Api` consumes engine-free Characters/Encounters APIs and `Game.Combat.Runtime` remains engine-free; Runtime consumes only engine-free `Game.Vitality.Api`. Current Kentridge production composition still owns direct runtime construction.
- [x] **T01-003 — Define character-backed participant binding.** `Game.Combat.Api` references engine-free `Game.Characters.Api`; `CombatParticipant.FromCharacter(CharacterId, CombatTeam)` preserves the production `CharacterId`, derives the combat participant identity from its serialized value, and carries only Combat team semantics. `CombatCharacterBindingTests` proves the binding with a non-Kentridge fixture.
- [x] **T01-004 — Define combat-start request/result.** Real `Game.Encounters.Api.EncounterId` keys `CombatStartRequest`/`CombatStartResult`; the request carries only already-mapped Combat participants, so Encounter role-to-team policy stays in composition rather than shared Combat.
- [x] **T01-005 — Define combat-resolution fact.** `CombatResolved` carries only `EncounterId`, `CombatSessionId`, and `CombatTeam`; it deliberately does not embed `EncounterResolution`, cleanup, campaign, or victory policy. `CombatEncounterContractTests` validates the contract against the real Encounters consumer types.

## Runtime migration

- [ ] **T01-010 — Replace prototype health authority.** **PARTIAL, final production seam pending.** `CombatService(IVitalityService)` reads character-backed current/alive state directly from Vitality and routes accepted damage through the API. The parameterless legacy HP fallback remains only for existing non-migrated production/tests until Kentridge can inject a real Vitality Runtime.
- [ ] **T01-011 — Route damage through Vitality.** **PARTIAL, final production seam pending.** In the Vitality-backed constructor path, accepted attacks call `IVitalityService.ApplyDamage(DamageRequest)` and do not mutate a Combat character-health store. Legacy parameterless combat remains for the not-yet-migrated Kentridge bootstrap.
- [ ] **T01-012 — Preserve combat-only state.** Vitality-backed migration changes only life-state reads/damage. Round/readiness (`ChainRoundReadinessCoordinator`), committed tactical intents/enemy phase (`ChainEnemyTacticalAI`), reaction ownership (`ChainReactionReservationCoordinator`), planning/history (`ChainExecutionPlan`), and board/motion state remain Combat concerns. Final checkbox waits for production migration/blast-radius validation.
- [x] **T01-013 — Integrate Encounter ownership.** `EncounterCombatCoordinator` is a thin engine-free adapter over the existing `CombatService`: it starts a session from `CombatStartRequest`, remembers the owning `EncounterId`, and emits exactly one `CombatResolved` fact after authoritative Combat completion. `EncounterCombatIntegrationTests` uses the real `EncounterRegistry` and keeps winner-to-EncounterResolution policy in composition.
- [ ] **T01-014 — Migrate semantic input.** Current `CombatInputController` already reads `IPlayerInputReader`/`PlayerInputSnapshot` rather than raw keys. Production composition still constructs input/runtime locally in Kentridge, so final migration waits for the production composition seam.
- [ ] **T01-015 — Remove scene-local combat construction.** **BLOCKED on publication/injection of a real Vitality Runtime into production composition.** Current master has convention-owned `Game.Vitality.Api` and `Game.Vitality.Tests`, but no `Assets/Game/Vitality/Runtime`; agent-3 will not invent, copy, merge, or cherry-pick System 02 Runtime ownership from another assignment branch.

## Verification

- [x] **T01-020 — Add participant/vitality regression tests.** `CombatVitalityIntegrationTests` uses an independent `IVitalityService` fixture to prove Combat preserves pre-existing Vitality, routes attack damage through the API, observes external Vitality defeat rather than shadow state, and still settles a lethal combat winner.
- [x] **T01-021 — Add resolution/idempotency tests.** `EncounterCombatIntegrationTests` proves `EncounterCombatCoordinator.TryTakeResolved` emits one terminal fact per session under repeated polling and the real `EncounterRegistry.ApplyCombatResolved` accepts an identical repeat without another revision mutation.
- [x] **T01-022 — Add encounter mapping tests.** `EncounterCombatIntegrationTests` proves real `EncounterCombatRequest` membership maps through production CharacterIds into Combat participants and preserves the same `EncounterId` through start and terminal result.
- [ ] **T01-023 — Add independent reuse fixture.** Reuse proof includes non-Kentridge Character binding, Encounter registry integration, and independent `IVitalityService` Combat tests. Final checkbox remains open until a real Vitality Runtime is present in assembled production composition.
- [x] **T01-024 — Run module-owned EditMode/PlayMode tests.** Exact-SHA request for feature `0669bd2ed9981fdba6bff9c8c0abb9ba3290a8e0` completed successfully as run `33800856291`: automatic validation passed `Game.CharacterAI.Tests`, `Game.Combat.Tests`, `Game.Continuity.Tests`, `Game.GameplayReplication.Tests`, and `Game.Vitality.Tests` after the renderer restoration merge.
- [ ] **T01-025 — Run assembled integration proof.** Exact-SHA run `33800856291` built Kentridge and completed its 80-second player scenario with seven real-player captures and a clean process exit; the prior renderer teardown crash no longer reproduces. Final acceptance remains open until Kentridge uses the real Vitality-backed production Combat path and that migrated exact SHA is validated.

## Cleanup and close

- [ ] **T01-030 — Search for bypasses.** Baseline audit complete: `KentridgeForestBanditEncounter` remains the production bypass constructing parameterless Combat/Input runtime. Final checkbox remains open until that path injects Vitality and legacy character HP authority is no longer used in production.
- [ ] **T01-031 — Check blast radius.** **CURRENT BASELINE GREEN; FINAL POST-CUTOVER RERUN PENDING.** Exact feature `0669bd2ed9981fdba6bff9c8c0abb9ba3290a8e0` passed run `33800856291`: all affected automatic module assemblies passed and the Kentridge built-player scenario completed cleanly, resolving the previous `GpuSurfaceMirrorCoordinator.DetachPageArena` teardown blocker for this baseline. Keep this task open until the required production Vitality cutover changes are validated on their final exact SHA.
- [ ] **T01-032 — Close only with one authority.** Confirm Vitality owns production life state, Encounters owns encounter lifecycle, Combat owns combat resolution, and no duplicate production path remains.

## Active dependency / CI evidence

- Current master remains `f5593cc1236ba3963fc5713a11df35292628e97d`, already merged into agent-3, including the GPU renderer restoration.
- Current master still contains only `Assets/Game/Vitality/Api` and `Assets/Game/Vitality/Tests`; there is no `Assets/Game/Vitality/Runtime`, so production Kentridge injection remains externally blocked.
- System 02 dependency branch `fixes/agent-9` contains `Assets/Game/Vitality/Runtime/VitalityRegistry.cs`; inspection confirms `VitalityRegistry : IVitalityService` provides registration, authoritative damage, defeat events, capture, and restore. This narrows the dependency to normal publication onto master; agent-3 will not consume the unmerged assignment branch directly.
- During earlier master synchronization, agent-3 preserved master Vitality Unity GUIDs and `IVitalityQuery`/revision fields and added only damage/service semantics required by Combat; master’s `ICombatService` semantic surface is retained.
- Exact request commit `74eae6b52d81462edbe250c13af48801298dacac`, parented directly to feature `0669bd2ed9981fdba6bff9c8c0abb9ba3290a8e0`, ran as `33800856291` and completed successfully. Automatic module validation executed the five affected EditMode assemblies; Kentridge built, ran for 80 seconds, produced seven real-player captures, and exited cleanly. The prior renderer teardown blocker is therefore resolved for the current baseline.
- A fresh exact-SHA gate is still required after the real Vitality Runtime lands and production composition is migrated; the current green run does not satisfy acceptance for code that does not yet exist.
- `ci-test/fixes/agent-3` remains the only authorized targeted-CI transport. Never replace queued/running work.
