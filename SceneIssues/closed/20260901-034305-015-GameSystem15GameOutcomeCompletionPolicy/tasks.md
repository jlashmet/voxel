# 15 Game outcome & completion policy — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Outcomes.Api` / `Game.Outcomes.Runtime`
**Execution rule:** Outcomes owns exactly one authoritative terminal gameplay result. Combat defeat, encounter completion and technical shutdown remain separate facts.

## API / policy boundary

- [x] **T15-001 — Inventory current win/loss/game-over logic.** `CombatService` owns battle-local `WinningTeam`/completion only; Campaign/Story own progression effects; existing Outcomes is query-only and replication projects it. No demonstrated global terminal/shutdown owner exists to migrate.
- [x] **T15-002 — Establish asmdefs.** `Game.Outcomes.Runtime` is engine-neutral, references only `Game.Outcomes.Api`, and receives semantic requests/facts through API contracts.
- [x] **T15-003 — Define outcome lifecycle.** `Running` may accept authorized terminal requests; the first accepted request commits immutable `Resolved`; all later requests are idempotent only when identical or otherwise rejected.
- [x] **T15-004 — Define disposition and semantic `OutcomeRef`.** Shared contracts use semantic string refs and do not encode scene/boss identities.
- [x] **T15-005 — Define resolution request/result.** `GameOutcomeResolutionRequest` carries stable resolution identity, authority, disposition, and semantic outcome; results distinguish accepted/idempotent/unauthorized/already-resolved/no-request.
- [x] **T15-006 — Define current outcome snapshot.** `GameOutcomeSnapshot` coherently exposes lifecycle, disposition, outcome, resolution id, authority, and revision while preserving the prior constructor for existing query consumers.
- [x] **T15-007 — Define exactly-once `GameOutcomeResolved` event.** API event carries stable resolution identity, authority, semantic outcome, disposition, and revision for downstream reaction/dedupe.
- [x] **T15-008 — Own module validation surface.** Outcomes API/Runtime are pure headless/domain assemblies (`Game.Outcomes.Runtime` is `noEngineReferences=true`); the plan records the repository-authorized no-scene exception and module-owned tests live under `Assets/Game/Outcomes/Tests/EditMode`.

## Runtime

- [x] **T15-010 — Implement immutable terminal transition.** `GameOutcomeRuntime` commits the first authorized Running -> Resolved transition before publishing one notification.
- [x] **T15-011 — Handle duplicate/late requests deterministically.** An identical winning request is idempotent; changed/competing requests cannot replace the committed outcome.
- [x] **T15-012 — Define authoritative request source/configuration seam.** Runtime requires configured `OutcomeAuthorityRef` values; `OutcomePolicyRouter` maps authored semantic conditions to explicit requests and ignores unmapped facts.
- [x] **T15-013 — Preserve ordinary defeat semantics.** Combat/party-defeat facts are inert unless an authored `OutcomePolicyRule` explicitly maps the semantic condition to a terminal request.
- [x] **T15-014 — Add Orchestration notification seam.** `IGameOutcomeEvents.OutcomeResolved` exposes committed resolution for System 14/downstream composition; Outcomes itself performs no graph teardown.
- [x] **T15-015 — Add persistence/replication projection seams.** `IGameOutcomeQuery` continues to expose the coherent snapshot consumed by existing replication, and the runtime constructor restores that snapshot without replaying events.

## Verification

- [x] **T15-020 — Nonterminal combat-loss regression.** `UnmappedCombatLossLeavesOutcomeRunning` proves defeat/combat loss alone leaves outcome `Running` at revision 0.
- [x] **T15-021 — Authored success test.** `ConfiguredCampaignSuccessResolvesExactlyOnce` proves a configured campaign terminal condition resolves semantic success once and a duplicate is idempotent.
- [x] **T15-022 — Authored failure-policy test.** `PartyDefeatResolvesOnlyWhenAuthoredPolicyMapsIt` proves the same defeat fact remains nonterminal without policy and resolves failure only when authored.
- [x] **T15-023 — Competing/duplicate request tests.** `DuplicateAndCompetingRequestsKeepFirstWinnerAndEmitOneEvent` plus `FirstAuthoredRuleWinsWhenOneConditionHasCompetingMappings` prove deterministic first-winner ordering, immutable outcome, and exactly one resolution event.
- [x] **T15-024 — Technical shutdown regression.** `TechnicalShutdownWithoutGameplayPolicyCreatesNoOutcome` proves server/application shutdown facts do not create a gameplay outcome.
- [x] **T15-025 — Snapshot/restore test.** `RestoredResolvedSnapshotDoesNotReplayHistoricalResolution` proves restored terminal state remains immutable and emits no historical resolution event.
- [x] **T15-026 — Run automatic Outcomes/dependent tests.** Exact-SHA request `6f91e7e36484e8bcd4e2f5fcccb6030f4cf0bddb` validated feature SHA `b9d11d54aff204d71b0bea94dc2dd583883a342b` in workflow run `33839483224`: `Game.Continuity.Tests` 7/7 passed, `Game.GameplayReplication.Tests` 14/14 passed, `Game.Outcomes.Tests` 11/11 passed, no failures/skips; canonical `KentridgePlayableSlice` player validation completed with 0 assertion failures. Total repository-selected validation time was 204.71 seconds, below the five-minute budget.
- [x] **T15-027 — Preserve relocated Outcomes test asset identities.** Initial exact-SHA run `33834559402` exposed invalid 40-character Git blob SHAs in the relocated test asmdef/API-contract `.meta` files; restored their original valid Unity GUIDs and proved the correction with green replacement run `33839483224`.

## Cleanup / close

- [x] **T15-030 — Remove implicit game-over ownership.** Audited current Combat/Campaign/Story ownership plus repository searches for direct quit/scene-load/game-over paths; no demonstrated global terminal owner exists to migrate, so no unrelated production path was changed.
- [x] **T15-031 — Boundary audit.** Outcomes production diff contains no final-boss flags, score UI, save deletion, scene transitions, engine shutdown, or network teardown; Runtime references only Outcomes API.
- [x] **T15-032 — Close with exactly-one proof.** The regressions prove ordinary combat/party loss and technical shutdown remain `Running`; only an authored request from configured authority can commit revision 1, emit one resolution event, and become the immutable terminal result while identical retries are idempotent and competitors are rejected.
