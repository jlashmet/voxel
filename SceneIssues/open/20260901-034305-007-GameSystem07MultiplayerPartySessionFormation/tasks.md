# 07 Multiplayer party & session formation — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Sessions.Api` / `Game.Sessions.Runtime`
**Execution rule:** durable party/member/slot identity is gameplay/session identity; transport connection identity is temporary plumbing.

## API / identity model
- [x] **T07-001 — Inventory existing lobby/session identities.** Legacy `ServerPlayerRegistry` uses connectionId/playerId and removes identity on disconnect; no Sessions module existed.
- [x] **T07-002 — Establish asmdefs and dependency direction.** Sessions.Api is transport-neutral; Runtime references Sessions.Api, Characters.Api and Net.Api.
- [x] **T07-003 — Define `GameSessionId`, `PartyMemberId`, and `PlayerSlot`.** Stable equality/serialization; member/slot survive reconnect.
- [x] **T07-004 — Define roster/member snapshot.** Durable identity/slot/leadership/presence/readiness/CharacterId; no connection id.
- [x] **T07-005 — Define join request/result and failure reasons.** Capacity/version/content/session/JIP compatibility.
- [x] **T07-006 — Define join-provider seam.** Semantic endpoint/token result; no matchmaking platform type.
- [x] **T07-007 — Define readiness/session lifecycle events.** Joined, connected, synchronized and GameplayReady are distinct.

## Runtime
- [x] **T07-010 — Implement stable party roster.** Monotonic member identity and deterministic lookup/removal.
- [x] **T07-011 — Implement deterministic slot allocation.** Lowest free configured slot; no four-player hardcode.
- [x] **T07-012 — Bind connection to durable member.** Runtime-only opaque handle; rebind preserves identity.
- [x] **T07-013 — Bind PlayerSlot to CharacterId.** Stable unique mapping through Characters.Api semantic binding.
- [x] **T07-014 — Implement leadership semantics.** Explicit role; oldest-member transfer policy only.
- [x] **T07-015 — Implement readiness barrier.** Connected alone cannot launch; synchronization precedes GameplayReady.
- [x] **T07-016 — Implement configured join-in-progress admission.** Existing identities preserved; full/incompatible joins rejected deterministically.
- [x] **T07-017 — Expose snapshot/events for presentation and persistence adapters.** Read-only snapshot + semantic lifecycle event stream.

## Verification
- [ ] **T07-020 — Formation tests for 2–4/configured capacity.** Authored; exact-SHA run pending.
- [ ] **T07-021 — Leadership tests.** Authored; exact-SHA run pending.
- [ ] **T07-022 — Readiness tests.** Authored; exact-SHA run pending.
- [ ] **T07-023 — Join-in-progress tests.** Authored; exact-SHA run pending.
- [ ] **T07-024 — Headless join-provider fixture.** Authored independent provider fixture; exact-SHA run pending.
- [ ] **T07-025 — Character binding test.** Authored; exact-SHA run pending.
- [ ] **T07-026 — Run automatic module/network dependent tests.**

## Cleanup / close
- [ ] **T07-030 — Remove socket-id gameplay identity.** Migrate legacy server authentication/player registry integration to durable Sessions identity without taking reconnect policy.
- [ ] **T07-031 — Boundary audit.** Sessions.Api exposes no UTP/socket type and Sessions owns no reconnect policy.
- [ ] **T07-032 — Close with identity proof.** Show stable `PartyMemberId -> PlayerSlot -> CharacterId` chain for system 08 continuity.
