# Minimal Voxel Networking — Bandwidth Plan

**Status:** implementation branch design
**Branch:** `feature/minimal-voxel-networking`
**Extends:** `architecture-notes.md` and `contracts/wire-protocol.md`

## Goal

Make the smooth/destructible voxel world practical in multiplayer without replicating voxel effects, render meshes, SDF samples, or per-voxel writes during normal play.

The network sends the smallest deterministic **cause** that can reproduce authoritative state. The server owns the truth; clients predict locally and converge through authoritative events plus state-based repair.

This document does not introduce a second networking stack. It evolves the existing Unity Transport + custom replication design.

## Non-negotiable invariants

1. **Server authority.** Client messages are requests. Only server-accepted alterations become durable world state.
2. **No render replication.** Smooth voxel meshes, raymarch data, SDF textures, GPU buffers, and generated geometry never cross the network.
3. **Cause, not effect.** An explosion that changes 100,000 voxels should cost approximately the same as one that changes 1,000 voxels: origin + shape + seed + attribution/order.
4. **Deterministic expansion.** Accepted alterations expand using the same integer algorithms and authoritative seed on server and clients.
5. **Interest before send.** A client receives live world mutations only for simulation-interest regions it currently subscribes to.
6. **Repairable state.** Periodic hashes detect drift; authoritative state repair fixes it without replaying the full session history.
7. **Late join is state-based.** Base terrain regenerates from seeds and only the compacted edit overlay/state is streamed.
8. **Simulation interest is platform-neutral.** Draw distance/device quality may change presentation, never which nearby gameplay state a player receives.

## Traffic classes

### 1. Input / player motion

High-frequency, tiny, ephemeral traffic.

- Client input should become **unreliable sequenced with redundancy**: send the newest input plus a small history window so one dropped datagram does not require retransmission.
- Player state should be delta/quantized and interest-filtered.
- Old motion packets are useless; they must not head-of-line-block world mutations.

### 2. Durable voxel mutations

Small, high-priority, authoritative traffic.

- Reliable and ordered.
- Replicate `AlterationEvent` semantics, not voxel writes.
- Group events by `(region, serverTick)` before transmission.
- Amortize region/tick metadata across the batch.
- Encode origins relative to the target region when representable.
- Keep each live-event datagram below a conservative non-fragmented payload ceiling.

The first implementation in this branch is `S_AlterationEventBatch`.

### 3. Repair

Medium-priority authoritative corrections.

- Reliable.
- Region/brick scoped.
- Triggered by hash mismatch or reconnect gap.
- Prefer the smaller of missing event suffix vs compressed state repair.

### 4. Bulk region/snapshot data

Low-priority, reliable fragmented traffic.

- Rate limited so it cannot increase live EVENT latency.
- Used for late join, region stream-in, and large repair/snapshot transfers.
- Base procedural terrain is regenerated locally; only seeds + touched/compacted state are transferred.

## First compact event batch

The existing broadcast wrapper costs approximately 52 bytes for each 32-byte event because every event repeats a 20-byte wrapper containing tick, region, and payload length.

For bursts of alterations in one region/tick, use one shared header:

```text
S_AlterationEventBatch header (18 B)
  regionCoord : int3    12 B
  tick        : uint     4 B
  count       : ushort   2 B

Compact entry (24 B)
  kind        : byte     1 B
  material    : byte     1 B
  localOrigin : int16x3  6 B   (relative to region voxel origin)
  shapeKind   : uint     4 B
  shapeData   : uint     4 B
  seed        : uint     4 B
  playerId    : ushort   2 B
  sequence    : ushort   2 B
```

The entry is lossless: shared tick is restored on decode and local coordinates are converted back to the original world voxel coordinate. If an event cannot be represented losslessly (wrong tick or origin outside signed-16 relative range), it is not eligible for that batch and must be sent in another batch/fallback message.

At 10 events, the current wrapper model costs about `10 * 52 = 520 B`; the compact batch costs `18 + 10 * 24 = 258 B`, roughly a 50% reduction before transport headers.

`MaxEventsPerBatch` is initially 48, producing a 1,170-byte payload. That stays conservative for a non-fragmented live-event datagram while leaving room for transport/protocol headers. This ceiling should be tuned from real UTP packet captures rather than guessed upward.

## Interest model

Maintain an explicit server-side subscription set per connection:

