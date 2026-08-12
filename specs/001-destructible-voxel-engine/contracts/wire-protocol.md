# Contract — Wire Protocol

**Created:** 2026-08-04  
**Transport:** Unity Transport (UTP), custom replication above it  
**Authority:** server

This is the externally observable networking contract. Independently written peers must preserve these semantics even if codecs or storage implementations are optimized later.

---

## 1. Channels

| Channel | Delivery | Carries |
|---|---|---|
| `EVENT` | reliable, ordered | durable commands/results, alteration batches, hash barriers, current-state fences |
| `EPHEMERAL` | unreliable, sequenced | input/motion where newer data supersedes stale data |
| `REPAIR` | reliable | bounded exact-checkpoint semantic repair |
| `BULK` | fragmentation → reliable | current region state, reconnect/late-join state, future mip refinement |

`BULK` is rate limited and must yield to latency-sensitive traffic. Durable world mutation never moves to `EPHEMERAL`.

---

## 2. Packet framing

Every custom packet starts with:

| Offset | Type | Meaning |
|---|---|---|
| 0 | `byte` | protocol version (`1`) |
| 1 | `byte` | message kind |
| 2.. | bytes | message-specific payload |

Current stable kinds:

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
| 36 | `S_RegionRepair` semantic chunk |
| 37 | `S_RegionData` semantic BULK chunk |
| 38 | `S_PlayerState` |
| 39 | `S_RegionResyncRequired` |
| 40 | `S_RegionStateFence` |

Unknown versions/kinds fail closed. Existing kind values are never renumbered after shipping.

---

## 3. Client → server

### `C_AlterationRequest` — 32 B payload / 34 B framed, `EVENT`

`clientTick:uint + origin:int3 + kind:byte + material:byte + shapeKind:uint + shapeData:uint + requestedSeed:uint + clientSequence:ushort`.

There is **no player ID**. Connection → authenticated player state supplies identity, authoritative position, reach and permissions. The server substitutes final tick, player, sequence and seed before committing an `AlterationEvent`.

Live deterministic application currently supports explosion semantics; brush/raw-batch fail closed until their shared Core representation is canonical.

### `C_PlayerInput` — 16 B sample

Contains only client tick/sequence, signed movement axes, quantized yaw/pitch, action bits, tool/material and flags. There is no player ID and no claimed world position.

### `C_PlayerInputBundle` — `3 + 16N` B framed, `1 ≤ N ≤ 3`, `EPHEMERAL`

Samples are oldest → newest. Normal steady state sends newest + two prior samples, max **51 B**. Server validates sequence ordering and deduplicates per connection with ushort-wrap-aware comparison.

### `C_RegionHashMismatch` — 24 B payload / 26 B framed, `EVENT`

`regionCoord:int3 + hashTick:uint + clientHash:uint + serverHash:uint`.

Rules:

- connection must be authenticated and still subscribed;
- client/server hashes must differ;
- future hash ticks are invalid;
- a report inside the server retention window is valid only if that exact hash was issued to that connection;
- absence inside the retention window is treated as fabricated/stale input, not as permission to request expensive state;
- history old enough to have legitimately fallen out of retention may escalate to current-state BULK recovery.

### `C_RegionRequest` — 16 B payload / 18 B framed, `EVENT`

| Field | Type | Bytes |
|---|---|---:|
| regionCoord | `int3` | 12 |
| haveMipLevel | `byte` | 1 |
| reserved | bytes | 3 |

`haveMipLevel = 0xFF` means **send a complete current semantic region state**. Lower mip values remain reserved for progressive refinement.

Requests enter `ServerRegionStateRequestInbox`; snapshot serialization never occurs inside the network callback.

---

## 4. Durable EVENT authority

### `S_AlterationEventBatch` — `18 + 24N` B payload, `N ≤ 48`

Maximum: **1,170 B payload / 1,172 B framed**. Only consecutive events with the same server tick/encoding region may batch. Wire order is authoritative order and clients never globally regroup/re-sort.

Cause-not-effect invariant: no ordinary voxel diffs, SDF samples, meshes, render buffers or GPU state are replicated.

### `S_AlterationRejected`

Reliable stable-reason response for speculative client edits that did not become authority.

### `S_RegionHash` — 20 B payload / 22 B framed

`regionCoord:int3 + serverTick:uint + semanticHash:uint`.

A hash is an ordered authority barrier. Same-tick mutation EVENT packets are queued before the hash. The client compares only when that hash reaches the front of the EVENT FIFO.

The semantic hash includes region coordinate, hard-surface semantic bits and material state. Allocator-local `BrickPool` indices are excluded.

Hash work is staggered deterministically by region over the configured interval (30 ticks by default) so all interested regions are not serialized on one server frame. An exact-checkpoint hash is advertised only when a bounded semantic snapshot for that `(region,tick)` is retained.

### `S_RegionResyncRequired` — 17 B payload / 19 B framed

`regionCoord:int3 + failedHashTick:uint + reason:byte`.

Reasons include checkpoint expired, snapshot unavailable and current server state unavailable. It tells the client exact repair cannot satisfy the paused authority state and current/full state is required.

### `S_RegionStateFence` — 20 B payload / 22 B framed

| Field | Type | Bytes |
|---|---|---:|
| transferId | `uint` | 4 |
| regionCoord | `int3` | 12 |
| snapshotTick | `uint` | 4 |

