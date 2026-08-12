# Contract — Wire Protocol

**Created**: 2026-08-04
**Transport**: Unity Transport (UTP). Custom replication above it (R-001).
**Authority**: server (C-005). No client message mutates world state without server adjudication.

The externally-observable interface of this system is the client/server protocol. It is specified here because it is the boundary two independently-written implementations must agree on, and because its channel separation is a correctness property rather than an optimisation.

---

## Channels

Four UTP pipelines, deliberately separate by traffic lifetime and delivery semantics.

| Channel | Delivery | Priority | Carries |
|---|---|---|---|
| `EVENT` | Reliable, ordered | High | Durable authoritative world/gameplay events and confirmations |
| `EPHEMERAL` | Unreliable, sequenced | High | Player input/motion samples where newer data supersedes old data |
| `REPAIR` | Reliable | Medium | Authoritative brick blobs correcting detected drift |
| `BULK` | Reliable, fragmented | **Low — must never starve live traffic** | Region stream-in, mip refinement, late-join snapshots |

`BULK` pipeline stage order is fragmentation then reliability. A lost fragment therefore retransmits that fragment rather than the entire logical payload.

**Invariant**: `BULK` is rate-limited against the connection's estimated capacity such that `EVENT` and `EPHEMERAL` latency are unaffected. This is load-bearing for the mobile target (C-002, SC-014).

Durable world mutations never move to `EPHEMERAL`. Conversely, ordinary movement/aim samples never use reliable `EVENT`, because retransmitting stale motion would head-of-line-block authoritative combat/destruction traffic.

---

## Packet framing

UTP already supplies packet boundaries, delivery semantics, and transport integrity. The custom protocol therefore adds only a two-byte envelope:

| Offset | Type | Meaning |
|---|---|---|
| 0 | `byte` | protocol version (`1`) |
| 1 | `byte` | message kind |
| 2.. | bytes | message-specific payload |

There is deliberately no redundant length or checksum field. Unsupported versions or unknown message kinds fail closed.

Message-kind values are stable once shipped:

| Value | Kind |
|---:|---|
| 1 | `C_PlayerInput` |
| 2 | `C_AlterationRequest` |
| 3 | `C_RegionRequest` |
| 4 | `C_PlayerInputBundle` |
| 32 | `S_AlterationEvent` |
| 33 | `S_AlterationEventBatch` |
| 34 | `S_AlterationRejected` |
| 35 | `S_RegionHash` |
| 36 | `S_RegionRepair` |
| 37 | `S_RegionData` |
| 38 | `S_PlayerState` |

---

## Message types

Sizes below are message payloads unless explicitly stated; the two-byte protocol envelope is additional.

### Client → Server

#### `C_AlterationRequest` (32 B payload / 34 B framed)

| Field | Type | Bytes |
|---|---|---:|
| clientTick | `uint` | 4 |
| origin | `int3` | 12 |
| kind | `byte` | 1 |
| material | `byte` | 1 |
| shapeKind | `uint` | 4 |
| shapeData | `uint` | 4 |
| requestedSeed | `uint` | 4 |
| clientSequence | `ushort` | 2 |

The 8-byte `(shapeKind, shapeData)` union is deliberately the same semantic representation used by `AlterationEvent`; request → authoritative event therefore needs no lossy shape repacking.

**There is no player ID in this message.** Player identity comes from the authenticated connection and is supplied by the server when materializing the authoritative event. Accepting a client-authored player ID would waste bandwidth and create an attribution/spoofing bug.

The client has already applied the request to its speculative overlay. **The seed is a request, not a fact** — the server may substitute its own. The server also owns the final tick and authoritative sequence.

Raw single-voxel edits are never sent individually: they are buffered ~100 ms and coalesced into one run-length-encoded `raw-batch` scoped to a single brick.

#### `C_PlayerInput` (16 B sample / 18 B single-sample framed, `EPHEMERAL`)

| Field | Type | Bytes |
|---|---|---:|
| clientTick | `uint` | 4 |
| sequence | `ushort` | 2 |
| moveX | `sbyte` | 1 |
| moveY | `sbyte` | 1 |
| viewYaw | `ushort` | 2 |
| viewPitch | `short` | 2 |
| actions | `ushort` bitfield | 2 |
| toolMaterial | `byte` | 1 |
| flags | `byte` | 1 |

There is **no player ID and no claimed world position**. The authenticated connection establishes identity and the server simulation owns position. The client sends intent only.

Movement is quantised to `[-127,+127]` per axis. Yaw covers a full turn in 16 bits; pitch covers `[-90,+90]` degrees in a signed 16-bit value.

`C_PlayerInput` is the canonical sample codec. The concrete client normally transmits samples inside `C_PlayerInputBundle`; the single-sample framed form remains valid for tests/compatibility and simple peers.

#### `C_PlayerInputBundle` (3 + 16N B framed, 1 ≤ N ≤ 3, `EPHEMERAL`)

| Field | Type | Bytes |
|---|---|---:|
| protocolVersion | `byte` | 1 |
| messageKind | `byte` | 1 |
| count | `byte` | 1 |
| samples | `C_PlayerInput[count]` | `16N` |

