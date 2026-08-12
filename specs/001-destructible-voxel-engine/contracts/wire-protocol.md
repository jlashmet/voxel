# Contract — Wire Protocol

**Created:** 2026-08-04  
**Transport:** Unity Transport (UTP), custom replication above it  
**Authority:** server

This file is the externally observable protocol contract. Code may be optimized underneath it, but independently written peers must preserve these semantics.

---

## 1. Channels

Four UTP pipelines are deliberately separated by traffic lifetime:

| Channel | Delivery | Carries |
|---|---|---|
| `EVENT` | reliable, ordered | durable authoritative commands/results, alteration batches, rejections, hash barriers, mismatch reports |
| `EPHEMERAL` | unreliable, sequenced | movement/aim/input where newer data supersedes stale data |
| `REPAIR` | reliable | bounded semantic checkpoint-repair chunks |
| `BULK` | fragmented → reliable | region stream-in, late join, large snapshot/refinement traffic |

`BULK` must be rate limited so it cannot starve `EVENT` or `EPHEMERAL`. Durable world mutations never move to `EPHEMERAL`.

---

## 2. Framing

Every custom packet begins with exactly two bytes:

| Offset | Type | Meaning |
|---|---|---|
| 0 | `byte` | protocol version (`1`) |
| 1 | `byte` | message kind |
| 2.. | bytes | message-specific payload |

UTP already provides packet boundaries and transport integrity, so no redundant custom length/checksum is added unless a variable-size message needs its own semantic lengths.

Current message kinds:

| Value | Kind |
|---:|---|
| 1 | `C_PlayerInput` |
| 2 | `C_AlterationRequest` |
| 3 | `C_RegionRequest` |
| 4 | `C_PlayerInputBundle` |
| 5 | `C_RegionHashMismatch` |
| 32 | `S_AlterationEvent` legacy |
| 33 | `S_AlterationEventBatch` |
| 34 | `S_AlterationRejected` |
| 35 | `S_RegionHash` |
| 36 | `S_RegionRepair` live semantic chunk format |
| 37 | `S_RegionData` |
| 38 | `S_PlayerState` |

Existing values must never be renumbered after shipping.

---

## 3. Client → server

### `C_AlterationRequest` — 32 B payload / 34 B framed, `EVENT`

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

**No player ID is transmitted.** Transport connection → authenticated player mapping owns identity. `requestedSeed` is only a request; the server substitutes authoritative tick, player, sequence and seed before creating an `AlterationEvent`.

Live production application currently supports canonical explosion semantics. Brush/raw-batch requests fail closed until their shared deterministic Core application format is canonical.

### `C_PlayerInput` — 16 B sample

| Field | Type | Bytes |
|---|---|---:|
| clientTick | `uint` | 4 |
| sequence | `ushort` | 2 |
| moveX | `sbyte` | 1 |
| moveY | `sbyte` | 1 |
| viewYaw | `ushort` | 2 |
| viewPitch | `short` | 2 |
| actions | `ushort` | 2 |
| toolMaterial | `byte` | 1 |
| flags | `byte` | 1 |

There is no player ID and no claimed world position. The client sends intent only; authoritative position/reach/collision state lives on the server.

### `C_PlayerInputBundle` — `3 + 16N` B framed, `1 ≤ N ≤ 3`, `EPHEMERAL`

Samples are oldest → newest. Normal steady state sends the newest sample plus two previous samples, max **51 B**. The server validates sequence monotonicity, then deduplicates repeated samples per connection with 16-bit wrap-aware ordering.

### `C_RegionHashMismatch` — 24 B payload / 26 B framed, `EVENT`

| Field | Type | Bytes |
|---|---|---:|
| regionCoord | `int3` | 12 |
| hashTick | `uint` | 4 |
| clientHash | `uint` | 4 |
| serverHash | `uint` | 4 |

A mismatch is only valid if the server actually issued `serverHash` to this authenticated, still-interested connection for exactly `(regionCoord, hashTick)` and retained the exact checkpoint snapshot.

The client cannot directly trigger world repair from a socket callback. Reports enter a separate bounded convergence inbox and are verified on the authoritative tick.

### `C_RegionRequest`

Legacy/current streaming request. BULK region streaming is still being migrated to the concrete runtime and is not part of the completed convergence path below.

---

## 4. Server → client live authority

### `S_AlterationEventBatch` — `18 + 24N` B payload, `N ≤ 48`, `EVENT`

Header:

| Field | Type | Bytes |
|---|---|---:|
| encodingRegion | `int3` | 12 |
| serverTick | `uint` | 4 |
| count | `ushort` | 2 |

Entry:

| Field | Type | Bytes |
|---|---|---:|
| kind | `byte` | 1 |
| material | `byte` | 1 |
| localOrigin | `int16x3` | 6 |
| shapeKind | `uint` | 4 |
| shapeData | `uint` | 4 |
| authoritativeSeed | `uint` | 4 |
| playerId | `ushort` | 2 |
| authoritativeSequence | `ushort` | 2 |

Maximum is **1,170 B payload / 1,172 B framed**, below the 1,200-byte live EVENT ceiling.

Events are sent in authoritative order. A sender may batch only consecutive events sharing tick/encoding region; global regrouping that changes order is forbidden.

**Cause, not effect:** voxel writes, SDF samples, render meshes and GPU buffers never appear in this packet.

### `S_AlterationRejected` — 8 B payload / 10 B framed, `EVENT`

Contains server tick, authoritative player ID and stable reason enum. It tells the speculative client why its requested edit did not become authority.

