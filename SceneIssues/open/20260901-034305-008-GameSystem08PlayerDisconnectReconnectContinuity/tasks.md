# 08 Player disconnect, reconnect & continuity — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Continuity.Api` / `Game.Continuity.Runtime`
**Execution rule:** Sessions owns durable member/slot identity; Continuity owns interruption/recovery policy around temporary connections.

## API / policy

- [x] **T08-001 — Inventory current disconnect/reconnect behavior.** Sessions drops only ephemeral connection association and preserves member/slot/CharacterId; lower Net removes transient authenticated network-player state. No GameplayReplication.Api exists on current master; blocker recorded in plan.
- [ ] **T08-002 — Establish asmdefs.** Continuity.Api/Runtime asmdefs authored for independent slice, but required GameplayReplication.Api dependency is externally unavailable and cannot be added without changing acceptance.
- [x] **T08-003 — Define recovery state machine.** Connected, ConnectionInterrupted, Reconnecting, Resynchronizing, Recovered, Expired and Left plus guarded transitions.
- [x] **T08-004 — Define reconnect request/result.** Durable session/member credential plus opaque token; no socket/connection identity in API.
- [x] **T08-005 — Define continuity policy.** Configured grace and fast-repair window with validation; no transport hardcoded timing.
- [x] **T08-006 — Define presence/recovery events.** Semantic continuity event stream and recovery query keyed by PartyMemberId.

## Runtime

- [x] **T08-010 — Observe unexpected transport loss.** Coordinator enters interruption without mutating Sessions membership/slot/CharacterId.
- [x] **T08-011 — Reserve durable identity through grace.** Sessions member remains authoritative while Continuity holds recoverable credential/state; no replacement member allocation occurs.
- [x] **T08-012 — Reauthenticate new connection.** Runtime admission seam binds a new opaque connection to the existing member snapshot; Sessions-backed independent fixture rebinds 11 -> 99 with unchanged durable identity.
- [ ] **T08-013 — Select repair vs full resync.** Coordinator deterministically selects fast versus full-resync intent from configured repair window, but wiring to the required existing GameplayReplication/network repair capabilities is blocked by missing system 06 API.
- [ ] **T08-014 — Gate input authority on GameplayReady.** Semantic completion hook exists, but required system-06 GameplayReady source is absent on current master; do not substitute a fake production readiness authority.
- [ ] **T08-015 — Handle authoritative changes while absent.** Blocked by missing system-06 current-state replication API; must prove vitality/inventory/progression current truth after that prerequisite lands.
- [x] **T08-016 — Implement explicit Leave Game separately.** Leave invalidates recovery immediately and hands removal semantics to owning systems; it never enters reconnect grace.
- [x] **T08-017 — Implement grace expiration.** Expiration invalidates old credentials and deterministically hands final cleanup to owning systems without fabricating identity.

## Verification

- [x] **T08-020 — Fast reconnect test.** Exact CI run 33506812126 passed on request `9b9fa56c0c6cd35f15eb7861e6525ec63fdb03d3` whose direct feature parent is `20d2e386e7fe9bd7b277ab339d5cc2b321dabb29`; regression proves connection 99 with unchanged PartyMemberId/PlayerSlot/CharacterId.
- [ ] **T08-021 — Full-resync reconnect test.** Path-selection/identity regression is green in exact CI, but full current-state resync proof remains blocked by missing system 06.
- [ ] **T08-022 — Absent-state mutation test.** Blocked by missing GameplayReplication current-state API.
- [x] **T08-023 — Duplicate-character regression.** Exact CI run 33506812126 passed the authored regression proving repeated reconnect cannot create another roster member/CharacterId.
- [x] **T08-024 — Explicit leave test.** Exact CI run 33506812126 passed the authored explicit-leave regression.
- [x] **T08-025 — Grace-expiration test.** Exact CI run 33506812126 passed the authored expiration/credential-invalidation regression.
- [ ] **T08-026 — Run automatic Continuity/Sessions/Replication tests.** Exact CI run 33506812126 passed automatic required module validation for the currently available Continuity/Sessions slice; required GameplayReplication validation remains blocked until system 06 lands.

## Cleanup / close

- [x] **T08-030 — Remove transport-owned identity policy.** Audit found lower-level disconnect only removes transient network-player state; Sessions preserves gameplay identity. New Continuity API exposes no socket id and runtime policy is keyed by PartyMemberId.
- [x] **T08-031 — Boundary audit.** Continuity contains no persistence-across-runs or AI takeover policy; terminal leave/expiration policy is handed to owning systems.
- [ ] **T08-032 — Close with continuity proof.** Cannot close until exact gates plus system-06-dependent GameplayReady/current-state continuity acceptance are available and validated.