Samples are ordered **oldest → newest**. The normal steady-state client sends the newest sample plus the two previous samples, producing a maximum 51-byte datagram. This deliberately repeats recent input so an isolated lost packet does not lose an action edge.

The server validates monotonic 16-bit sample sequence order before dispatching any sample, then deduplicates sequences per transport connection. Sequence comparison handles ushort wraparound as long as the gap is below half the sequence space. Duplicate samples are valid but are not delivered twice to gameplay.

#### `C_RegionRequest` (~16 B)

| Field | Type |
|---|---|
| coord | `int3` |
| haveMipLevel | `byte` — `0xFF` if nothing held |

`haveMipLevel` enables **refinement rather than refetch**: approaching a distant region ships only the levels not already held. Each level is ~1/8 the next, so progressive refinement is nearly free.

### Server → Client

#### `S_AlterationEvent` (legacy single-event form)

Semantic fields:

| Field | Type |
|---|---|
| tick | `uint` |
| kind | `byte` |
| origin | `int3` |
| shape | union |
| material | `byte` |
| seed | `uint` — **authoritative** |
| playerId | `ushort` |
| sequence | `ushort` |

Expands deterministically on every client to potentially thousands of voxel writes. It is retained as a compatibility/fallback concept while the canonical single-event codec is cleaned up; live replication should prefer the compact batch below.

#### `S_AlterationEventBatch` (18 + 24N B payload, N ≤ 48)

The normal durable world-mutation packet. Events must share a server tick and encoding region. The header amortises tick/region metadata and origins are encoded relative to the region.

Header:

| Field | Type | Bytes |
|---|---|---:|
| regionCoord | `int3` | 12 |
| tick | `uint` | 4 |
| count | `ushort` | 2 |

Each entry:

| Field | Type | Bytes |
|---|---|---:|
| kind | `byte` | 1 |
| material | `byte` | 1 |
| localOrigin | `int16x3` | 6 |
| shapeKind | `uint` | 4 |
| shapeData | `uint` | 4 |
| seed | `uint` | 4 |
| playerId | `ushort` | 2 |
| sequence | `ushort` | 2 |

Maximum payload is 1,170 B for 48 events; with the two-byte envelope the packet is 1,172 B. Live mutation packets stay at or below a conservative 1,200-byte non-fragmented ceiling.

**Ordering invariant**: events arrive in server arbitration order `(tick, playerId, sequence)`. Clients apply wire order directly and never re-sort. A sender may combine only consecutive events with the same tick/encoding region; it must not globally regroup events in a way that changes authoritative order.

**Cause-not-effect invariant**: the batch contains deterministic causes. It never contains SDF samples, generated meshes, GPU buffers, or ordinary per-voxel destruction results.

#### `S_AlterationRejected` (~8 B)

| Field | Type |
|---|---|
| tick | `uint` |
| playerId | `ushort` |
| reason | `byte` |

Reason codes are a contract, not debug text — FR-009 requires the player be shown why.

#### `S_RegionHash` (~17 B)

| Field | Type |
|---|---|
| coord | `int3` |
| hash | `uint` |

Cheap periodic drift detection. Mismatch → client requests repair. The concrete codec is authoritative over older indicative sizes in design prose.

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

## Fixed-tick, event-driven authority

The simulation clock remains fixed and authoritative. Event-driven means systems communicate through semantic events inside that tick; it does **not** mean callbacks advance world state at arbitrary wall-clock times.

Per server tick:

1. frame-level transport pumping decodes packets into a bounded command inbox;
2. the fixed tick drains `EPHEMERAL` input and durable `EVENT` commands;
3. authenticate connection-owned identity, validate requests, and resolve simulation in deterministic arbitration order;
4. substitute authoritative tick/sequence/seed where required and publish semantic authoritative events;
5. seal the tick event stream;
6. persistence/moderation/replication consume the sealed stream;
7. replication interest-filters events, batches consecutive compatible alterations, writes `EVENT` packets, then flushes sends once.

Internal gameplay may produce more domain events than are transmitted. Replication sends only the minimum facts required for another client to reconstruct authoritative state.

---

## Reconciliation

1. Client receives `S_PlayerState` for tick *T*.
2. Client rewinds its own player to that state.
3. Client replays buffered inputs from *T+1* to now — **against world state at each replayed tick**, obtained by querying the region event log by tick, not against present world state.
4. Divergence beyond threshold snaps; otherwise it is smoothed.

**Invariant**: step 3 is why `RegionEventLog.tickIndex` must exist from day one. Reconciling against present-tick world state is a divergence defect.

---

## Interest management

Every world and player message is position-tagged and filtered by receiver interest before send.

Simulation interest is fully 3D. Region coordinates use the authoritative 512-voxel region edge, including correct floor mapping for negative world coordinates. This matters for caves, underground kingdoms, mountains, towers, flying actors, and vertically separated gameplay.

The server maintains both connection → regions and region → connections mappings. Event fan-out uses the inverse index and therefore scales with interested receivers rather than all connected clients. Cross-region effects route to the union of subscribers and each connection receives a given authoritative event at most once.

**Invariant**: interest radius is a **simulation** parameter and must never be derived from draw distance or device tier (C-006). Bandwidth scheduling may adapt to connection quality; simulation visibility may not.

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