### `S_RegionHash` — 20 B payload / 22 B framed, `EVENT`

| Field | Type | Bytes |
|---|---|---:|
| regionCoord | `int3` | 12 |
| serverTick | `uint` | 4 |
| semanticHash | `uint` | 4 |

A hash packet is an **ordered authority barrier**. The server queues same-tick alteration EVENT packets first, then the hash, then flushes. The client compares the hash only after every earlier EVENT mutation has applied.

The semantic hash includes:

- region coordinate;
- authored hard-surface bit for every brick;
- uniform material, or all 512 material bytes for a mixed brick.

Allocator-local `BrickPool` indices are explicitly excluded.

A hash is advertised only if the server retained a bounded semantic snapshot for that exact `(region, tick)`, guaranteeing that an accepted mismatch report is repairable.

---

## 5. Exact-checkpoint repair

### Semantic checkpoint snapshot

The live repair source is `SemanticRegionSnapshotCodec`, not raw `BrickRef` values or pool indices.

The snapshot covers all region brick slots sequentially using semantic RLE:

- uniform run: `tag(1) + runLength ushort(2) + material(1) + flags(1)` = **5 B**;
- mixed brick: `tag(1) + flags(1) + 512 material bytes` = **514 B**;
- flags bit 0 = authored hard-surface semantics.

Current checkpoint limits:

- max one-region semantic checkpoint: **256 KiB**;
- server-wide retained checkpoint bytes: **8 MiB**;
- checkpoint retention: currently 90 ticks;
- if a region cannot produce/retain a checkpoint under those limits, its hash is skipped rather than advertising an unrecoverable checkpoint.

### `S_RegionRepair` live chunk — ≤1,024 B framed, `REPAIR`

The historical `S_RegionRepair` struct remains source-compatible, but the live network format is `RegionRepairChunkPacket`:

| Field | Type | Bytes |
|---|---|---:|
| protocolVersion + kind | bytes | 2 |
| regionCoord | `int3` | 12 |
| snapshotTick | `uint` | 4 |
| semanticHash | `uint` | 4 |
| totalSnapshotLength | `uint` | 4 |
| chunkOffset | `uint` | 4 |
| chunkLength | `ushort` | 2 |
| chunk | bytes | ≤992 |

Header is 32 B; maximum packet is **1,024 B** and requires no fragmentation.

The server sends repair chunks under a bounded per-tick packet budget. The client assembler accepts contiguous chunks with identical region/tick/hash metadata; network callbacks only copy bytes.

### Repair barrier semantics

When a client compares a hash and finds drift:

1. it consumes that hash barrier;
2. records `(region, hashTick, serverHash)` as the required repair checkpoint;
3. sends `C_RegionHashMismatch`;
4. **pauses later authoritative world events** at that exact point;
5. server verifies the report against an issued hash and retained snapshot;
6. server sends the exact checkpoint snapshot on `REPAIR`;
7. client validates the full snapshot and pool capacity before mutating anything;
8. client replaces the region semantically;
9. client recomputes the shared semantic hash and requires it to equal `serverHash`;
10. only then does it unpause and apply EVENT authority that arrived after the checkpoint.

This prevents both partial repair and duplicate application of post-checkpoint events.

---

## 6. Fixed-tick authority/trust boundary

Transport pumping runs every frame, but it never advances authoritative world state.

Normal server flow:

1. UTP frame pump decodes into bounded command/convergence inboxes;
2. fixed authoritative tick resolves connection → authenticated player;
3. reject stale/replayed commands and arbitrate deterministically;
4. validate against server-owned position, reach, collision volume, permissions, protected zones, density and rate limits;
5. apply the semantic edit using the shared Core deterministic applier;
6. only a successful real world change becomes an authoritative event;
7. interest-filter and queue alteration EVENT packets;
8. queue due semantic hash barriers;
9. queue bounded REPAIR chunks;
10. flush UTP once.

Client-supplied identity and position are never authority.

The old `ServerTickLoop` networking scaffold is obsolete. `AuthoritativeServerSession` is the canonical live networking/convergence composition root.

---

## 7. Deterministic application and residency

Server and client both call `Core/Edits/DeterministicAlterationApplier`.

For explosions it uses integer sphere tests and brick-batched writes. A peer must have **every region the effect may touch resident before application**. It may not apply only the loaded portion.

Client EVENT batches therefore enter an ordered queue. If the head event needs a non-resident neighboring region, it remains pending and later authority cannot leapfrog it.

Brush/raw-batch application currently fails closed rather than using a client-only/server-only approximation.

---

## 8. Interest management

Simulation interest is platform-neutral and fully 3D. Region edge is the authoritative 512 voxels. The server maintains both connection → regions and region → connections mappings.

Cross-region alterations route to the union of subscribers, with per-connection deduplication. Presentation/draw distance may vary by hardware; simulation visibility may not.

---

## 9. Event history and reconciliation

`RegionEventLog` is a bounded 960-event semantic ring. It uses modulo indexing, supports multiple events at the same tick, and can copy a retained tick range in original authority order.

The former non-power-of-two bitmask ring/tick→single-index scaffold is obsolete.

Player-state reconciliation still needs full integration with authoritative `S_PlayerState`; when implemented, replayed player input must observe world authority at each replayed tick rather than present-time world state.

---

## 10. BULK / late join

Late join remains state-based: procedural base terrain regenerates locally and only current touched/compacted state is transferred. Full BULK region streaming and reconnect fallback for checkpoints outside the repair-retention window remain follow-up work.

**Never send raw session history, render geometry, SDF samples, or allocator-local brick-pool indices as late-join state.**
