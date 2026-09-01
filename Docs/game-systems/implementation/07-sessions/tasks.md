# 07 Multiplayer party & session formation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Sessions.Api` / `Game.Sessions.Runtime`
**Execution rule:** durable party/member/slot identity is gameplay/session identity; transport connection identity is temporary plumbing.

## API / identity model

- [ ] **T07-001 — Inventory existing lobby/session identities.** Find player indexes, connection ids, host flags, ready state, spawn slots and any code using socket ids as gameplay identity.
- [ ] **T07-002 — Establish asmdefs and dependency direction.** Sessions.Runtime may consume transport/network API and Characters.Api; Sessions.Api remains transport-technology-neutral.
- [ ] **T07-003 — Define `GameSessionId`, `PartyMemberId`, and `PlayerSlot`.** Specify stable equality/serialization and uniqueness semantics; document which survive reconnect and which survive durable restore.
- [ ] **T07-004 — Define roster/member snapshot.** Include durable identity, slot, leadership role, presence/readiness and controlled CharacterId when assigned; exclude transport connection ids.
- [ ] **T07-005 — Define join request/result and failure reasons.** Include configured capacity/version/content/session compatibility needed by current production flow.
- [ ] **T07-006 — Define join-provider seam.** Provider returns semantic join/connect information without embedding a specific matchmaking platform into Sessions.
- [ ] **T07-007 — Define readiness/session lifecycle events.** Distinguish joined, connected, synchronized and GameplayReady states.

## Runtime

- [ ] **T07-010 — Implement stable party roster.** Deterministic member creation/removal/lookup and no identity reuse within a running session unless policy explicitly permits it.
- [ ] **T07-011 — Implement deterministic slot allocation.** Respect configured player capacity; remove hardcoded four-player assumptions from generic logic.
- [ ] **T07-012 — Bind connection to durable member.** Authenticate/associate a transport connection without making connection id part of durable member identity.
- [ ] **T07-013 — Bind PlayerSlot to CharacterId.** Coordinate Character creation/binding through Characters.Api; preserve the mapping for continuity.
- [ ] **T07-014 — Implement leadership semantics.** Party leader role is explicit and must not imply server gameplay authority.
- [ ] **T07-015 — Implement readiness barrier.** Coordinate with system 06 and later system 14; launch/GameplayReady requires semantic readiness, not merely sockets.
- [ ] **T07-016 — Implement configured join-in-progress admission.** Preserve existing roster/slots and reject incompatible/full joins deterministically.
- [ ] **T07-017 — Expose snapshot/events for presentation and persistence adapters.** No direct dependency on systems 20/16 Runtime.

## Verification

- [ ] **T07-020 — Formation tests for 2–4/configured capacity.** Unique member ids/slots, deterministic allocation and no framework hardcode to exactly four.
- [ ] **T07-021 — Leadership tests.** Leader transfer/removal behavior follows configured policy and never changes gameplay authority.
- [ ] **T07-022 — Readiness tests.** Connected-but-unsynchronized member remains not GameplayReady.
- [ ] **T07-023 — Join-in-progress tests.** Successful compatible join plus full/incompatible rejection.
- [ ] **T07-024 — Headless join-provider fixture.** Prove Sessions works with a deterministic local provider independent of frontend/matchmaking UI.
- [ ] **T07-025 — Character binding test.** Member -> slot -> CharacterId mapping is stable and unique.
- [ ] **T07-026 — Run automatic module/network dependent tests.**

## Cleanup / close

- [ ] **T07-030 — Remove socket-id gameplay identity.** Repository search for connection/client ids used as party/member/character keys and migrate them.
- [ ] **T07-031 — Boundary audit.** Sessions.Api exposes no UTP-specific packet/socket type and Sessions owns no reconnect policy.
- [ ] **T07-032 — Close with identity proof.** Show one stable `PartyMemberId -> PlayerSlot -> CharacterId` chain available for system 08 continuity.
