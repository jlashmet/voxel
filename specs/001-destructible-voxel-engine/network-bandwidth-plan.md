# Minimal Voxel Networking — Bandwidth Plan

**Status:** M1 complete; M2 concrete transport + ephemeral input foundation implemented
**Branch:** `feature/minimal-voxel-networking`
**Extends:** `architecture-notes.md` and `contracts/wire-protocol.md`

## Goal

Make the smooth/destructible voxel world practical in multiplayer without replicating voxel effects, render meshes, SDF samples, GPU buffers, or ordinary per-voxel writes.

The network sends the smallest deterministic **cause** that can reproduce authoritative state. The server owns truth; clients predict locally and converge through authoritative events plus state-based repair.

## Non-negotiable invariants

1. **Server authority.** Client messages are requests/intent. Only server-accepted facts become durable state.
2. **No render replication.** SDF/render meshes/textures/generated geometry never cross the network.
3. **Cause, not effect.** Huge destruction should cost roughly the same as small destruction when the semantic cause is the same shape/seed.
4. **Deterministic expansion.** Accepted alterations expand with the same integer algorithms and authoritative seed.
5. **Interest before send.** Live world mutations only reach clients subscribed to impacted simulation regions.
6. **Repairable state.** Hashes detect drift; state repair converges without replaying full session history.
7. **Late join is state-based.** Base terrain regenerates from seeds; only touched/compacted state streams.
8. **Simulation interest is platform-neutral.** Hardware tier may change presentation, never gameplay visibility.
9. **Fixed clock, event-driven systems.** Network callbacks queue intent; only the fixed tick mutates authoritative simulation.
10. **Connection-owned identity.** Client packets never establish player identity or authoritative position.
11. **Traffic lifetime determines delivery.** Durable facts are reliable; stale motion is not retransmitted.

## Traffic classes

### 1. EPHEMERAL input / motion — implemented

High-frequency, tiny, supersedable traffic.

- Dedicated `UnreliableSequencedPipelineStage` pipeline.
- `C_PlayerInput` is 16 B payload / 18 B single-sample framed packet.
- No client-authored player ID.
- No client-authored world position.
- Movement is signed 8-bit per axis.
- View is yaw/pitch quantised to 16 bits each.
- Actions are a 16-bit bitfield.
- Client automatically sends newest + up to two previous samples.
- Steady-state redundant bundle: **51 B** (`2 B envelope + 1 B count + 3 * 16 B`).
- Server sequence-deduplicates per connection, including ushort wraparound.

At 30 Hz the custom steady-state input payload is about `51 * 30 = 1530 B/s` per client before UTP/UDP/IP headers. This is intentionally cheap enough to buy redundancy without reliable retransmission.

The next packet can recover an isolated dropped action edge because it repeats the previous two samples. Newer datagrams still supersede old datagrams at the UTP pipeline level.

### 2. EVENT durable voxel/gameplay mutations — implemented foundation

- Reliable and ordered.
- Replicate semantic `AlterationEvent` causes, not voxel writes.
- Seal authoritative events at the server tick boundary.
- Route in server arbitration order.
- Batch only **consecutive** events sharing `(encodingRegion, serverTick)` so batching never reorders authority.
- Region/tick metadata is amortised across the batch.
- Origins are region-relative.
- Maximum current framed alteration batch: **1172 B**, below the 1200 B live-event ceiling.

At 10 same-region events the previous wrapper model was about 520 B; compact batching is 258 B before the two-byte envelope.

### 3. REPAIR

Medium-priority authoritative correction.

- Reliable pipeline is configured.
- Region/brick scoped.
- Triggered by hash mismatch or reconnect gap.
- Prefer the smaller of missing event suffix vs compressed state repair.
- End-to-end framed repair dispatch is still open.

### 4. BULK

Low-priority large state transfer.

- Fragmentation -> reliability pipeline is configured.
- Used for region stream-in, late join, snapshots, and large state repair.
- Rate limited so it cannot increase EVENT/EPHEMERAL latency.
- Base terrain regenerates locally; only seeds + touched state transfer.
- End-to-end host integration/back-pressure instrumentation is still open.

