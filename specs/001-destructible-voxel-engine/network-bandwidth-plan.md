# Minimal Voxel Networking — Bandwidth Plan

**Status:** M0–M2 foundations implemented; M3 convergence/BULK + canonical cube brush + player reconciliation foundations implemented  
**Branch:** `feature/minimal-voxel-networking`  
**Extends:** `architecture-notes.md` and `contracts/wire-protocol.md`

## Goal

Make smooth/destructible multiplayer practical without replicating voxel effects, render meshes, SDF samples, GPU buffers, or ordinary per-voxel writes. Replication sends deterministic causes; state transfer sends semantic state, never allocator-local storage identity.

## Non-negotiable invariants

1. Server authority: packets are intent, never direct world mutation.
2. Connection-owned identity/position.
3. Fixed simulation clock; network callbacks only decode/copy into bounded ingress/protocol buffers.
4. Cause, not effect, for deterministic live mutations.
5. Shared deterministic Core application/hash semantics on server and client.
6. Platform-neutral 3D simulation interest before send/state request acceptance.
7. EPHEMERAL for supersedable input/player state; reliable channels for durable authority/state.
8. Normal events never partially apply because a neighboring region is absent.
9. Hashes and current-state fences are ordered EVENT barriers.
10. Repair/current-state snapshots contain semantic materials + flags, never `BrickPool` indices.
11. Late join/reconnect are state-based, not full-history replay.
12. BULK is bounded/throttled and must yield to live traffic.
13. One canonical shape definition drives request decoding, validation, interest routing, server application, client replay and bandwidth accounting.
14. Player prediction is game-owned; networking only carries authoritative rewind points and consumed-input acknowledgements.

---

## Traffic classes

### EPHEMERAL — implemented

- `UnreliableSequencedPipelineStage`.
- 16 B canonical input sample.
- normal redundant input bundle: max **51 B** (newest + two prior samples).
- at 30 Hz: about **1,530 B/s/client** custom input payload before transport headers.
- connection-scoped sequence validation/deduplication with ushort wrap support.
- absolute `S_PlayerState` snapshot = **40 B**.
- player-state bundle = `3 + 40N` B framed, `N ≤ 6`, max **243 B**.
- default player-state cadence = every two 30 Hz ticks = **15 Hz**.
- normal four-player state therefore fits in one EPHEMERAL datagram per recipient per snapshot cadence.

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
- encoded snapshot hash is verified before target storage mutation.
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
          game-owned player simulation
                         |
        processed-input acknowledgement
                         |
          validation + deterministic apply
                         |
            player-state EPHEMERAL bundles
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

Client receive path:

```text
UTP PumpTransport
    |
    +--> EVENT/REPAIR/BULK protocol queues
    |
    +--> EPHEMERAL player-state timeline
             |
      explicit game update
             |
      ApplyPlayerStateUpdates
             |
    local: rewind + replay unacked input
    remote: interpolate newest two snapshots
```

`AuthoritativeServerSession` is the canonical composition root. `ServerTickLoop` is obsolete networking scaffold.

---

## Trust / command path

`ServerPlayerRegistry` owns connection ID, player ID, authoritative voxel position/kinematics, collision volume, reach and edit permission.

`ServerCommandProcessor`:

- drains only at fixed tick;
- rejects implausible client ticks;
- deduplicates durable/input sequences;
- arbitrates by server-owned player/sequence order rather than packet arrival;
- advances the player input acknowledgement only after `IAuthoritativePlayerInputSink.ApplyInput` executes;
- derives authoritative seed/order/tick for durable world changes;
- validates reach/rate/permissions/zones/player volumes/density;
- applies the real shared deterministic mutation before publishing it.

Rejected alteration requests do not consume accepted rate/allocation budget.

---

## Player state / prediction reconciliation

`S_PlayerState` is an absolute server snapshot, never a delta from a previous packet:

- player ID;
- server tick;
- per-player snapshot sequence;
- last fixed-tick-consumed client input sequence;
- absolute Q19.13 voxel position;
- absolute Q12.20 voxel/second velocity;
- quantized yaw;
- grounded/teleport/respawn flags.

`ServerPlayerStateReplicator`:

- samples only after fixed-tick input processing;
- defaults to 15 Hz;
- uses the same 3D region subscription index as alteration fan-out;
- always includes the owning connection for reconciliation;
- bundles up to six player states into one ≤243 B EPHEMERAL datagram;
- keeps per-player ushort snapshot sequence independent from UTP packet sequence.

`ClientPredictionReconciler`:

- records only successful client input sends;
- keeps a bounded 256-sample history by default;
- compares input sequences with ushort modular ordering;
- on an authoritative local snapshot, discards inputs at-or-before the acknowledged sequence;
- calls game-owned prediction code to apply the authoritative rewind state;
- replays remaining unacknowledged inputs in original order.

