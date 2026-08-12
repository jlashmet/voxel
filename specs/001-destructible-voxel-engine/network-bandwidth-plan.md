# Minimal Voxel Networking — Bandwidth Plan

**Status:** M0–M2 foundations implemented; M3 exact-checkpoint convergence foundation implemented  
**Branch:** `feature/minimal-voxel-networking`  
**Extends:** `architecture-notes.md` and `contracts/wire-protocol.md`

## Goal

Make smooth/destructible multiplayer practical without replicating voxel effects, render meshes, SDF samples, GPU buffers, or ordinary per-voxel writes.

The network transmits the smallest deterministic **cause** that reproduces authoritative state. The server owns truth; clients predict and converge using ordered semantic events plus bounded semantic state repair.

## Non-negotiable invariants

1. Server authority: packets are requests/intent, never direct state mutation.
2. Connection-owned identity: client payloads do not establish player identity or authoritative position.
3. Fixed clock + event-driven systems: frame-level network callbacks queue/copy bytes only; the fixed tick owns simulation changes.
4. Cause, not effect: destruction transmits semantic origin/shape/seed/order, not voxel diffs.
5. Same deterministic implementation on both peers: shared Core alteration applier and semantic region hasher.
6. Interest before send: live authority is filtered by platform-neutral 3D simulation interest.
7. Traffic lifetime determines transport semantics: stale input is unreliable; durable facts are reliable.
8. No partial authority from streaming state: an event waits until all regions it may touch are resident.
9. Hashes are ordered barriers, not asynchronous observations.
10. Repair replaces exact semantic checkpoint state; allocator-local pool indices never cross the network.
11. Late join/reconnect are state-based, not full-history replay.

---

## Traffic classes

### EPHEMERAL input — implemented

- UTP `UnreliableSequencedPipelineStage`.
- Canonical input sample: **16 B**.
- No player ID; no claimed position.
- Normal packet: newest + two previous samples = max **51 B** framed.
- At 30 Hz: about **1,530 B/s/client** custom payload before transport headers.
- Server validates oldest→newest sequence order and deduplicates repeats per connection, including ushort wraparound.

### EVENT durable authority — implemented foundation

- Reliable ordered pipeline.
- `C_AlterationRequest`: **34 B framed**.
- `S_AlterationEventBatch`: max **1,172 B framed** for 48 semantic alterations.
- Only consecutive events sharing tick/encoding region may batch.
- Cross-region fan-out uses subscriber union and per-connection deduplication.
- Alteration rejection: **10 B framed**.
- Region hash barrier: **22 B framed**.
- Region mismatch report: **26 B framed**.

### REPAIR — exact-checkpoint semantic repair implemented

- Reliable, non-fragmented pipeline.
- Live chunk max **1,024 B**, with up to **992 B semantic snapshot data**.
- Server retains bounded semantic snapshots only for advertised hash checkpoints.
- Default repair scheduler queues at most two repair packets per authoritative tick across active repairs; current loop favors fairness by advancing at most one chunk per repair per pass.
- Client pauses later EVENT authority at the mismatched hash barrier until the matching snapshot applies and re-hashes correctly.

### BULK — transport configured, state streaming still open

- Fragmentation → reliability pipeline.
- Intended for region stream-in, late join, reconnect fallback, very large state, mip refinement.
- Must be rate-limited so EVENT/EPHEMERAL latency stays stable.
- Base terrain regenerates locally; only touched/current semantic state should transfer.

---

## Concrete authoritative runtime

```text
Unity frame loop
      |
      v
 UtpServerHost
      |
      +--> EVENT decode ----------> ServerCommandInbox
      |                              ServerConvergenceInbox
      |
      +--> EPHEMERAL decode ------> ServerCommandInbox
                                       |
                                fixed authoritative tick
                                       |
                         connection -> player registry
                                       |
                         rate/replay/reach/zone checks
                                       |
                         shared deterministic Core apply
                                       |
                         AuthoritativeEventStream
                                       |
                       3D interest + ordered batching
                                       |
                         queue mutation EVENT packets
                                       |
                         queue semantic hash barriers
                                       |
                         queue bounded REPAIR chunks
                                       |
                              one UTP send flush
```

`AuthoritativeServerSession` is the canonical server networking composition root. The old `ServerTickLoop` networking scaffold is obsolete and intentionally no longer performs packet handling/convergence.

### Server trust state

`ServerPlayerRegistry` owns:

- connection ID;
- authenticated player ID;
- authoritative voxel position;
- collision half-extents;
- reach;
- world-edit permission.

