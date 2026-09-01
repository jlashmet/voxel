# 06 Gameplay-state replication — implementation plan

**Target module:** `Assets/Game/GameplayReplication/Api` / `Runtime` (`Game.GameplayReplication.Api`, `Game.GameplayReplication.Runtime`). The existing `Assets/VoxelEngine/Net` UTP/server-authoritative transport remains underneath.

## Inventory / ownership

- `Packages/manifest.json` includes `com.unity.transport` 6.5.0 and intentionally does not include NGO.
- The production server/client/transport/protocol/interest/convergence stack remains under `Assets/VoxelEngine/Net`.
- `AuthoritativeServerSession.ProcessAuthoritativeTick` is the authoritative fixed-tick cadence. Gameplay-state publication plugs into that cadence after authoritative simulation state is resolved and before the existing replication/send flush; no second update loop exists.
- Existing Net owns transport, connection identity, packet framing, subscriptions/interest, convergence/repair, reconnect/admission plumbing and client/server receive paths. `Game.GameplayReplication` owns semantic gameplay publications/revisions and replicated client truth only.
- Existing owning semantic APIs consumed directly: Characters, Encounters, Combat, Inventory, and Sessions.
- The binding system designs require replication-facing read seams for Vitality, Progression, Continuity, and Outcomes. System 06 supplies only the minimum engine-neutral owning API contracts required by this consumer; it does not implement those runtimes.

## Minimal missing API contracts

- `Game.Vitality.Api`: immutable `VitalitySnapshot` keyed by `CharacterId` with current/max/defeated/revision and `IVitalityQuery`. No damage/heal/defeat runtime.
- `Game.Progression.Api`: stable quest/objective identities, lifecycle state, revisions, coherent `ProgressionSnapshot`, and `IProgressionQuery`. No observation evaluation, completion mutation, Story integration, or runtime.
- `Game.Continuity.Api`: semantic recovery state snapshots keyed by durable Sessions-owned `PartyMemberId`, coherent snapshot/query. No grace policy, reconnect authentication, input gating, or runtime.
- `Game.Outcomes.Api`: Running/Resolved lifecycle, disposition, semantic `OutcomeRef`, current snapshot/query. No resolution request policy, event emission, orchestration, or runtime.

Each contract-only API has a tiny module-local consumer test assembly so repository-derived validation owns and exercises the contract directly rather than falling back to unrelated modules. The contracts contain no Unity, transport, presentation, scene, or named-content policy.

## Replication API / runtime

One publication barrier advances one monotonic `GameplayRevision`; every projection in that publication shares the revision. Deltas must be exact-next; duplicate/older publications are ignored, gaps/schema incompatibility enter `RepairRequired`, and a newer full snapshot may jump directly to current truth for repair, late join and reconnect convergence.

Subsystem identity/versioning is semantic (`GameplayProjectionId` + schema version). Producers implement `IGameplayProjectionSource` through adapters; owning gameplay modules never depend on replication Runtime. `GameplayReady` is configuration-driven and true only while synchronized with all configured required compatible projections.

`Game.GameplayReplication.Api` and `Runtime` stay engine-neutral. `Adapters` consumes owning gameplay APIs. `Transport` is the sole gameplay transport bridge on top of the existing Net protocol/send/receive seams. Repair requests travel through the existing client EVENT path; new/reconnected authenticated connections cause coherent current-state snapshots. Sessions durable identity is independent of transient connection IDs.

## Validation and demonstrated fixes

The transport-backed fixture covers two authenticated UTP clients with Characters + Vitality + transactional Inventory, a forced semantic revision gap with live repair request/response, a late joiner, and disconnect/reconnect under a new transient connection ID. `GameplayReplicationProjectionContractTests` independently consumes the four minimal APIs with API-only fixtures and verifies deterministic Continuity, Outcomes, Progression, and Vitality projections without any owning runtime implementation.

Run `33513817861` exposed an obsolete parallel `Game.GameplayReplication.Networking` assembly as a compile-time duplicate; it was removed, leaving `Game.GameplayReplication.Transport` as the sole bridge. Run `33521707518` then proved the strengthened focused UTP test and standalone player but broadened module validation because the new API-only folders had no convention-owned tests; the unrelated `Game.Materials.Tests` failures demonstrated that ownership defect. Adding tiny module-local API contract tests fixed the validation boundary without modifying Materials or the planner.

Exact source `5432ef305138c2948d182342df52af626da154f0` passed final acceptance validation in run `33522951566`, job `99906521904`: the focused UTP test, repository-derived automatic module/dependent validation, standalone `KentridgePlayableSlice` SceneIssue replay, screenshot/artifact evidence, and final `ci/single-test=success` all passed.

Current master at closure check was `fdf9fffab5df3b1f16cd7123a7a5410111d46b58`, one commit beyond the feature base and containing only a separate SceneIssue's three bookkeeping files. Per repository workflow, merge current master into the closed feature branch before promotion; no production/test compatibility conflict is expected.

## Non-goals preserved

No second transport, NGO adoption, UI state replication, event-history reconstruction, or subsystem-specific runtime authority was added. Vitality damage, Progression evaluation, Continuity policy, and Outcome resolution remain outside system 06. Reconnect identity remains Sessions-owned and connection identity remains Net-owned.