`ClientPlayerStateTimeline`:

- rejects stale/reordered per-player state sequences;
- retains previous/current accepted snapshots;
- interpolates position/velocity and shortest-path yaw;
- snaps teleport/respawn updates instead of interpolating across discontinuities.

UTP receive callbacks do not run prediction code. `ClientNetworkRuntime.ApplyPlayerStateUpdates()` is the explicit mutation/reconciliation boundary.

---

## Deterministic world mutation

`Core/Edits/DeterministicAlterationApplier` is shared by authority and clients.

Current canonical support:

### Explosion

- integer sphere expansion;
- brick-batched destruction;
- full affected-region residency preflight;
- mixed bricks collapse when they become uniform.

### Axis-aligned cube brush

`BrushShapeCodec` owns the single live brush representation:

```text
shapeKind byte 0 : full X dimension in bricks
shapeKind byte 1 : full Y dimension in bricks
shapeKind byte 2 : full Z dimension in bricks
shapeKind byte 3 : shape type (1 = cube)
shapeData bit 0  : authored hard-surface semantic flag
```

- dimensions are full dimensions, not radii;
- dimensions are `1..64` bricks;
- arbitrary voxel origins are allowed, so a nominal one-brick cube may straddle two world bricks;
- exact inclusive voxel bounds drive validation, rate/allocation budget and interest routing;
- server/client use the same brick-batched fill/remove implementation;
- full-brick uniform writes remain `BrickRef.Uniform` and allocate no mixed slot;
- partial uniform writes materialize at most once and collapse once;
- semantic-only hard-surface changes are committed even when material bytes already match;
- every potentially affected region is resident before any mutation;
- current-state catch-up can apply pre-fence events everywhere except one region already replaced by an authoritative snapshot.

The previous brush shape union was invalid because extent Y and the shape discriminator overlapped. Compatibility request constructors canonicalize legacy X/Y/Z arguments before they can enter the live protocol.

Constructive cube placement attachment is checked over every voxel immediately outside the six faces rather than six face-center samples, so valid edge/corner support is not rejected.

Sphere/cylinder/extrude/rotated brushes and raw-batch/RLE edits still fail closed pending canonical shared semantics.

---

## Interest management

`SimulationInterest` + `RegionSubscriptionIndex` provide:

- 300 m load / 420 m unload hysteresis;
- 512-voxel region edge;
- full X/Y/Z interest including negative coordinates;
- connection→regions + region→connections indexes;
- cross-region recipient union/deduplication.

Explosion and cube-brush fan-out use canonical effect bounds. Cube brush routing no longer treats dimensions as radius padding, avoiding unnecessary adjacent-region sends near boundaries.

Remote player-state fan-out uses the same subscription index; owners are always included for reconciliation.

Full-state requests are accepted only for authenticated, currently subscribed regions.

---

## Semantic drift detection

`SemanticRegionHasher` includes region coordinate, hard-surface semantic bits, and materials. Mixed-brick pool indices are excluded.

Region checkpoint work is deterministically staggered across the configured interval (default 30 ticks), rather than serializing all interested regions on one frame.

Mismatch verification distinguishes:

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
3. verify encoded semantic hash before target mutation;
4. on explicit world-update path, atomically install the region and verify the live semantic hash;
5. replay queued authority through `T` in all other affected regions while excluding the replaced region;
6. supersede old hash barriers for the replaced region through `T`;
7. remain in catch-up mode even if the EVENT queue temporarily empties;
8. only the matching EVENT fence ends duplicate suppression;
9. resume newer authority normally.

This is the correctness foundation for expired checkpoint recovery, reconnect and late join.

---

## Bounded state ingress / memory

Gameplay, convergence, and region-state requests use separate bounded ingress because their costs/lifetimes differ.

Current state-transfer bounds:

- command input: bounded per connection/global inbox;
- local prediction history: 256 input samples by default, configurable up to 4096;
- pending client player-state notifications: newest state/player, max 64 distinct players;
- region request inbox: 8 pending/connection, 256 global;
- persistent manager deferred list: 256;
- one active current-state transfer/connection;
- current snapshot cap: 16 MiB;
- pending snapshot memory cap: 64 MiB;
- client assembler: one active transfer, max four completed snapshots / 32 MiB completed bytes.

