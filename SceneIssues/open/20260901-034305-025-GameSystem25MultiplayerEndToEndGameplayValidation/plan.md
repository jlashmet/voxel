# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Target ownership:** shared built-player validation infrastructure plus multiplayer scenarios. **No new gameplay API/Runtime module and no test-only networking runtime.**

## Implementation

1. Extend the shared standalone-player runner with generic multiple-process roles, isolated writable directories, logs and exact-SHA verification.
2. Launch the real production authority/host topology plus at least two separate built client processes.
3. Enter through #23/#07 production session flows and wait on semantic readiness rather than arbitrary sleeps.
4. Add a two-client authoritative contention scenario (for example one loot claim) and verify authority plus both clients converge.
5. Add a **squad-beat combat convergence scenario**: each player has a squad, system 01 authoritatively selects one active member for each participating player in the same beat, and each client submits at most one deliberate move for its selected member.
6. Prove accepted player moves resolve as one authoritative beat rather than a serial 20–30-character turn queue, with deterministic ordering for conflicts and downstream events.
7. Exercise at least one **cross-player event-driven combo** in which another player's/squad member's configured interaction joins and redirects or transforms/escalates an in-flight movement/projectile/impact/spell/world event. Status-only proc chaining is insufficient proof.
8. Verify authority and all clients converge on beat identity, active/upcoming member sequence, accepted commands, event-chain ordering, vitality and resulting world/combat outcome.
9. Kill/disconnect one client unexpectedly, mutate authoritative state while absent, reconnect on a new transport connection, and verify same PartyMemberId/PlayerSlot/CharacterId plus current combat/beat state where applicable.
10. Verify explicit Leave Game differs from interruption.
11. Put full-capacity, join-in-progress, repeated reconnect and persisted rehost in slower scheduled/release scenarios.

## Dependencies

06-08 networking/session continuity, 01 Combat squad-beat/event-chain contracts, 14 application graph, representative authoritative domains, shared validation architecture.

## Proof

Separate OS processes, same build SHA, real UTP/session code, semantic diagnostic oracle and role-tagged artifacts prove one-action-per-player simultaneous beats, cross-player combo continuation, deterministic bounded chain convergence, authoritative vitality/outcomes and reconnect recovery.

## Do not build

No WAN simulator, second transport, direct state mutation helpers, client-authoritative combat sequencing/reactions, or four-client visual matrix on every PR.
