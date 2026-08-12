# Minimal Voxel Networking — Bandwidth Plan

**Status:** M0–M2 foundations implemented; M3 semantic convergence + current-state BULK foundation implemented  
**Branch:** `feature/minimal-voxel-networking`  
**Extends:** `architecture-notes.md` and `contracts/wire-protocol.md`

## Goal

Make smooth/destructible multiplayer practical without replicating voxel effects, render meshes, SDF samples, GPU buffers, or ordinary per-voxel writes. Replication sends deterministic causes; state transfer sends semantic state, never allocator-local storage identity.

## Non-negotiable invariants

1. Server authority: packets are intent, never direct world mutation.
2. Connection-owned identity/position.
3. Fixed simulation clock; network callbacks only decode/copy into bounded ingress.
4. Cause, not effect, for deterministic live mutations.
5. Shared deterministic Core application/hash semantics on server and client.
6. Platform-neutral 3D simulation interest before send/state request acceptance.
7. EPHEMERAL for supersedable input; reliable channels for durable authority/state.
8. Normal events never partially apply because a neighboring region is absent.
9. Hashes and current-state fences are ordered EVENT barriers.
10. Repair/current-state snapshots contain semantic materials + flags, never `BrickPool` indices.
11. Late join/reconnect are state-based, not full-history replay.
12. BULK is bounded/throttled and must yield to live traffic.

---

## Traffic classes

### EPHEMERAL — implemented

- `UnreliableSequencedPipelineStage`.
- 16 B canonical input sample.
- normal redundant bundle: max **51 B** (newest + two prior samples).
- at 30 Hz: about **1,530 B/s/client** custom payload before transport headers.
- connection-scoped sequence validation/deduplication with ushort wrap support.

### EVENT — implemented foundation

- reliable ordered pipeline.
- alteration request: **34 B framed**.
- alteration batch: max **1,172 B framed / 48 events**.
- alteration rejection: **10 B framed**.
- region hash barrier: **22 B framed**.
- mismatch report: **26 B framed**.
- expired-repair/full-state signal: **19 B framed**.
- current-state cross-pipeline fence: **22 B framed**.

### REPAIR — exact-checkpoint semantic recovery implemented

- reliable, non-fragmented.
- max **1,024 B packet / 992 B semantic chunk**.
- exact semantic checkpoint retention only for advertised hashes.
- client pauses at the mismatched hash, applies/re-hashes exact checkpoint state, then resumes later authority.

### BULK — current semantic region state implemented foundation

- fragmentation → reliability.
- live `S_RegionData` is `RegionStateChunkPacket`, not the old allocator-local scaffold.
- 36 B header + up to **16,348 B** semantic payload = **16 KiB max packet**.
- current full semantic snapshot cap: **16 MiB/region**.
- one current-state transfer/connection at a time.
- max 256 persistent deferred requests.
- max 64 MiB pending snapshot bytes server-wide.
- one BULK packet/connection/tick plus global packet cap.
- per-connection rolling `BulkThrottle` currently uses wired/Wi-Fi default budget.

The current-state format prioritizes correctness. Future late-join optimization should regenerate procedural base terrain from a seed and send only compacted touched overlay state when that is cheaper than the complete semantic snapshot.

---

## Canonical runtime

```text
Unity frame pump
    |
    +--> EVENT ------> ServerCommandInbox
    |                  ServerConvergenceInbox
    |                  ServerRegionStateRequestInbox
    |
    +--> EPHEMERAL --> ServerCommandInbox
                         |
                   fixed server tick
                         |
             authenticated player state
                         |
          validation + deterministic apply
                         |
               authoritative events
                         |
            3D interest + batching
                         |
            mutation EVENT packets
                         |
          semantic hash EVENT barriers
                         |
      region request capture + EVENT fence
                         |
           bounded REPAIR + BULK sends
                         |
                  one UTP flush
```

`AuthoritativeServerSession` is the canonical composition root. `ServerTickLoop` is obsolete networking scaffold.

---

## Trust / command path

`ServerPlayerRegistry` owns connection ID, player ID, authoritative voxel position, collision volume, reach and edit permission.