`ServerCommandProcessor` drains commands only at the fixed tick, validates client timestamps, deduplicates durable sequences, orders cross-player edits deterministically, substitutes authoritative tick/sequence/seed, validates against server state, applies the world change, and publishes only a successful real mutation.

`AlterationRateLimiter` uses server ticks. Rejected requests do not consume another player’s accepted budget.

---

## Deterministic world mutation

`Core/Edits/DeterministicAlterationApplier` is used by both authoritative server and client.

Current live canonical support:

- **explosion**: integer sphere, brick-batched writes, collapse once per touched brick, region commit once per touched brick;
- required-region residency preflight prevents partial application;
- unsupported brush/raw-batch semantics fail closed.

A fully covered uniform brick clears without pool allocation. Partial bricks materialize once, mutate their voxel materials, then collapse if uniform.

The client’s authoritative queue never lets a later batch leapfrog an earlier event waiting on region residency.

---

## Interest management

`SimulationInterest` + `RegionSubscriptionIndex` provide:

- common 300 m load / 420 m unload hysteresis for all hardware tiers;
- correct 512-voxel region edge;
- full X/Y/Z interest;
- correct negative-coordinate floor mapping;
- connection→regions and region→connections indexes;
- cross-region recipient union;
- at-most-once event fan-out per connection.

Presentation distance may vary by hardware. Simulation visibility may not.

---

## Drift detection

### Shared semantic hash

`Core/Storage/SemanticRegionHasher` hashes world semantics, not storage identity:

- region coordinate;
- hard-surface semantic bit per brick;
- uniform material, or all 512 mixed-brick material bytes.

`BrickPool` indices are excluded, so two peers with different allocator histories still hash equal when their world material state is equal.

### Ordered hash barrier

At a configured interval, the server:

1. queues all same-tick mutation EVENT packets;
2. captures an exact bounded semantic snapshot;
3. computes semantic hash;
4. queues `S_RegionHash(region,tick,hash)` on the same reliable EVENT stream;
5. flushes later.

The client stores alteration batches and hashes in one FIFO. Therefore a hash is compared only after every earlier mutation has applied.

### Mismatch report verification

On drift, the client sends `C_RegionHashMismatch(region,tick,clientHash,serverHash)` and pauses at that barrier.

The server only accepts it if:

- connection is authenticated;
- connection is still subscribed to the region;
- hashes actually differ;
- server previously issued exactly that hash to that connection;
- exact checkpoint snapshot still exists.

Mismatch traffic has its own bounded ingress: default 16 pending/connection, 256 total, with newer same-region reports replacing older ones.

---

## Exact semantic checkpoint repair

### Snapshot format

`SemanticRegionSnapshotCodec` uses semantic RLE across all region brick slots:

- uniform run = **5 B** (`tag + ushort run + material + hard flag`);
- mixed brick = **514 B** (`tag + hard flag + 512 materials`).

No pool index is encoded.

Before target mutation the client validates:

- complete snapshot syntax;
- exact region brick coverage;
- required mixed-brick capacity.

It then releases target mixed bricks, rebuilds semantic state, commits, recomputes the shared semantic hash, and resumes authority only if that hash equals the paused checkpoint hash.

### Bounds

Current defaults:

- max checkpoint snapshot per region: **256 KiB**;
- total retained server checkpoint bytes: **8 MiB**;
- checkpoint/hash retention: 90 ticks;
- hash interval: 30 ticks by default;
- live REPAIR packet: max **1,024 B**;
- live repair data/chunk: max **992 B**.

If a region snapshot cannot fit the per-region/global checkpoint budget, the server **skips the hash** rather than advertising a checkpoint it cannot repair.

Snapshots are shared across all clients for the same `(region,tick)` rather than retained per connection.

### Repair ordering

When a mismatch is found:

```text
EVENT ... mutations <= T
EVENT hash barrier T   <-- mismatch found, client pauses here
EVENT later authority  <-- remains queued

client -> EVENT mismatch report
server -> REPAIR exact semantic snapshot at T
client validates/applies/re-hashes
client unpauses
client applies queued EVENT authority > T
```

REPAIR callbacks only assemble bytes; region replacement occurs from the explicit client world-update path.

---

## Event history

`RegionEventLog` was corrected from the scaffold implementation:

- capacity remains 960 semantic events;
- modulo wrap rather than a broken power-of-two bitmask;
- tick stored per event;
- multiple events at one tick preserved;
- retained range copied in authority order;
- compaction boundary honored.

This enables future event-suffix-vs-snapshot cost selection without relying on the old one-event-per-tick index.

