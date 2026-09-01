# 08 Player disconnect, reconnect & continuity — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Continuity.Api` / `Game.Continuity.Runtime`
**Execution rule:** Sessions owns durable member/slot identity; Continuity owns interruption/recovery policy around temporary connections.

## API / policy

- [ ] **T08-001 — Inventory current disconnect/reconnect behavior.** Map transport callbacks, member deletion, character despawn/recreate, late-join repair and any connection-id persistence.
- [ ] **T08-002 — Establish asmdefs.** Continuity.Runtime depends on Sessions.Api, GameplayReplication.Api, Characters.Api and network capability APIs only.
- [ ] **T08-003 — Define recovery state machine.** Specify Connected, ConnectionInterrupted, Reconnecting, Resynchronizing, Recovered/Expired states and legal transitions.
- [ ] **T08-004 — Define reconnect request/result.** Key recovery by durable PartyMemberId/session credentials rather than prior socket identity; enumerate semantic failures.
- [ ] **T08-005 — Define continuity policy.** Grace/expiration and join/recovery constraints are configuration, not hardcoded timing in transport code.
- [ ] **T08-006 — Define presence/recovery events.** Expose state required by Sessions presentation and orchestration without leaking transport callbacks.

## Runtime

- [ ] **T08-010 — Observe unexpected transport loss.** Transition continuity state without removing Sessions membership, slot or CharacterId.
- [ ] **T08-011 — Reserve durable identity through grace.** Ensure no second member/character is allocated while the interrupted identity remains recoverable.
- [ ] **T08-012 — Reauthenticate new connection.** Bind a new transport connection to the existing PartyMemberId/slot/CharacterId and retire stale connection association.
- [ ] **T08-013 — Select repair vs full resync.** Use existing network capabilities; choice is an optimization and must not alter durable identity semantics.
- [ ] **T08-014 — Gate input authority on GameplayReady.** Reconnected client cannot command its character until system 06 reports current-state synchronization.
- [ ] **T08-015 — Handle authoritative changes while absent.** Character/vitality/inventory/progression changes remain authoritative and appear on reconnect; never replay old one-shot effects to reconstruct state.
- [ ] **T08-016 — Implement explicit Leave Game separately.** Leave removes membership/character according to Sessions/gameplay policy and must not enter reconnect grace.
- [ ] **T08-017 — Implement grace expiration.** Deterministically hand control/removal policy back to owning systems without fabricating a new identity.

## Verification

- [ ] **T08-020 — Fast reconnect test.** New connection id, same PartyMemberId/PlayerSlot/CharacterId, repair path, current state.
- [ ] **T08-021 — Full-resync reconnect test.** Force repair window miss and prove identity and current truth remain unchanged.
- [ ] **T08-022 — Absent-state mutation test.** Mutate authoritative vitality/inventory/progression while client is absent and verify recovered client sees final current state.
- [ ] **T08-023 — Duplicate-character regression.** Repeated reconnect attempts cannot create another controlled CharacterId.
- [ ] **T08-024 — Explicit leave test.** Leave immediately follows leave semantics and cannot be recovered as an interruption.
- [ ] **T08-025 — Grace-expiration test.** Expired recovery rejects old credentials and follows configured cleanup.
- [ ] **T08-026 — Run automatic Continuity/Sessions/Replication tests.**

## Cleanup / close

- [ ] **T08-030 — Remove transport-owned identity policy.** Search for disconnect handlers deleting gameplay identity or spawning replacements directly.
- [ ] **T08-031 — Boundary audit.** No persistence-across-runs semantics or AI takeover policy inside Continuity; AI takeover, if ever configured, routes through CharacterAI API.
- [ ] **T08-032 — Close with continuity proof.** Demonstrate connection identity changes while durable member/slot/character identity and current gameplay state survive.