## Concrete runtime boundary

The transport host now exists. `NetworkDriver` is isolated from simulation/replication code.

```text
Unity frame loop
      |
      v
UtpServerHost.ScheduleUpdate
      |
      +--> EVENT decode --------+
      |                         |
      +--> EPHEMERAL decode ----+
                                v
                       ServerCommandInbox
                       (bounded, untrusted intent)
                                |
                                | fixed 30 Hz drain
                                v
                   authentication + validation
                                |
                                v
                      authoritative simulation
                                |
                                v
                   AuthoritativeEventStream
                                |
                 +--------------+--------------+
                 |                             |
          persistence/replay            ReplicationRouter
                                               |
                                      3D interest filtering
                                      ordered compact batches
                                               |
                                               v
                                  AlterationBatchPacketSink
                                               |
                                               v
                                        UtpServerHost
                                               |
                                       one send flush/tick
```

`UtpClientHost` owns client connect/disconnect, channel-aware receive pumping, durable alteration sends, and redundant EPHEMERAL input history.

### Connection IDs

UTP connection handles remain transport details. The server assigns monotonically increasing `uint` connection IDs and never derives game identity from a client payload. Those IDs are the keys used by subscription state, command queues, and eventual authenticated player mapping.

Disconnect immediately removes:

- region subscriptions;
- EPHEMERAL sequence/dedup state;
- queued unvalidated commands when using the recommended shared `ServerCommandInbox` composition.

## Bounded command ingress

`ServerCommandInbox` is the frame-pump -> fixed-tick choke point.

- Default maximum pending per connection: 256 commands.
- Default global maximum: 4096 commands.
- Tracks dropped commands for telemetry.
- Preserves server-observed arrival ordinal for diagnostics only.
- Arrival order is **not** authoritative arbitration.
- Simulation drains into caller-owned reusable lists at a fixed tick boundary.
- Dead connections lose any intent that has not yet crossed authentication/validation.

The current implementation assumes transport pumping and simulation draining are on the same owning thread. If transport is later moved to a worker, this boundary must become an explicitly synchronized/SPSC queue instead of adding incidental locks throughout gameplay code.

## Client alteration request

`C_AlterationRequest` is 32 B payload / 34 B framed:

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

There is no `playerId` on the wire. Connection state supplies identity. Server code must also select authoritative tick, sequence, and seed before materialising the final `AlterationEvent`.

## Compact authoritative event batch

```text
S_AlterationEventBatch header (18 B)
  regionCoord : int3    12 B
  tick        : uint     4 B
  count       : ushort   2 B

Compact entry (24 B)
  kind        : byte     1 B
  material    : byte     1 B
  localOrigin : int16x3  6 B
  shapeKind   : uint     4 B
  shapeData   : uint     4 B
  seed        : uint     4 B
  playerId    : ushort   2 B
  sequence    : ushort   2 B
```

`MaxEventsPerBatch = 48`, so payload is 1170 B and framed packet is 1172 B.

## Packet framing

Every custom packet begins with only:

```text
protocolVersion : byte
messageKind     : byte
payload         : bytes...
```

UTP already supplies packet boundaries and integrity; length/checksum fields here would be duplicate overhead. Unknown versions/kinds fail closed.

## Interest model

`SimulationInterest` + `RegionSubscriptionIndex` now provide:

- common 300 m load / 420 m unload hysteresis across hardware tiers;
- correct 512-voxel region edge;
- full X/Y/Z interest for caves, underground lands, mountains, towers, and flying actors;
- arithmetic-shift floor mapping for negative coordinates;
- connection -> regions and region -> connections indexes;
- cross-region effect routing to the union of subscribers;
- at-most-once fan-out of one authoritative event to a connection even when several impacted regions overlap its subscription set.

The older `InterestFilter` remains only for scaffold/unmigrated callers and is not used by the new live replication path.

## Ordering and prediction

For each connection, the router first builds one sequence in server arbitration order. It combines only adjacent compatible events. Global regrouping by region is forbidden because it could change authority order.

Prediction path remains:

1. client submits alteration and shows speculative overlay;
2. server connection identity + validation decides acceptance;
3. server substitutes authoritative order/seed and publishes semantic event;
4. tick seals;
5. interest routing/batching sends reliable EVENT;
6. client applies wire order through `EventApplication.ApplyWithArbitration`;
7. periodic hashes detect divergence;
8. repair converges state.

Do not elide the authoritative echo to the originating client until deterministic parity, rejection handling, and seed substitution have been exercised under loss/reordering.

## Current status

### Resolved / implemented

1. Compact alteration batch codec and bandwidth tests.
2. Versioned message framing.
3. Canonical 34 B framed alteration request with connection-owned identity.
4. Full 3D platform-neutral simulation interest and inverse subscription index.
5. Cross-region recipient union + deduplication.
6. Ordered per-connection batching.
7. Reliable EVENT / reliable REPAIR / fragmented-reliable BULK semantics.
8. Concrete UTP 6.5 server/client host lifecycle.
9. Stable server-owned connection IDs.
10. Concrete `IEventPacketSender` adapter.
11. Real loopback integration test covering request -> server and authoritative batch -> client.
12. Separate unreliable-sequenced EPHEMERAL pipeline.
13. 16 B player input payload with no identity/position spoof fields.
14. Automatic three-sample EPHEMERAL redundancy and sequence deduplication.
15. Bounded frame-to-fixed-tick `ServerCommandInbox`.

### Still open

1. Complete real authentication/connection -> player mapping.
2. Finish `Validation` reach/rate/player-state logic before untrusted requests may mutate production state.
3. Wire the fixed server simulation consumer to drain `ServerCommandInbox` and publish accepted events.
4. Frame and integrate region hash/repair/reconnect/late-join paths through the concrete host.
5. Add per-channel bytes/packets/queue-age/retransmit instrumentation and BULK back-pressure.
6. Add authoritative `S_PlayerState` delta snapshots and client reconciliation over the EPHEMERAL input history.
7. Rename/remove old device-tier `InterestFilter` after remaining callers migrate.

## Metrics to add before tuning

Per connection/channel:

- encoded payload bytes/s;
- transport bytes/s;
- packets/s + fill ratio;
- reliable retransmit bytes/s;
- EVENT queue age p50/p95/p99;
- EPHEMERAL samples sent, recovered from redundancy, duplicates discarded;
- event batch size distribution;
- bytes per accepted alteration;
- interest fan-out count;
- repair frequency/bytes;
- hash mismatch frequency;
- late-join bytes/time-to-playable.

Server-wide:

- accepted/rejected/dropped commands per second;
- event encoding CPU;
- interest routing CPU;
- deterministic expansion CPU;
- snapshot/compaction CPU.

## Milestones

### M0 — baseline/invariants — complete

Compact cause codec, tests, and explicit bandwidth plan.

### M1 — event-driven replication — complete

Authoritative event stream, 3D subscriptions, interest routing, ordered compact batches, framing, client application bridge.

### M2 — concrete transport + ephemeral traffic — foundation complete

Concrete UTP hosts, connection lifecycle, loopback test, separate EPHEMERAL pipeline, compact redundant input, bounded fixed-tick ingress.

Remaining M2 work is primarily instrumentation/back-pressure and integrating the inbox with real authentication/validation/simulation.

### M3 — state convergence

Region hashes, repair-vs-event-suffix cost choice, reconnect, late join, player-state reconciliation.

### M4 — adversarial/soak

- simultaneous chain-reaction destruction;
- heavy construction/raw editing;
- players spread horizontally and vertically;
- packet loss/jitter/reordering;
- lost input/action-edge recovery;
- late join during destruction;
- reconnect after long gap;
- server tick spikes;
- BULK saturation while EVENT/EPHEMERAL latency remains stable.

## Deliberate non-goals

- Replicating SDF samples, chunks, meshes, or generated render geometry.
- Generic GameObject/RPC replication for voxel state.
- Sending ordinary per-voxel diffs for deterministic edits.
- Device-dependent gameplay interest radius.
- Premature entropy coding before structural batching/interest filtering are measured.