- Convert authoritative player/camera gameplay position to simulation regions.
- Add regions at the common simulation load radius.
- Remove them only after a larger hysteresis radius.
- Diff the set; stream snapshot-on-enter and stop live replication after exit.
- Fan each accepted alteration only to connections subscribed to its impacted region(s).

Do **not** enumerate every region every send. Maintain the subscription set incrementally and index interested connections by region so server fan-out is `O(interested clients)`, not `O(all clients * all regions)`.

Destructive effects can cross region boundaries. The server should compute the impacted-region set once from the deterministic alteration bounds and route the event to the union of subscribers. Clients still expand the event once; routing region metadata is not part of simulation semantics.

## Prediction and reconciliation

1. Client submits an alteration request and immediately renders it in the speculative overlay.
2. Server validates reach/rate/protected zones/etc., assigns authoritative ordering/seed, and commits the event.
3. Server batches accepted events by region/tick for interested clients.
4. Client decodes the authoritative event and replaces/reconciles matching speculative state.
5. Periodic region hash checks detect any divergence.
6. Repair supplies authoritative brick/region state when needed.

Do **not** omit the authoritative event to the originating client yet. Echo elision is a later optimization only after deterministic parity, rejection handling, and seed substitution are proven under packet loss/reordering tests.

## Known contract/code mismatches to resolve on this branch

These existed before this branch and affect networking correctness:

1. `contracts/wire-protocol.md` specifies reliable `REPAIR` and reliable fragmented `BULK`; current `ChannelSetup` creates unreliable-sequenced repair and fragmentation-only bulk. Align implementation with the active contract after confirming UTP pipeline stage ordering for the installed package version.
2. The active wire contract says simulation interest must never derive from device tier; current `InterestFilter.LoadRadius(DeviceTier)` does. Split **simulation interest** from presentation/streaming distance.
3. `AlterationEvent` is documented as 32 bytes, but its listed semantic fields total more than that, and `C_AlterationRequest.WireSize` is 34 while the encoder currently writes through byte 31. Establish one canonical field-level codec and make size tests measure encoded bytes, not comments/constants.
4. `EVENT` currently also carries player input/state. Long-term, ephemeral motion/input should not share a reliable ordered queue with durable world mutation because retransmission can create avoidable head-of-line blocking.

## Metrics to add before tuning further

Per connection and per channel:

- encoded payload bytes/s
- transport bytes/s
- packets/s and average fill ratio
- reliable retransmit bytes/s
- EVENT queue age (p50/p95/p99)
- event batch size distribution
- bytes per accepted alteration
- interest-filter fan-out count
- repair bytes and repair frequency
- hash mismatch frequency
- late-join bytes and time-to-playable

Server-wide:

- accepted alterations/s
- event encoding CPU
- interest routing CPU
- deterministic expansion CPU
- snapshot/compaction CPU

## Implementation milestones

### M0 — Baseline and invariants

- Add this plan.
- Add a lossless compact same-region/same-tick event batch codec and tests.
- Measure batch savings against the current wrapper.

### M1 — Transport integration

- Batch accepted alterations at the server tick boundary.
- Route batches using persistent region subscription sets.
- Add client decode/application path.
- Keep legacy single-event message as fallback during migration.

### M2 — Correct channel semantics

- Separate ephemeral input/player-state traffic from durable mutation traffic.
- Align REPAIR/BULK reliability with the active contract.
- Add queue-age/bandwidth instrumentation and bulk back-pressure.

### M3 — State convergence

- Wire periodic region hashes end-to-end.
- Implement repair-vs-event-suffix cost choice.
- Exercise reconnect and late join against compacted state.

### M4 — Soak and adversarial tests

Test 4-player Mounting Force gameplay first, then stress beyond expected player count:

- simultaneous chain-reaction destruction
- heavy construction/raw editing
- players spread across different regions
- packet loss/jitter/reordering
- late join during destruction
- reconnect after a long gap
- server tick spikes

Success is not merely low bandwidth: authoritative EVENT latency must remain stable while BULK streaming and destruction are both active.

## Deliberate non-goals

- Replicating SDF samples, chunks, meshes, or generated render geometry.
- Generic GameObject/RPC replication for voxel state.
- Sending all voxel diffs for deterministic edits.
- Device-dependent gameplay interest radius.
- Premature entropy coding/bit-level packing before structural batching and interest filtering are measured.