No world serialization, voxel mutation, or local-player prediction rewind occurs from a UTP callback.

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
10. Canonical axis-aligned cube brush packing + deterministic server/client application.
11. Exact cube bounds for validation, budget and interest routing.
12. Hard-surface brush semantics and exact boundary attachment validation.
13. Ordered client authority FIFO + residency deferral.
14. Allocator-independent semantic hashing.
15. Staggered ordered hash barriers.
16. Verified mismatch reports with future/fabricated-report rejection.
17. Exact-checkpoint semantic REPAIR with pre-mutation snapshot hash verification.
18. Explicit full-resync-required signal.
19. Framed 18 B current region requests.
20. Bounded/throttled semantic BULK snapshot transfer.
21. 22 B EVENT snapshot fence and excluded-region catch-up semantics.
22. Automatic expired-checkpoint → current-state request escalation.
23. Corrected multi-event/wrap-safe `RegionEventLog`.
24. Absolute bundled `S_PlayerState` replication over EPHEMERAL.
25. Fixed-tick consumed-input acknowledgements.
26. Bounded local input history + rewind/replay reconciliation seam.
27. Remote two-snapshot interpolation timeline with stale-sequence rejection.
28. Unit/real-UTP loopback test source for transport, player reconciliation, convergence, current-state recovery and canonical cube brushes.

### Tests are still not executed

The test files are committed, but this branch still needs a real Unity compile/test run before any of them can be called passing.

### Still open

1. Canonical raw-batch/RLE application.
2. Additional deterministic brush shapes/rotation if gameplay actually needs them; do not re-enable the legacy encodings.
3. Progressive/base-seed + touched-overlay region streaming to reduce late-join bytes versus full semantic snapshots.
4. Multi-region reconnect/late-join orchestration and prioritization around spawn/player motion.
5. Integrate the game character controller with `UpdateAuthoritativePlayerKinematics` and `IClientPredictionAdapter`; networking now provides the seam but does not own movement physics.
6. Decide whether one-frame gameplay action edges such as jump/ability activation remain acceptable on redundant EPHEMERAL input or require a durable command lane.
7. Event-suffix vs state-snapshot repair cost selection.
8. Per-channel bytes/packets/queue age/retransmit instrumentation and connection-quality-aware BULK budgets.
9. Derived mip/mesh/irradiance rebuild scheduling after mutation/repair/state replacement.
10. Density-cap accounting should evolve from conservative touched-brick estimates to maintained per-region mixed-brick counts before construction is tuned aggressively.
11. Remove remaining legacy `InterestFilter`, `RepairDispatch`, `WorldHistory`, old immediate-apply client receiver and old protocol scaffolds after caller migration.
12. Adversarial loss/jitter/reconnect/late-join/BULK-saturation/heavy-construction/player-prediction soak tests.

---

## Metrics to add before tuning

Per connection/channel:

- encoded + transport bytes/s;
- packets/s and fill ratio;
- reliable retransmit bytes;
- EVENT queue age p50/p95/p99;
- EPHEMERAL recovered/duplicate input samples;
- player-state bundle size / states per bundle / stale snapshot count;
- input ack age and local replay count per reconciliation;
- prediction history drops;
- alteration batch distribution;
- brush touched-bricks vs mixed-growth distribution;
- bytes per accepted explosion/brush;
- interest fan-out by event/player state;
- hash/mismatch/repair rate;
- exact repair bytes/latency;
- full-state request/deferred/drop counts;
- BULK bytes/throttle yield/transfer latency;
- time client authority is paused/catching up;
- late-join bytes/time-to-playable.

Server CPU:

- command validation;
- player-state sampling/routing;
- brush attachment validation;
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

### M3 — state convergence + construction + player reconciliation — foundation implemented

Semantic hashes, exact-checkpoint repair, expired-history current-state BULK fallback, cross-pipeline EVENT fence/deterministic catch-up, canonical cube brush authority/replication, and absolute player snapshots with consumed-input rewind/replay.

Remaining M3: game-controller integration, optimized late-join/reconnect orchestration, event-suffix cost choice and raw-batch semantics.

### M4 — adversarial/soak

- simultaneous chain-reaction destruction;
- heavy canonical cube construction;
- heavy raw editing once canonical;
- horizontal + vertical player separation;
- packet loss/jitter/reordering;
- lost input edge recovery;
- local prediction correction under burst loss;
- remote snapshot reorder/interpolation;
- deliberate drift + multi-chunk repair;
- expired checkpoint full-state fallback;
- late join during destruction/construction;
- reconnect after long gap;
- BULK saturation while EVENT/EPHEMERAL latency stays stable;
- server tick spikes.

## Deliberate non-goals

- render/SDF/GPU replication;
- networking-owned character physics;
- generic GameObject/RPC voxel state;
- ordinary per-voxel diffs for deterministic effects;
- device-dependent gameplay interest;
- raw `BrickRef`/`BrickPool` identities on the wire;
- resurrecting ambiguous legacy brush packing;
- premature entropy coding before correctness/instrumentation measurements.