`ServerCommandProcessor`:

- drains only at fixed tick;
- rejects implausible client ticks;
- deduplicates durable sequences;
- arbitrates by server-owned player/sequence order rather than packet arrival;
- derives authoritative seed/order/tick;
- validates reach/rate/permissions/zones/player volumes/density;
- applies the real shared deterministic mutation before publishing it.

Rejected requests do not consume accepted rate/allocation budget.

---

## Deterministic world mutation

`Core/Edits/DeterministicAlterationApplier` is shared by authority and clients.

Current canonical support:

- explosion: integer sphere + brick-batched writes;
- full affected-region residency preflight;
- collapse mixed bricks when they become uniform;
- current-state catch-up variant applies old events everywhere except one region already replaced by an authoritative snapshot.

Brush/raw-batch still fail closed pending canonical shared semantics.

---

## Interest management

`SimulationInterest` + `RegionSubscriptionIndex` provide:

- 300 m load / 420 m unload hysteresis;
- 512-voxel region edge;
- full X/Y/Z interest including negative coordinates;
- connection→regions + region→connections indexes;
- cross-region recipient union/deduplication.

Full-state requests are accepted only for authenticated, currently subscribed regions.

---

## Semantic drift detection

`SemanticRegionHasher` includes region coordinate, hard-surface semantic bits, and materials. Mixed-brick pool indices are excluded.

Region checkpoint work is deterministically staggered across the configured interval (default 30 ticks), rather than serializing all interested regions on one frame.

Mismatch verification now distinguishes:

- future tick → reject;
- never-issued checkpoint still inside retention → reject;
- exact issued retained checkpoint → exact REPAIR;
- history legitimately older than retention → current-state BULK escalation.

This prevents an authenticated client from manufacturing an in-window hash report just to force an expensive full-region state transfer.

---

## Exact checkpoint REPAIR

Semantic snapshot RLE:

- uniform run = **5 B**;
- mixed brick = **514 B**;
- hard-surface semantic bit included.

Current exact-repair bounds:

- 256 KiB max checkpoint/region;
- 8 MiB retained checkpoint bytes/server;
- 90-tick retention;
- max two REPAIR chunks queued per authoritative tick by default.

If a checkpoint cannot be retained under the configured bounds, its hash is skipped.

---

## Current-state BULK recovery

### Why an EVENT fence is required

A snapshot captured at tick `T` already contains effects of events through `T`, but those EVENT packets may still be queued/unapplied on the client. EVENT and BULK are separate reliable pipelines, so network arrival order cannot establish which facts the snapshot includes.

The server therefore queues:

```text
EVENT mutation(s) through T
EVENT due hash barrier(s)
EVENT RegionStateFence(transferId, region, T)
BULK semantic region snapshot at T
```

BULK may arrive before or after the fence. Correctness does not depend on cross-pipeline delivery order.

### Client flow

1. pause EVENT application before requesting current region state;
2. assemble BULK bytes without world mutation;
3. on explicit world-update path, atomically install the region and verify semantic hash;
4. replay queued authority through `T` in all other affected regions while excluding the replaced region;
5. supersede old hash barriers for the replaced region through `T`;
6. remain in catch-up mode even if the EVENT queue temporarily empties;
7. only the matching EVENT fence ends duplicate suppression;
8. resume newer authority normally.

This is the correctness foundation for expired checkpoint recovery, reconnect and late join.

---

## Bounded state ingress / memory

Gameplay, convergence, and region-state requests use separate bounded ingress because their costs/lifetimes differ.

Current state-transfer bounds:

- region request inbox: 8 pending/connection, 256 global;
- persistent manager deferred list: 256;
- one active current-state transfer/connection;
- current snapshot cap: 16 MiB;
- pending snapshot memory cap: 64 MiB;
- client assembler: one active transfer, max four completed snapshots / 32 MiB completed bytes.

No world serialization occurs from a UTP callback.

---

## Current implementation status

### Implemented

