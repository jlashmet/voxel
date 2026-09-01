# 02 Actor vitality, damage & defeat — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Vitality.Api` / `Game.Vitality.Runtime`
**Execution rule:** coordinate the `CharacterId` contract with system 03 before committing public API. Combat consumes Vitality; Vitality never depends on Combat.

## Foundation

- [x] **T02-001 — Baseline existing health state.** Find every authoritative/prototype health, alive/dead, damage, defeat and reset store; classify each as migrate, presentation-only, or obsolete.
  - Evidence: `plan.md` ownership table records `CombatCore.CombatState`, production `Game.Combat.Runtime.CombatService`, `ChainUnitState`/`ChainCombatBoard`, and the `Assets/CombatPrototype` presentation surface, with explicit migration/adapter ownership boundaries.
- [ ] **T02-002 — Create/update module assemblies.** Establish `Assets/Game/Vitality/Api` and `Runtime` asmdefs with Runtime -> own API and `Characters.Api`; assert API has no Unity/Runtime dependency.
  - **BLOCKED:** `Game.Characters.Api.CharacterId` and the ticket-specified `Game.SharedKernel.Api.SessionId` are both absent at feature/master SHA `71e5b6b146cb7dd3b7da0305d0ab42bcc9cea22e`; do not create placeholder shared identity types.
- [ ] **T02-003 — Define vitality state contract.** Add immutable snapshot/state keyed by `CharacterId`, with only semantic current/max/defeated data demonstrated by current gameplay.
  - **BLOCKED:** public signature depends on the System 03 `CharacterId` contract and ticket-specified shared session identity.
- [ ] **T02-004 — Define damage contract.** Add authoritative damage request/result including stable request identity only where existing command delivery can duplicate requests; represent rejection reasons semantically.
  - **BLOCKED:** public signature depends on the System 03 `CharacterId` contract and ticket-specified shared session identity.
- [ ] **T02-005 — Define defeat transition/event.** Guarantee one transition event when crossing the terminal threshold; do not conflate defeat with removal, combat resolution, or game outcome.
  - **BLOCKED:** public signature depends on the System 03 `CharacterId` contract and ticket-specified shared session identity.
- [x] **T02-006 — Add heal/restore contract only if required.** Inspect current content/tests first; do not invent revive/respawn semantics.
  - Evidence: `CombatCore.CombatState`, production `CombatService`, and `ChainCombatBoard` demonstrate initialization/reset plus damage/defeat only; no in-session heal/revive operation is present. No public healing/revive command will be invented. Snapshot restore remains the separate persistence seam in T02-013.

## Runtime

- [ ] **T02-010 — Implement vitality registry.** Key all authoritative vitality by `CharacterId`; reject unknown/removed characters deterministically.
- [ ] **T02-011 — Implement deterministic damage application.** Validate input, clamp state, preserve ordering, and return the resulting authoritative state.
- [ ] **T02-012 — Implement exactly-once defeat transition.** Additional damage to an already defeated character must not re-emit defeat.
- [ ] **T02-013 — Add capture/restore seam.** Expose API-level snapshot contribution needed by system 16 without referencing Persistence Runtime.
- [ ] **T02-014 — Add replication projection seam.** Expose current semantic vitality state/events needed by system 06 without referencing GameplayReplication Runtime.
- [ ] **T02-015 — Migrate combat health.** Adapt existing Combat participant health/alive access to Vitality API; remove duplicate authoritative combat health after behavior parity.
- [ ] **T02-016 — Migrate non-combat damage consumers.** Route any demonstrated environmental/world damage through the same API to prove vitality is actor-owned rather than combat-owned.
  - Evidence baseline: repository searches found no demonstrated production environmental/world damage consumer outside Combat/prototype life-state code. Do not invent one; T02-022 will provide the required independent non-combat reuse proof once the Vitality API exists.

## Verification

- [ ] **T02-020 — Add damage boundary tests.** Cover zero/invalid damage, lethal/nonlethal ordering, clamping, unknown CharacterId, and deterministic results.
- [ ] **T02-021 — Add defeat-event regression.** Prove exactly one defeat event despite repeated/late damage requests.
- [ ] **T02-022 — Add non-combat reuse fixture.** Damage a character without starting Combat and prove identical vitality semantics.
- [ ] **T02-023 — Add snapshot/restore tests.** Alive and defeated state must round-trip exactly with stable CharacterId.
- [ ] **T02-024 — Verify defeat does not resolve game.** Assert no direct dependency/call to Outcomes or session teardown.
  - Baseline evidence: repository searches found no direct `SceneManager`, `LoadScene`, or `GameOver` coupling in the existing life-state paths. Re-verify against final Vitality implementation before checking this task.
- [ ] **T02-025 — Run automatic module tests and dependent Combat tests.** Do not manually enumerate CI tests.

## Cleanup / close

- [ ] **T02-030 — Repository-wide duplicate-state search.** Remove or demote every remaining authoritative `health`/`isAlive` store outside Vitality where it represents the same character life truth.
- [ ] **T02-031 — Boundary audit.** Confirm no external assembly references `Game.Vitality.Runtime` and no Unity object appears in Vitality API.
- [ ] **T02-032 — Close with ownership proof.** Document that character life state has exactly one owner and combat/game-outcome semantics remain separate.
