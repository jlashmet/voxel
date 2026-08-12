# Minimal Voxel Networking — Bandwidth Plan

**Status:** M1 event-driven replication foundation implemented on branch
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
9. **Fixed clock, event-driven systems.** Server simulation remains fixed-tick; gameplay publishes semantic authoritative events that replication consumes after the tick is sealed.
10. **Connection-owned identity.** Client packets never establish their own player identity; the server derives attribution from authenticated connection state.

## Traffic classes

### 1. Input / player motion

High-frequency, tiny, ephemeral traffic.

- Client input should become **unreliable sequenced with redundancy**: send the newest input plus a small history window so one dropped datagram does not require retransmission.
- Player state should be delta/quantized and interest-filtered.
- Old motion packets are useless; they must not head-of-line-block world mutations.

This separation is still future work because the repository does not yet contain the concrete UTP host/send loop.

### 2. Durable voxel mutations

Small, high-priority, authoritative traffic.

- Reliable and ordered.
- Replicate `AlterationEvent` semantics, not voxel writes.
- Seal authoritative events at the server tick boundary.
- Route in server arbitration order to interested connections.
- Batch only **consecutive** events sharing `(encodingRegion, serverTick)` so batching can never reorder authority.
- Amortize region/tick metadata across the batch.
- Encode origins relative to the target region.
- Keep each live-event datagram below a conservative non-fragmented payload ceiling.

The implementation on this branch is `AuthoritativeEventStream -> ReplicationRouter -> AlterationBatchPacketSink`.

### 3. Repair

Medium-priority authoritative corrections.

- Reliable.
- Region/brick scoped.
- Triggered by hash mismatch or reconnect gap.
- Prefer the smaller of missing event suffix vs compressed state repair.

### 4. Bulk region/snapshot data

Low-priority, reliable fragmented traffic.

- Rate limited so it cannot increase live EVENT latency.
- Fragmentation precedes reliability in the UTP pipeline.
- Used for late join, region stream-in, and large repair/snapshot transfers.
- Base procedural terrain is regenerated locally; only seeds + touched/compacted state are transferred.

## Event-driven authority boundary

The server tick remains the deterministic clock. Event-driven means simulation/gameplay systems publish semantic facts rather than directly invoking networking code.

```text
commands / inputs
      |
      v
fixed authoritative tick
      |
      +--> authentication + validation + simulation
      |
      v
AuthoritativeEventStream
      |
      +--> persistence / moderation / replay
      |
      v
ReplicationRouter
      |
      +--> simulation-interest filtering
      +--> cross-region recipient union
      +--> ordered batching
      |
      v
AlterationBatchPacketSink
      |
      v
IEventPacketSender  <-- future concrete UTP host adapter
```

Internal gameplay can generate many domain events. Replication does not automatically transmit all of them; it chooses the minimum deterministic facts required for clients to reproduce authoritative state.

## Client alteration request

`C_AlterationRequest` is now a 32-byte payload / 34-byte framed packet:

```text
clientTick     : uint      4 B
origin         : int3     12 B
kind           : byte      1 B
material       : byte      1 B
shapeKind      : uint      4 B
shapeData      : uint      4 B
requestedSeed  : uint      4 B
clientSequence : ushort    2 B
```

There is no `playerId` on the wire. `ClientEventPacketReceiver` passes the transport connection ID separately to the authoritative handler, and `C_AlterationRequest.ToAuthoritativeEvent` requires server-owned tick, player ID, sequence, and seed explicitly.

Using the same `(shapeKind, shapeData)` union as `AlterationEvent` removes the old brush/raw shape conversion ambiguity without increasing the packet size.

## First compact event batch

The previous broadcast wrapper costs approximately 52 bytes for each 32-byte semantic event because every event repeats a 20-byte wrapper containing tick, region, and payload length.

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

The entry is lossless: shared tick is restored on decode and local coordinates are converted back to the original world voxel coordinate.

At 10 events, the previous wrapper model costs about `10 * 52 = 520 B`; the compact batch costs `18 + 10 * 24 = 258 B`, roughly a 50% reduction before transport framing.

`MaxEventsPerBatch` is 48, producing a 1,170-byte payload. The versioned protocol envelope adds two bytes, so the complete custom packet is 1,172 bytes and remains under the 1,200-byte live EVENT ceiling.

## Packet framing

Every custom packet begins with:

```text
protocolVersion : byte
messageKind     : byte
payload         : bytes...
```

UTP already supplies packet boundaries and integrity, so the custom envelope does not duplicate length/checksum fields. Unknown versions and message kinds fail closed.

The packet sink is deliberately transport-independent. `IEventPacketSender` is the single adapter seam where the eventual UTP host will call `NetworkDriver.BeginSend/EndSend` and own retry/back-pressure behavior.

