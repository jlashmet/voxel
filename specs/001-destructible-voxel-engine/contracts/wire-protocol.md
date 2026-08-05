# Contract — Wire Protocol

**Created**: 2026-08-04
**Transport**: Unity Transport (UTP). Custom replication above it (R-001).
**Authority**: server (C-005). No client message mutates world state without server adjudication.

The externally-observable interface of this system is the client/server protocol. It is specified here because it is the boundary two independently-written implementations must agree on, and because its channel separation is a correctness property rather than an optimisation.

---

## Channels

Three UTP pipelines, deliberately separate. Collapsing them is the failure mode where a player parachuting across the map adds latency to everyone's combat.

| Channel | Delivery | Priority | Carries |
|---|---|---|---|
| `EVENT` | Reliable, ordered per region | High | Alteration events, player input, player state |
| `REPAIR` | Reliable | Medium | Authoritative brick blobs correcting detected drift |
| `BULK` | Reliable, fragmented | **Low — must never starve `EVENT`** | Region stream-in, mip refinement, late-join snapshots |

**Invariant**: `BULK` is rate-limited against the connection's estimated capacity such that `EVENT` latency is unaffected. This is load-bearing for the mobile target (C-002, SC-014).

---

## Message types

Sizes are indicative payload, excluding transport headers.

### Client → Server

#### `C_AlterationRequest` (~34 B)

| Field | Type |
|---|---|
| clientTick | `uint` |
| kind | `byte` — explosion / brush / raw-batch |
| origin | `int3` |
| shape | union — radius \| brush extents + rotation \| prefab id |
| material | `byte` |
| seed | `uint` |

The client has already applied this to its speculative overlay. **The seed is a request, not a fact** — the server may substitute its own, and the client must accept the server's value.

Raw single-voxel edits are never sent individually: they are buffered ~100 ms and coalesced into one run-length-encoded `raw-batch` scoped to a single brick.

#### `C_PlayerInput` (~16 B)

| Field | Type |
|---|---|
| clientTick | `uint` |
| movement | quantised `int2` |
| actions | `ushort` bitfield |
| viewDirection | quantised `int2` |

Sent redundantly across several ticks; the server keeps an input ring buffer.

#### `C_RegionRequest` (~16 B)

| Field | Type |
|---|---|
| coord | `int3` |
| haveMipLevel | `byte` — `0xFF` if nothing held |

`haveMipLevel` enables **refinement rather than refetch**: approaching a distant region ships only the levels not already held. Each level is ~1/8 the next, so progressive refinement is nearly free.

### Server → Client

#### `S_AlterationEvent` (~32 B)

| Field | Type |
|---|---|
| tick | `uint` |
| kind | `byte` |
| origin | `int3` |
| shape | union |
| material | `byte` |
| seed | `uint` — **authoritative** |
| playerId | `ushort` |

Expands deterministically on every client to potentially thousands of voxel writes. **This message is why SC-002 holds and why 64 players fit a constrained mobile connection.**

#### `S_AlterationRejected` (~10 B)

| Field | Type |
|---|---|
| clientTick | `uint` |
| reason | `byte` — enum below |

Reason codes are a contract, not debug text — FR-009 requires the player be shown why.

`OUT_OF_REACH` · `PROTECTED_ZONE` · `RATE_LIMITED` · `DENSITY_CAP` · `NOT_ATTACHED` · `REGION_NOT_LOADED` · `IMPLAUSIBLE`

#### `S_RegionHash` (~16 B)

| Field | Type |
|---|---|
| coord | `int3` |
| tick | `uint` |
| hash | `ulong` |

Cheap periodic drift detection. Mismatch → client requests repair.

#### `S_RegionRepair` (variable, `REPAIR` channel)

| Field | Type |
|---|---|
| coord | `int3` |
| tick | `uint` |
| bricks | compressed brick blobs |

Authoritative state-based correction. The repair half of "event-sourced with state-based repair".

#### `S_RegionData` (variable, `BULK` channel)

| Field | Type |
|---|---|
| coord | `int3` |
| mipLevel | `byte` |
| terrainSeed | `uint` |
| editOverlay | compressed |

**Only the edit overlay is transmitted.** Base terrain is regenerated client-side from the seed and costs zero bandwidth — this is what makes streaming cost scale with how much of the world has been *touched* rather than how large it is.

#### `S_PlayerState` (~20 B/player)

Delta-encoded against the last acknowledged snapshot, filtered by interest management.

| Field | Type |
|---|---|
| tick | `uint` |
| playerId | `ushort` |
| position | quantised `int3` |
| viewDirection | quantised `int2` |
| stateFlags | `ushort` |

---

## Reconciliation

The delicate part, and the reason no third-party framework was adopted (R-001).

1. Client receives `S_PlayerState` for tick *T*.
2. Client rewinds its own player to that state.
3. Client replays buffered inputs from *T+1* to now — **against world state at each replayed tick**, obtained by querying the region event log by tick, not against present world state.
4. Divergence beyond threshold snaps; otherwise it is smoothed.

**Invariant**: step 3 is why `RegionEventLog.tickIndex` must exist from day one. Reconciling against present-tick world state is the specific defect that eliminated Photon Fusion 2, whose resimulation does not roll back non-networked state.

---

## Interest management

Every world and player message is position-tagged and filtered by receiver interest before send.

**Invariant**: interest radius is a **simulation** parameter and must never be derived from draw distance or device tier (C-006). Coupling them would silently disadvantage mobile players. This is the specific C-006 trap; enforce by test (SC-013), not by convention.

---

## Session lifecycle

| Phase | Exchange |
|---|---|
| Connect | Handshake → world seed, material palette, protected zone masks, tick rate, server tick |
| Late join | Top-level mips for the whole world (small, immediate) → `BULK` refinement near the spawn point |
| Reconnect | Client reports last known tick per region; server sends repair or full region data by cost |
| Disconnect | Server retains player-attributed alterations; world state is unaffected |
| Session end | All alterations discarded (Q2, FR-031) |

**Invariant**: late join never replays session history (FR-024). It ships compacted current state.