This is the cross-pipeline ordering marker for current-state `BULK` snapshots. The server queues it on reliable `EVENT` **after every EVENT fact represented by the snapshot** and before future ticks can append newer authority.

The fence is required because `EVENT` and `BULK` are independent UTP pipelines and therefore have no cross-pipeline delivery order.

---

## 5. Exact-checkpoint REPAIR

`SemanticRegionSnapshotCodec` is the semantic state format:

- uniform run: `tag(1) + runLength ushort(2) + material(1) + flags(1)` = **5 B**;
- mixed brick: `tag(1) + flags(1) + 512 material bytes` = **514 B**;
- flags bit 0 = hard-surface semantic bit.

No `BrickRef` or pool index is encoded.

Current checkpoint bounds:

- max checkpoint snapshot: **256 KiB/region**;
- retained checkpoint memory: **8 MiB/server**;
- retention: **90 ticks**;
- `REPAIR` packet max: **1,024 B**;
- semantic bytes/chunk: up to **992 B**.

On mismatch, the client pauses exactly after the mismatched hash, server verifies the report, sends that exact checkpoint state, client validates/applies it atomically, recomputes the semantic hash, and resumes later EVENT authority only after equality is proven.

---

## 6. Current semantic region state over BULK

The legacy `S_RegionData` source struct is compatibility scaffolding only. **Live kind 37 is `RegionStateChunkPacket`; allocator-local pool indices must never be put on the wire.**

### `S_RegionData` live chunk — ≤16 KiB framed, `BULK`

Header is **36 B**:

| Field | Bytes |
|---|---:|
| protocol version + kind | 2 |
| transferId | 4 |
| regionCoord | 12 |
| snapshotTick | 4 |
| semanticHash | 4 |
| totalSnapshotLength | 4 |
| chunkOffset | 4 |
| chunkLength | 2 |

Chunk payload is up to **16,348 B**, making the total packet at most **16,384 B**. Current full semantic snapshot cap is **16 MiB/region**.

Server bounds:

- one current-state transfer per connection at a time;
- at most 256 persistently deferred requests;
- at most 64 MiB pending snapshot bytes server-wide;
- at most one BULK packet/connection/tick and a global packet cap;
- a rolling `BulkThrottle` reserves live-traffic bandwidth.

The current throttle uses the wired/Wi-Fi default budget; adaptive connection-quality/mobile budgeting is future work.

### Current-state recovery ordering

A current snapshot may include effects of EVENT packets the client has not applied yet. Therefore simply overwriting a region and replaying the entire EVENT FIFO would double-apply those effects.

Canonical flow:

1. client globally pauses EVENT application for the target region-state request;
2. request travels on reliable EVENT;
3. at fixed server tick `T`, all gameplay mutations are resolved/applied;
4. server queues mutation EVENT batches and due hash barriers;
5. server captures current semantic region state and queues `S_RegionStateFence(transferId, region, T)` on EVENT;
6. server sends the snapshot chunks on throttled fragmented-reliable BULK;
7. delivery order between BULK and EVENT is irrelevant;
8. client assembles BULK bytes without touching the world;
9. from the explicit client world-update path, it atomically installs the semantic snapshot and verifies its advertised hash;
10. client replays queued EVENT authority through tick `T` **everywhere except the replaced region**;
11. hashes for the replaced region at or before `T` are superseded by the snapshot;
12. only the matching EVENT fence ends this catch-up mode;
13. newer EVENT authority then resumes normally.

Catch-up remains active even if the EVENT queue temporarily becomes empty; queue emptiness is never treated as an ordering proof. Only the matching fence can end duplicate suppression.

This same mechanism is the foundation for expired-checkpoint recovery, reconnect and late join. Current full snapshots transfer semantic state rather than the eventual optimized base-seed + touched-overlay representation.

---

## 7. Fixed-tick trust boundary

Frame-level UTP pumping only decodes/copies into bounded inboxes/assemblers. World mutation is never executed from a transport callback.

Canonical server tick:

1. process authenticated convergence reports;
2. drain/resolve commands and EPHEMERAL input;
3. validate connection-owned identity/position/reach/permissions/rate/zone state;
4. apply deterministic world mutations;
5. queue alteration EVENT batches;
6. queue due semantic hash barriers;
7. process bounded full-region requests, capture state and queue EVENT fences;
8. queue bounded REPAIR packets;
9. queue throttled BULK packets;
10. flush transport once.

`AuthoritativeServerSession` is the canonical server networking composition root. The old `ServerTickLoop` network scaffold is obsolete.

---

## 8. Deterministic application / streaming residency

Server and client use `Core/Edits/DeterministicAlterationApplier`.

Normal authoritative events require every region they may affect to be resident before application. During current-state catch-up, pre-fence events are deterministically applied to every required region **except** the one already replaced by the snapshot; all other affected regions still require residency.

Current canonical mutation support is explosion. Brush/raw-batch fail closed.

---

## 9. Interest, reconnect and late join

Simulation interest is platform-neutral, fully 3D and based on the authoritative 512-voxel region edge. Server indexes both connection→regions and region→connections.

Full-state requests are accepted only from authenticated connections currently subscribed to the requested simulation region.

Current-state BULK transfer now provides the correctness foundation for reconnect/late join. Remaining bandwidth optimization is to regenerate procedural base terrain from seeds and transfer only touched/compacted overlay state rather than a complete semantic region snapshot when economical.

**Never send raw session history, render geometry, SDF samples, GPU state, `BrickRef`, or allocator-local `BrickPool` indices as authoritative region state.**
