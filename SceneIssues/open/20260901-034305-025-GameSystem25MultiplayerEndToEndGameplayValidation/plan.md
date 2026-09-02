# 25 Multiplayer end-to-end gameplay validation — implementation plan

**Target ownership:** shared built-player validation infrastructure plus multiplayer scenarios. **No new gameplay API/Runtime module and no test-only networking runtime.**

## Implementation

1. Extend the shared standalone-player runner with generic multiple-process roles, isolated writable directories, logs and exact-SHA verification.
2. Launch the real production authority/host topology plus at least two separate built client processes.
3. Enter through #23/#07 production session flows and wait on semantic readiness rather than arbitrary sleeps.
4. Add a two-client authoritative contention scenario (for example one loot claim) and verify authority plus both clients converge.
5. Add combat/vitality cross-client convergence.
6. Kill/disconnect one client unexpectedly, mutate authoritative state while absent, reconnect on a new transport connection, and verify same PartyMemberId/PlayerSlot/CharacterId plus current state.
7. Verify explicit Leave Game differs from interruption.
8. Put full-capacity, join-in-progress, repeated reconnect and persisted rehost in slower scheduled/release scenarios.

## Dependencies

06-08 networking/session continuity, 14 application graph, representative authoritative domains, shared validation architecture.

## Proof

Separate OS processes, same build SHA, real UTP/session code, semantic diagnostic oracle, role-tagged artifacts.

## Do not build

No WAN simulator, second transport, direct state mutation helpers, or four-client visual matrix on every PR.