1. Versioned framing and stable message registry.
2. Compact cause-based alteration replication.
3. Concrete Unity Transport server/client lifecycle.
4. Separate EVENT / EPHEMERAL / REPAIR / BULK pipelines.
5. 16 B input + redundant unreliable bundles.
6. Bounded command ingress and connection-owned identity.
7. Fixed-tick authoritative validation/application/publish pipeline.
8. Full 3D simulation interest/inverse subscriber index.
9. Shared deterministic explosion application.
10. Ordered client authority FIFO + residency deferral.
11. Allocator-independent semantic hashing.
12. Staggered ordered hash barriers.
13. Verified mismatch reports with future/fabricated-report rejection.
14. Exact-checkpoint semantic REPAIR.
15. Explicit full-resync-required signal.
16. Framed 18 B current region requests.
17. Bounded/throttled semantic BULK snapshot transfer.
18. 22 B EVENT snapshot fence and excluded-region catch-up semantics.
19. Automatic expired-checkpoint → current-state request escalation.
20. Corrected multi-event/wrap-safe `RegionEventLog`.
21. Unit/loopback test source for codec, catch-up and full current-state recovery.

### Tests are still not executed

The test files are committed, but this branch still needs a real Unity compile/test run before any of them can be called passing.

### Still open

1. Canonical deterministic brush application.
2. Canonical raw-batch/RLE application.
3. Progressive/base-seed + touched-overlay region streaming to reduce late-join bytes versus full semantic snapshots.
4. Multi-region reconnect/late-join orchestration and prioritization around spawn/player motion.
5. `S_PlayerState` snapshots + prediction/reconciliation using EPHEMERAL history.
6. Event-suffix vs state-snapshot repair cost selection.
7. Per-channel bytes/packets/queue age/retransmit instrumentation and connection-quality-aware BULK budgets.
8. Derived mip/mesh/irradiance rebuild scheduling after mutation/repair/state replacement.
9. Remove remaining legacy `InterestFilter`, `RepairDispatch`, `WorldHistory`, and old protocol scaffolds after caller migration.
10. Adversarial loss/jitter/reconnect/late-join/BULK-saturation soak tests.

---

## Metrics to add before tuning

Per connection/channel:

- encoded + transport bytes/s;
- packets/s and fill ratio;
- reliable retransmit bytes;
- EVENT queue age p50/p95/p99;
- EPHEMERAL recovered/duplicate samples;
- alteration batch distribution;
- hash/mismatch/repair rate;
- exact repair bytes/latency;
- full-state request/deferred/drop counts;
- BULK bytes/throttle yield/transfer latency;
- time client authority is paused/catching up;
- late-join bytes/time-to-playable.

Server CPU:

- command validation;
- deterministic mutation;
- interest routing;
- semantic hash/snapshot encoding;
- REPAIR scheduling;
- BULK serialization/scheduling.

---

## Milestones

### M0 — baseline/invariants — complete

Cause-not-effect networking model and compact codec.

### M1 — event-driven replication — complete foundation

Authoritative event stream, 3D interest, ordered routing/batching/framing.

### M2 — concrete transport + trust boundary — complete foundation

UTP hosts, EPHEMERAL traffic, bounded ingress, authenticated player registry, fixed-tick command processor.

### M3 — state convergence — foundation implemented

Semantic hashes, exact-checkpoint repair, expired-history current-state BULK fallback, cross-pipeline EVENT fence and deterministic catch-up.

Remaining M3: optimized late-join/reconnect orchestration, event-suffix cost choice, player-state reconciliation.

### M4 — adversarial/soak

- simultaneous chain-reaction destruction;
- heavy construction/raw editing once canonical;
- horizontal + vertical player separation;
- packet loss/jitter/reordering;
- lost input edge recovery;
- deliberate drift + multi-chunk repair;
- expired checkpoint full-state fallback;
- late join during destruction;
- reconnect after long gap;
- BULK saturation while EVENT/EPHEMERAL latency stays stable;
- server tick spikes.

## Deliberate non-goals

- render/SDF/GPU replication;
- generic GameObject/RPC voxel state;
- ordinary per-voxel diffs for deterministic effects;
- device-dependent gameplay interest;
- raw `BrickRef`/`BrickPool` identities on the wire;
- premature entropy coding before correctness/instrumentation measurements.
