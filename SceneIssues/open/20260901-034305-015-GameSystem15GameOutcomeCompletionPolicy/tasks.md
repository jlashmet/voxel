# 15 Game outcome & completion policy — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Outcomes.Api` / `Game.Outcomes.Runtime`
**Execution rule:** Outcomes owns exactly one authoritative terminal gameplay result. Combat defeat, encounter completion and technical shutdown remain separate facts.

## API / policy boundary

- [x] **T15-001 — Inventory current win/loss/game-over logic.** `CombatService` owns battle-local `WinningTeam`/completion only; Campaign/Story own progression effects; existing Outcomes is query-only and replication projects it. No demonstrated global terminal/shutdown owner exists to migrate.
- [ ] **T15-002 — Establish asmdefs.** Outcomes.Runtime may receive semantic requests/facts via composition; API is engine/transport/presentation-neutral.
- [ ] **T15-003 — Define outcome lifecycle.** `Running` and immutable `Resolved`; specify behavior of all requests after resolution.
- [ ] **T15-004 — Define disposition and semantic `OutcomeRef`.** Keep semantic reason/configuration extensible without encoding scene/boss identities into shared Runtime.
- [ ] **T15-005 — Define resolution request/result.** Include deterministic acceptance/rejection/idempotency semantics and authority restrictions.
- [ ] **T15-006 — Define current outcome snapshot.** One coherent state for replication/persistence/presentation.
- [ ] **T15-007 — Define exactly-once `GameOutcomeResolved` event.** Stable event identity/revision sufficient for downstream reaction/dedupe.
- [ ] **T15-008 — Own module validation surface.** Outcomes API/Runtime are pure `noEngineReferences` headless/domain assemblies; document the validation-scene exception and provide module-owned EditMode coverage instead of a Unity validation scene.

## Runtime

- [ ] **T15-010 — Implement immutable terminal transition.** First accepted authoritative terminal request changes Running -> Resolved exactly once.
- [ ] **T15-011 — Handle duplicate/late requests deterministically.** Same request is idempotent; competing later request cannot replace the committed outcome.
- [ ] **T15-012 — Define authoritative request source/configuration seam.** Story/campaign/other approved policy can request resolution through composition; arbitrary domain events do not automatically terminate the game.
- [ ] **T15-013 — Preserve ordinary defeat semantics.** Character defeat/combat resolution/encounter failure remain nonterminal unless authored policy explicitly maps them to an outcome request.
- [ ] **T15-014 — Add Orchestration notification seam.** System 14 observes resolution and coordinates aftermath; Outcomes does not tear down the graph.
- [ ] **T15-015 — Add persistence/replication projection seams.** Current resolved/running state is consumable through APIs without Runtime coupling.

## Verification

- [ ] **T15-020 — Nonterminal combat-loss regression.** Defeat/combat loss alone leaves outcome Running.
- [ ] **T15-021 — Authored success test.** Configured campaign terminal condition requests a semantic success and resolves exactly once.
- [ ] **T15-022 — Authored failure-policy test.** Only when configured, the selected semantic failure condition resolves accordingly.
- [ ] **T15-023 — Competing/duplicate request tests.** Deterministic authoritative processing order, one immutable winner, one resolution event.
- [ ] **T15-024 — Technical shutdown regression.** Server/application shutdown without gameplay terminal policy creates no GameOutcome.
- [ ] **T15-025 — Snapshot/restore test.** Resolved state remains resolved and cannot re-emit historical resolution as a new outcome.
- [ ] **T15-026 — Run automatic Outcomes/Orchestration/Story dependent tests.**

## Cleanup / close

- [ ] **T15-030 — Remove implicit game-over ownership.** Search combat/scenes/campaign scripts for direct victory/loss/shutdown decisions and route demonstrated terminal policy through Outcomes.
- [ ] **T15-031 — Boundary audit.** No final-boss flags, score screens, save deletion, scene transitions or network shutdown in Outcomes.
- [ ] **T15-032 — Close with exactly-one proof.** A run can remain Running through ordinary losses and commits one immutable terminal result only via authored authority.