---

## Current implementation status

### Implemented

1. Versioned framing.
2. Compact durable alteration batches.
3. 34 B alteration request with connection-owned identity.
4. 16 B input samples + 51 B redundant EPHEMERAL bundles.
5. Concrete UTP 6.5 server/client lifecycle.
6. Dedicated EVENT/EPHEMERAL/REPAIR/BULK pipelines.
7. Bounded frame→tick gameplay and convergence inboxes.
8. Authenticated connection→player registry.
9. Server tick timestamp/replay/rate/reach/permission validation path.
10. Shared deterministic explosion application.
11. 3D platform-neutral interest routing.
12. Ordered client authority queue with residency deferral.
13. Shared allocator-independent semantic region hashing.
14. Tick-scoped ordered hash barriers.
15. Verified 26 B mismatch reporting.
16. Bounded exact-checkpoint semantic snapshot retention.
17. Chunked ≤1,024 B REPAIR transport.
18. Client repair assembly, semantic replacement, re-hash and authority resume.
19. Corrected multi-event/wrap-safe `RegionEventLog`.
20. Loopback/unit tests for the above are committed.

### Important: tests are not executed yet

The repository still has no GitHub Actions Unity test run for this branch. The new tests are source-level coverage only until Unity actually compiles/runs them.

### Still open

1. Canonical deterministic brush application.
2. Canonical raw-batch/RLE edit application.
3. BULK region stream-in and late join through `ClientNetworkRuntime` / `ServerNetworkRuntime`.
4. Reconnect/full-resync fallback when a mismatch checkpoint has expired or was never hashable under the snapshot cap.
5. Event-suffix vs snapshot repair cost selection.
6. `S_PlayerState` delta snapshots and player reconciliation using EPHEMERAL history.
7. Per-channel bytes/packets/queue-age/retransmit instrumentation and explicit BULK scheduler/back-pressure.
8. Mip/irradiance/derived-data rebuild scheduling after shared Core mutation/repair.
9. Remove/rename remaining legacy `InterestFilter`, `RepairDispatch`, `WorldHistory`, and old protocol scaffolds once no callers depend on them.
10. Adversarial packet-loss/jitter/late-join/reconnect/soak testing.

---

## Metrics to add before tuning

Per connection/channel:

- encoded and transport bytes/s;
- packets/s + fill ratio;
- reliable retransmit bytes/s;
- EVENT queue age p50/p95/p99;
- EPHEMERAL recovered/deduplicated samples;
- alteration batch size;
- bytes/accepted alteration;
- interest fan-out;
- hash packets, skipped checkpoints, mismatches;
- retained checkpoint bytes;
- repair queued/chunk bytes/completion latency;
- time spent paused at repair barriers;
- late-join bytes/time-to-playable.

Server CPU:

- command validation;
- deterministic mutation;
- interest routing/encoding;
- semantic hashing;
- snapshot encoding;
- repair scheduling;
- compaction.

---

## Milestones

### M0 — baseline/invariants — complete

Cause-not-effect plan + compact alteration codec.

### M1 — event-driven replication — complete foundation

Authoritative event stream, 3D subscriptions, ordered routing/batching, framing.

### M2 — concrete transport + trust boundary — complete foundation

UTP hosts, EPHEMERAL path, bounded inboxes, authenticated player state, fixed-tick command processor, real server/client composition roots.

### M3 — state convergence — foundation implemented

Shared semantic hash, ordered hash barriers, mismatch verification, exact-checkpoint semantic snapshot repair, corrected hot event history.

Remaining M3 work: event-suffix cost selection, checkpoint-expiry/full-resync fallback, reconnect/late join, player-state reconciliation.

### M4 — adversarial/soak

- four-player simultaneous chain-reaction destruction;
- heavy construction/raw edits once canonical;
- vertically separated players/regions;
- packet loss/jitter/reordering;
- dropped action-edge recovery;
- deliberate client drift and multi-chunk repair;
- expired checkpoint/full resync;
- late join during destruction;
- reconnect after long gap;
- BULK saturation while EVENT/EPHEMERAL latency remains stable;
- server tick spikes.

## Deliberate non-goals

- Replicating SDF samples, chunks, meshes, render geometry, or GPU buffers.
- Generic GameObject/RPC replication for voxel state.
- Sending ordinary per-voxel diffs for deterministic edits.
- Device-dependent gameplay interest radius.
- Raw `BrickRef`/`BrickPool` indices on the wire.
- Premature entropy coding before structural batching, interest filtering, convergence correctness and instrumentation are measured.