## Interest model

`SimulationInterest` and `RegionSubscriptionIndex` now implement the live replication model:

- one common 300 m initial load radius and 420 m unload hysteresis for all hardware tiers;
- authoritative 512-voxel region edge (`8 voxel/brick * 64 brick/region`);
- full **3D** X/Y/Z interest, important for caves, underground lands, mountains, towers and flying actors;
- arithmetic-shift floor mapping for negative world coordinates;
- persistent connection -> regions and region -> connections indexes;
- event fan-out proportional to interested connections rather than all clients.

Destructive effects can cross region boundaries. The router computes conservative impacted-region bounds and sends the event to the union of subscribers. A connection subscribed to multiple impacted regions receives that authoritative event only once.

The older `InterestFilter` remains in the repository for unrelated/scaffold callers, but the new replication path does not use its device-tier-derived radii.

## Ordering and batching

A tempting optimization is to globally group all events by region before sending. Do **not** do that: it can reorder authoritative events when a connection sees multiple regions.

For each connection, the router first builds one event sequence in server arbitration order. It then combines only adjacent events that share tick and encoding region, up to 48 entries. This preserves `(tick, playerId, sequence)` while still obtaining structural batching savings.

## Prediction and reconciliation

1. Client submits an alteration request and immediately renders it in the speculative overlay.
2. Server maps the connection to authoritative identity, validates reach/rate/protected zones/etc., and selects authoritative ordering/seed.
3. Server materializes and publishes the semantic event to the current authoritative tick stream.
4. At tick seal, replication routes/batches the event for interested clients.
5. Client packet dispatch decodes the compact batch in wire order and feeds the existing deterministic `EventApplication.ApplyWithArbitration` path.
6. Periodic region hash checks detect divergence.
7. Repair supplies authoritative brick/region state when needed.

Do **not** omit the authoritative event to the originating client yet. Echo elision is a later optimization only after deterministic parity, rejection handling, and seed substitution are proven under packet loss/reordering tests.

## Contract/code status

Resolved on this branch:

1. `REPAIR` is now reliable.
2. `BULK` is now fragmentation -> reliable, matching the active contract and Unity Transport's documented stage ordering.
3. Live simulation interest no longer derives from device tier in the new replication path.
4. Compact event batches use an explicit field-level codec instead of relying on the inconsistent legacy 32-byte `AlterationEvent.WireSize()` claim.
5. A versioned message-kind envelope now provides an actual packet dispatch boundary.
6. `C_AlterationRequest` is a canonical 32-byte payload / 34-byte framed packet.
7. Client-authored `playerId` was removed from the request wire format; connection identity owns attribution.
8. Client and server framed receive boundaries exist for alteration requests/batches without depending on a concrete UTP host implementation.

Still open:

1. The repository still lacks a concrete UTP host/connection lifecycle and send loop. `IEventPacketSender` is the adapter seam for it.
2. Ephemeral input/player state should eventually leave the durable reliable mutation stream to avoid head-of-line blocking.
3. Region hash/repair/reconnect/late-join paths are scaffolded but not yet integrated through the new framing/host boundary.
4. The old `InterestFilter` should eventually be renamed/re-scoped to presentation/streaming or removed after callers migrate.
5. `Validation` still contains placeholder reach/rate/player-state logic and must be completed before untrusted network requests can mutate production world state.

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

### M0 — Baseline and invariants — complete

- Add this plan.
- Add a lossless compact same-region/same-tick event batch codec and tests.
- Measure batch savings against the previous wrapper.

### M1 — Event-driven replication foundation — implemented, host adapter pending

- Tick-scoped authoritative event stream.
- Persistent 3D region subscription index.
- Cross-region interest fan-out with recipient deduplication.
- Ordered compact batching.
- Versioned packet framing.
- Canonical framed client alteration request.
- Client batch decode/application bridge.
- Server framed request dispatch with connection-owned identity.
- Transport-independent packet sender seam.

### M2 — Concrete transport + ephemeral traffic

- Add the actual server/client UTP driver and connection lifecycle.
- Implement `IEventPacketSender` using EVENT pipeline send/back-pressure queues.
- Separate ephemeral input/player-state traffic from durable mutation traffic.
- Add queue-age/bandwidth instrumentation and bulk back-pressure.
- Exercise the configured reliable REPAIR and fragmented-reliable BULK pipelines in packet-loss tests.

### M3 — State convergence

- Wire periodic region hashes end-to-end.
- Implement repair-vs-event-suffix cost choice.
- Exercise reconnect and late join against compacted state.

### M4 — Soak and adversarial tests

Test 4-player Mounting Force gameplay first, then stress beyond expected player count:

- simultaneous chain-reaction destruction
- heavy construction/raw editing
- players spread across different and vertically separated regions
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
