# Tasks: Destructible & Buildable Multiplayer Voxel World

**Feature Directory**: `001-destructible-voxel-engine`
**Plan**: [plan.md](./plan.md)
**Created**: 2026-08-04
**Revised**: 2026-08-04 — analysis remediation (U1, G1, C1, G2, G3, G4, I1) and mobile scope narrowing

## Task Format

`- [ ] [TaskID] [P?] [Story?] Description with file path`

- `[P]` — parallelizable: different files, no dependency on incomplete work
- `[US#]` — user story label, required in user story phases only

## User Story Derivation

`spec.md` records acceptance scenarios rather than prioritised P1/P2/P3 stories, so they are mapped here. Priority is by dependency order and by how much risk each retires, not by user value alone.

| Story | Priority | Source | Milestone |
|---|---|---|---|
| **US1** — Destruction is shared and identical | P1 | Scenario 1 | M5 |
| **US2** — Unsupported structures collapse | P2 | Scenario 2 | M5 |
| **US3** — Building with visible provisional state | P3 | Scenario 3 | M5 |
| **US4** — Seamless kilometre-scale traversal | P4 | Scenarios 4, 5 | M6 |
| **US5** — Alterations persist and late join works | P5 | Scenario 6 | M7 |
| **US6** — Scale, crossplay, and moderation | P6 | Scenario 7 | M7/M8 |

**A caveat worth stating plainly**: this is an engine, so the foundational phase is unusually large and the user stories are *not* independently deliverable in the way this template normally assumes. Nothing observable ships until Phase 2 completes. US1 through US3 then become independently testable increments; US4 onward genuinely are separable. Do not interpret the phase structure as permission to attempt US1 before the foundation lands.

**Tests are included** because `plan.md` names a deterministic replay harness and a cross-device parity harness as core deliverables, and because SC-003 and SC-013 are architectural guarantees rather than features. The parity harness must exist from Phase 2, not be added later.

**All numeric targets** come from [device-matrix.md](./device-matrix.md), which is authoritative.

---

## Phase 1: Setup

- [X] T001 Create Unity project with URP at repository root, targeting PC, console, and high-end mobile build profiles
- [X] T002 [P] Declare package dependencies (`com.unity.burst`, `com.unity.collections`, `com.unity.jobs`, `com.unity.transport`) in `Packages/manifest.json`, explicitly excluding `com.unity.entities` and Netcode for GameObjects per R-001/R-002
- [X] T003 [P] Create assembly definition `Assets/VoxelEngine/Core/VoxelEngine.Core.asmdef` with no `UnityEngine` reference beyond Collections/Burst, enforcing the isolation the parity harness depends on
- [X] T004 [P] Create assembly definitions for Collision, Rendering, Net, Streaming, Tiering, Tools under `Assets/VoxelEngine/*/`
- [X] T005 Create the folder structure from `plan.md` under `Assets/VoxelEngine/` and `Assets/Tests/`
- [X] T006 [P] Add `.editorconfig` and an analyzer rule forbidding `float`/`double` in `Assets/VoxelEngine/Core/`, enforcing Constitution Principle I, at repository root
- [X] T007 [P] Add a CI check asserting `DeviceTierBudget` contains no simulation parameter, enforcing Constitution Principle IV, in `Assets/Tests/EditMode/ConstitutionGuardTests.cs`
- [ ] T008 Name the Mobile-HE reference device and record it in `specs/001-destructible-voxel-engine/device-matrix.md`
- [ ] T009 Build throwaway brickmap raymarch spike in `Assets/Spikes/M0Raymarch/` targeting the Mobile-HE reference device
- [ ] T010 [P] Build implicit/mip-only contingency raymarch alongside it in `Assets/Spikes/M0Implicit/`
- [ ] T011 Measure T009 and T010 against the M0 target table, recording results in `specs/001-destructible-voxel-engine/device-matrix.md`
- [ ] T012 Record M0 go/no-go against the ≤ 9 ms Mobile-HE threshold in `specs/001-destructible-voxel-engine/research.md` under R-004 — **gates Phase 2 rendering tasks**
- [ ] T013 [P] Configure headless server build and CI test invocation in `Server/build.config` and CI workflow
- [ ] T014 [P] Stand up the two-machine parity rig (differing GPU vendors, one Mobile-HE) with result comparison in `Assets/Tests/Parity/ParityRig.cs`
- [ ] T015 [P] Implement packet loss, latency, and jitter injection at the figures in `device-matrix.md`, in `Assets/Tests/Parity/NetworkConditions.cs`

---

## Phase 2: Foundational (blocking prerequisites)

**No user story can begin until this phase completes.** Six of the nine plan risks are retired here.

### Storage

- [X] T016 Implement `BrickPool` over a single `NativeArray<byte>` with parallel occupancy array in `Assets/VoxelEngine/Core/Storage/BrickPool.cs`
- [X] T017 Implement free-list allocation and release in `Assets/VoxelEngine/Core/Storage/BrickPool.cs`, guaranteeing `AllocateBrick` never fails
- [X] T018 [P] Implement zero-cost uniform-brick encoding in `Assets/VoxelEngine/Core/Storage/BrickRef.cs` — **design change**: uniform material is packed into the brick reference itself as `-(material+1)` rather than pointing at a shared palette brick array. Strictly better: no indirection, no palette storage, and the uniform/mixed test is a sign check
- [X] T019 Implement `Region` with brick pointer grid, residency state, and dirty flag in `Assets/VoxelEngine/Core/Storage/Region.cs`
- [X] T020 Implement sparse `RegionTable` over `NativeHashMap<int3, RegionHandle>` in `Assets/VoxelEngine/Core/Storage/RegionTable.cs`
- [X] T021 Implement `GetVoxel`/`SetVoxel` with two-indirection lookup in `Assets/VoxelEngine/Core/Storage/VoxelAccess.cs`
- [X] T022 Implement uniform-brick collapse on `SetVoxel` — brick becoming uniform must return its pool slot — in `Assets/VoxelEngine/Core/Storage/VoxelAccess.cs`
- [X] T023 [P] Write tests asserting empty and uniform bricks allocate zero pool slots, and that collapse returns slots, in `Assets/Tests/EditMode/StorageAllocationTests.cs`

### Occupancy

- [X] T024 [P] Implement 64-bit occupancy mask per brick in `Assets/VoxelEngine/Core/Occupancy/OccupancyMask.cs`
- [ ] T025 Implement mip hierarchy build as bitwise OR up the chain in `Assets/VoxelEngine/Core/Occupancy/MipBuilder.cs`
- [ ] T026 Implement per-frame batched mip rebuild over a dirty-brick set in `Assets/VoxelEngine/Core/Occupancy/MipBuilder.cs`
- [ ] T027 [P] Write tests asserting mip rebuild is incremental, never a full recompute, in `Assets/Tests/EditMode/MipBuilderTests.cs`

### Terrain and edits

- [ ] T028 [P] Implement seeded procedural terrain generation as a Burst job in `Assets/VoxelEngine/Core/Terrain/TerrainGenerator.cs`
- [ ] T029 [P] Write test asserting identical terrain from identical seed across platforms in `Assets/Tests/Parity/TerrainDeterminismTests.cs`
- [ ] T030 [P] Define `AlterationEvent` struct per `data-model.md` in `Assets/VoxelEngine/Core/Edits/AlterationEvent.cs`
- [ ] T031 Implement seeded integer PRNG in `Assets/VoxelEngine/Core/Edits/DeterministicRandom.cs`
- [ ] T032 Implement explosion expansion as a Burst job in `Assets/VoxelEngine/Core/Edits/ExplosionExpansion.cs`
- [ ] T033 [P] Implement brush expansion (cube, extrude, prefab) as a Burst job in `Assets/VoxelEngine/Core/Edits/BrushExpansion.cs`
- [ ] T034 [P] Implement run-length-encoded raw-batch expansion in `Assets/VoxelEngine/Core/Edits/RawBatchExpansion.cs`
- [ ] T035 Write tests asserting bit-identical expansion output for identical input in `Assets/Tests/EditMode/ExpansionDeterminismTests.cs`

### Determinism harness

- [ ] T036 Implement deterministic replay harness over seeded worlds and recorded event logs in `Assets/Tests/Parity/ReplayHarness.cs`
- [ ] T037 Write the SC-003 test: 10,000 alteration events replay to byte-identical state across two machines of differing hardware, in `Assets/Tests/Parity/TenThousandEventParityTests.cs`

### Growth bounding

- [ ] T038 Implement per-player voxel budget over a rolling window in `Assets/VoxelEngine/Core/Edits/AllocationBudget.cs` — **must be settled before the allocator is considered final (R-007)**
- [ ] T039 [P] Implement per-region density cap in `Assets/VoxelEngine/Core/Edits/DensityCap.cs`

### Network transport and protocol

- [ ] T040 Configure Unity Transport with three pipelines (EVENT, REPAIR, BULK) in `Assets/VoxelEngine/Net/Transport/ChannelSetup.cs`
- [ ] T041 Implement BULK rate limiting reserving the EVENT channel share from `device-matrix.md`, in `Assets/VoxelEngine/Net/Transport/BulkThrottle.cs`
- [ ] T042 [P] Implement `C_AlterationRequest` encode/decode in `Assets/VoxelEngine/Net/Protocol/AlterationRequest.cs`
- [ ] T043 [P] Implement `C_PlayerInput` encode/decode with quantisation in `Assets/VoxelEngine/Net/Protocol/PlayerInput.cs`
- [ ] T044 [P] Implement `C_RegionRequest` with `haveMipLevel` refinement field in `Assets/VoxelEngine/Net/Protocol/RegionRequest.cs`
- [ ] T045 [P] Implement `S_AlterationEvent` encode/decode in `Assets/VoxelEngine/Net/Protocol/AlterationEventMessage.cs`
- [ ] T046 [P] Implement `S_AlterationRejected` with the reason-code enum in `Assets/VoxelEngine/Net/Protocol/AlterationRejected.cs`
- [ ] T047 [P] Implement `S_RegionHash` and `S_RegionRepair` in `Assets/VoxelEngine/Net/Protocol/RegionSync.cs`
- [ ] T048 [P] Implement `S_RegionData` with seed plus compressed edit overlay in `Assets/VoxelEngine/Net/Protocol/RegionData.cs`
- [ ] T049 [P] Implement `S_PlayerState` delta encoding in `Assets/VoxelEngine/Net/Protocol/PlayerState.cs`
- [ ] T050 [P] Write encode/decode round-trip tests for all message types in `Assets/Tests/EditMode/ProtocolRoundTripTests.cs`

### Server spine

- [ ] T051 Implement authoritative server tick loop at 30 Hz in `Assets/VoxelEngine/Net/Server/ServerTickLoop.cs`
- [ ] T052 Implement validation predicate framework with a single choke point to `SetVoxel` in `Assets/VoxelEngine/Net/Server/Validation.cs`
- [ ] T053 Implement `RegionEventLog` ring buffer in `Assets/VoxelEngine/Net/Server/RegionEventLog.cs`
- [ ] T054 Implement `tickIndex` on the event log — **required from the first commit; retrofitting means rewriting the log and its consumers** — in `Assets/VoxelEngine/Net/Server/RegionEventLog.cs`
- [ ] T055 Implement `TryGetWorldStateAt(tick, regionCoord)` over the 500 ms rollback window in `Assets/VoxelEngine/Net/Server/WorldHistory.cs`
- [ ] T056 [P] Implement per-region state hashing for drift detection in `Assets/VoxelEngine/Net/Server/RegionHasher.cs`
- [ ] T057 Implement authoritative brick repair dispatch on hash mismatch in `Assets/VoxelEngine/Net/Server/RepairDispatch.cs`
- [ ] T058 Implement spatial interest management, shared by world and player replication, in `Assets/VoxelEngine/Net/Interest/InterestFilter.cs`

### Client spine

- [ ] T059 Implement client tick loop aligned to server ticks in `Assets/VoxelEngine/Net/Client/ClientTickLoop.cs`
- [ ] T060 Implement player input ring buffer with redundant send in `Assets/VoxelEngine/Net/Client/InputBuffer.cs`
- [ ] T061 Implement `SpeculativeOverlay` keyed by brick coordinate in `Assets/VoxelEngine/Net/Client/SpeculativeOverlay.cs`
- [ ] T062 Implement reconciliation replaying inputs against world state at each replayed tick via `TryGetWorldStateAt`, in `Assets/VoxelEngine/Net/Client/Reconciliation.cs`
- [ ] T063 Write test asserting reconciliation uses historical, not present, world state in `Assets/Tests/PlayMode/ReconciliationTests.cs`
- [ ] T064 Write the SC-016 test: two clients converge to identical state under the cellular loss/latency/jitter figures, in `Assets/Tests/Parity/LossConvergenceTests.cs`

### Collision

- [ ] T065 Implement shared DDA traversal used by both raycast and render raymarch in `Assets/VoxelEngine/Collision/DdaTraversal.cs`
- [ ] T066 Implement `Raycast` as a Burst job over the shared DDA in `Assets/VoxelEngine/Collision/VoxelRaycast.cs`
- [ ] T067 Implement `SweepAABB` character collision against occupancy masks in `Assets/VoxelEngine/Collision/SweptAabb.cs`
- [ ] T068 [P] Implement `ExportLocalHulls` bridging debris and vehicles to Unity physics in `Assets/VoxelEngine/Collision/HullExport.cs`
- [ ] T069 Write test asserting visual and collision representations agree, per C-004 and Constitution Principle II, in `Assets/Tests/PlayMode/VisualCollisionParityTests.cs`

### Rendering

*Gated on T012.*

- [ ] T070 Implement compute-shader brickmap raymarch in `Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute`
- [ ] T071 Implement mip-based empty-space skipping in `Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute`
- [ ] T072 Implement `ScriptableRendererFeature` writing depth and colour for URP compositing in `Assets/VoxelEngine/Rendering/RenderFeature/VoxelRenderFeature.cs`
- [ ] T073 Implement persistent recycled compute buffers with no per-frame allocation in `Assets/VoxelEngine/Rendering/RenderFeature/BufferManager.cs`
- [ ] T074 Implement `SubmitBrickUpdate` as a partial `ComputeBuffer.SetData` uploading one brick in `Assets/VoxelEngine/Rendering/RenderFeature/BrickUpload.cs`
- [ ] T075 [P] Implement implicit far-field raymarch over mip data in `Assets/VoxelEngine/Rendering/Shaders/ImplicitFarField.compute`
- [ ] T076 [P] Implement world-space irradiance probe cache with invalidation and multi-frame reconvergence in `Assets/VoxelEngine/Rendering/Irradiance/ProbeCache.cs`
- [ ] T077 [P] Define `DeviceTierBudget` type with `interestRadius`, tick rate, and collision parameters structurally absent, in `Assets/VoxelEngine/Tiering/DeviceTierBudget.cs`

---

## Phase 3: US1 — Destruction is shared and identical (P1)

**Goal**: two players observing the same wall see the same section removed and can move through the gap.

**Independent test**: two clients, one destroys, both render and collide identically.

- [ ] T078 [US1] Wire client destruction input to `C_AlterationRequest` submission in `Assets/VoxelEngine/Net/Client/DestructionInput.cs`
- [ ] T079 [US1] Implement server adjudication and `S_AlterationEvent` broadcast for destruction in `Assets/VoxelEngine/Net/Server/DestructionHandler.cs`
- [ ] T080 [US1] Apply broadcast events to the client brickmap via expansion jobs in `Assets/VoxelEngine/Net/Client/EventApplication.cs`
- [ ] T081 [P] [US1] Implement material palette with at least two classes of distinct destruction behaviour (FR-005) in `Assets/VoxelEngine/Core/Storage/MaterialPalette.cs`
- [ ] T082 [US1] Trigger mip rebuild and irradiance invalidation on applied destruction in `Assets/VoxelEngine/Net/Client/EventApplication.cs`
- [ ] T083 [US1] Write acceptance test for scenario 1 — identical geometry and traversability across two clients — in `Assets/Tests/Parity/SharedDestructionTests.cs`
- [ ] T084 [US1] Write the SC-002 test asserting a ≥ 4000-voxel event transmits in ≤ 64 bytes, in `Assets/Tests/EditMode/EventCostTests.cs`

---

## Phase 4: US2 — Unsupported structures collapse (P2)

**Goal**: destroying supports collapses what they held, identically for all observers.

**Independent test**: two clients observe the same collapse outcome, including across a region boundary with one side unloaded.

- [ ] T085 [P] [US2] Implement bitwise connectivity flood-fill over occupancy masks as a Burst job in `Assets/VoxelEngine/Core/Structure/Connectivity.cs`
- [ ] T086 [US2] Implement support-value propagation from anchored bricks with distance decrement in `Assets/VoxelEngine/Core/Structure/SupportField.cs`
- [ ] T087 [US2] Treat unloaded region borders as anchored in support propagation in `Assets/VoxelEngine/Core/Structure/SupportField.cs`
- [ ] T088 [US2] Implement collapse detection below support threshold in `Assets/VoxelEngine/Core/Structure/CollapseDetection.cs`
- [ ] T089 [US2] Implement `DebrisBody` with `visualOnly` flag distinguishing culled visual debris from state-changing debris, in `Assets/VoxelEngine/Rendering/Debris/DebrisBody.cs`
- [ ] T090 [US2] Implement debris settle-and-rebake into the grid in `Assets/VoxelEngine/Rendering/Debris/DebrisSettle.cs`
- [ ] T091 [P] [US2] Implement debris indirect draw via `RenderMeshIndirect` with per-instance transforms in `Assets/VoxelEngine/Rendering/Debris/DebrisRenderer.cs`
- [ ] T092 [US2] Implement server-side always-resident coarse structural graph for cross-region collapse in `Assets/VoxelEngine/Net/Server/StructuralGraph.cs`
- [ ] T093 [US2] Write the SC-008 test — collapse outcomes agree across clients including across region boundaries — in `Assets/Tests/Parity/CollapseAgreementTests.cs`

---

## Phase 5: US3 — Building with visible provisional state (P3)

**Goal**: placements appear immediately, are adjudicated, and rejections are explained.

**Independent test**: a permitted placement persists for all players; a forbidden one dissolves locally with a reason and never appears remotely.

- [ ] T094 [US3] Implement generative build brushes as the primary placement verb in `Assets/VoxelEngine/Core/Edits/BuildBrushes.cs`
- [ ] T095 [P] [US3] Implement ~100 ms coalescing of raw single-voxel placements into brick-scoped RLE batches in `Assets/VoxelEngine/Net/Client/PlacementCoalescer.cs`
- [ ] T096 [US3] Implement attachment predicate requiring new voxels touch existing structure, reusing connectivity data, in `Assets/VoxelEngine/Net/Server/Validation.cs`
- [ ] T097 [US3] Wire rate budget and density cap predicates into validation in `Assets/VoxelEngine/Net/Server/Validation.cs`
- [ ] T098 [US3] Implement total ordering of concurrent alterations by `(serverTick, playerId, sequence)` with material priority tie-break, satisfying FR-011 per R-010, in `Assets/VoxelEngine/Net/Server/ConflictArbitration.cs`
- [ ] T099 [US3] Implement client-side adoption of server arbitration order without re-derivation, in `Assets/VoxelEngine/Net/Client/EventApplication.cs`
- [ ] T100 [US3] Write the SC-017 test — competing alterations delivered in differing orders converge on the same winner — in `Assets/Tests/Parity/ConcurrentEditArbitrationTests.cs`
- [ ] T101 [US3] Implement player-occupied volume rejection predicate for placements, satisfying FR-032 per R-011, in `Assets/VoxelEngine/Net/Server/Validation.cs`
- [ ] T102 [US3] Write the SC-018 test — no player left intersecting solid matter, all observers agree — in `Assets/Tests/PlayMode/OccupiedVolumeTests.cs`
- [ ] T103 [US3] Implement speculative application of placements to the overlay in `Assets/VoxelEngine/Net/Client/SpeculativeOverlay.cs`
- [ ] T104 [US3] Implement visually distinct rendering of pending overlay voxels in `Assets/VoxelEngine/Rendering/Shaders/BrickRaymarch.compute`
- [ ] T105 [US3] Implement deterministic collision resolution against one side of a pending voxel, never a blend, in `Assets/VoxelEngine/Collision/SweptAabb.cs`
- [ ] T106 [US3] Implement confirm path promoting overlay voxels into the real grid in `Assets/VoxelEngine/Net/Client/SpeculativeOverlay.cs`
- [ ] T107 [US3] Implement reject path with dissolve animation and player-facing reason (FR-009) in `Assets/VoxelEngine/Net/Client/RejectionFeedback.cs`
- [ ] T108 [US3] Implement structural collapse of unsupported player-built material in `Assets/VoxelEngine/Core/Structure/CollapseDetection.cs`
- [ ] T109 [US3] Write acceptance test for scenario 3 in `Assets/Tests/PlayMode/BuildAdjudicationTests.cs`
- [ ] T110 [US3] Write the SC-007 test on rejection rate and 100% reason coverage in `Assets/Tests/PlayMode/RejectionFeedbackTests.cs`

---

## Phase 6: US4 — Seamless kilometre-scale traversal (P4)

**Goal**: continuous travel with no loading screens, and distant alterations visible in the silhouette.

**Independent test**: ten minutes of continuous maximum-speed traversal with no stall and flat memory.

- [ ] T111 [US4] Implement client region residency with distance-keyed LRU eviction in `Assets/VoxelEngine/Streaming/ResidencyManager.cs`
- [ ] T112 [US4] Implement per-tier load and unload radii from `device-matrix.md`, with a ≥ 25% hysteresis gap, in `Assets/VoxelEngine/Streaming/ResidencyManager.cs`
- [ ] T113 [US4] Implement prefetch along the movement vector, explicitly not view direction, in `Assets/VoxelEngine/Streaming/Prefetch.cs`
- [ ] T114 [US4] Implement worker-thread region population publishing via single pointer splice in `Assets/VoxelEngine/Streaming/RegionLoader.cs`
- [ ] T115 [US4] Implement per-frame cap on regions loaded holding main-thread streaming work under 0.5 ms, with mip approximation for fast arrival, in `Assets/VoxelEngine/Streaming/RegionLoader.cs`
- [ ] T116 [US4] Implement client eviction without write-back in `Assets/VoxelEngine/Streaming/ResidencyManager.cs`
- [ ] T117 [US4] Implement mip-level selection for far-field replication in `Assets/VoxelEngine/Net/Server/MipReplication.cs`
- [ ] T118 [US4] Implement progressive mip refinement honouring `haveMipLevel` rather than refetching in `Assets/VoxelEngine/Streaming/MipRefinement.cs`
- [ ] T119 [US4] Implement bandwidth-driven fidelity degradation demoting mip levels under constraint while preserving world state correctness (FR-029), in `Assets/VoxelEngine/Net/Client/AdaptiveFidelity.cs`
- [ ] T120 [US4] Implement server hot/warm/cold region tiers with dirty-flag write-back in `Assets/VoxelEngine/Net/Server/RegionResidency.cs`
- [ ] T121 [US4] Select and integrate the region key-value store backend (R-006) in `Server/Storage/RegionStore.cs`
- [ ] T122 [US4] Write the SC-004 test — continuous traversal, no loading screen, no frame exceeding the tier budget attributable to streaming — in `Assets/Tests/PlayMode/TraversalStreamingTests.cs`
- [ ] T123 [US4] Write the SC-005 test asserting world-attributable memory stays within tier budget and flat within ±2% over two hours, in `Assets/Tests/PlayMode/MemoryStabilityTests.cs`
- [ ] T124 [US4] Write the SC-006 test asserting silhouette-changing alterations are visible at maximum view distance in `Assets/Tests/PlayMode/DistantAlterationTests.cs`

---

## Phase 7: US5 — Alterations persist and late join works (P5)

**Goal**: returning to a region shows your changes; joining mid-session is fast regardless of how altered the world is.

**Independent test**: alter, leave, return, verify. Join a heavily altered world and measure time to playable.

- [ ] T125 [US5] Implement event-log compaction into baked brick snapshots beyond the 2 s hot retention window in `Assets/VoxelEngine/Net/Server/LogCompaction.cs`
- [ ] T126 [US5] Implement late-join flow shipping top-level mips then BULK refinement, never replaying history, in `Assets/VoxelEngine/Net/Server/LateJoin.cs`
- [ ] T127 [US5] Implement reconnect flow selecting repair versus full region data by cost in `Assets/VoxelEngine/Net/Server/Reconnect.cs`
- [ ] T128 [US5] Implement session-end discard of all alterations (FR-031) in `Assets/VoxelEngine/Net/Server/SessionLifecycle.cs`
- [ ] T129 [US5] Write acceptance test for scenario 6 — alterations present on return — in `Assets/Tests/PlayMode/PersistenceTests.cs`
- [ ] T130 [US5] Write the SC-009 test on time-to-playable for late join into a heavily altered world in `Assets/Tests/PlayMode/LateJoinTests.cs`
- [ ] T131 [US5] Write the SC-010 test asserting sub-linear storage growth against cumulative alterations in `Assets/Tests/PlayMode/StorageGrowthTests.cs`

---

## Phase 8: US6 — Scale, crossplay, and moderation (P6)

**Goal**: 64 players across all three device classes, with griefing bounded and outcomes identical.

**Independent test**: soak at target player count across the device matrix, with the moderation predicates exercised.

- [ ] T132 [P] [US6] Implement protected zone masks and their validation predicate in `Assets/VoxelEngine/Net/Server/ProtectedZones.cs`
- [ ] T133 [P] [US6] Implement plausibility rejection for out-of-reach and unperceived-region alterations in `Assets/VoxelEngine/Net/Server/Validation.cs`
- [ ] T134 [US6] Implement owner attribution on player-placed voxels in `Assets/VoxelEngine/Core/Storage/Attribution.cs`
- [ ] T135 [US6] Implement operator-facing region alteration history query in `Assets/VoxelEngine/Net/Server/ModerationQuery.cs`
- [ ] T136 [US6] Implement device class detection and budget resolution against `device-matrix.md` in `Assets/VoxelEngine/Tiering/DeviceClassResolver.cs`
- [ ] T137 [US6] Wire tier budgets to brick pool capacity, detail radius, render scale, step budget, probe spacing, and visual-only debris cap in `Assets/VoxelEngine/Tiering/BudgetApplication.cs`
- [ ] T138 [US6] Write the C-006 tiering test matrix asserting no simulation parameter varies by device class in `Assets/Tests/Parity/TieringMatrixTests.cs`
- [ ] T139 [US6] Write the SC-013 test — identical outcomes for Mobile-HE and PC tiers — in `Assets/Tests/Parity/CrossDeviceOutcomeTests.cs`
- [ ] T140 [US6] Implement and validate a genuinely mixed-platform session with PC, console, and Mobile-HE clients in one instance (FR-026), in `Assets/Tests/Parity/MixedPlatformSessionTests.cs`
- [ ] T141 [US6] Write the SC-011 test asserting no legal message sequence bypasses FR-018 through FR-021 in `Assets/Tests/PlayMode/AuthorityBoundaryTests.cs`
- [ ] T142 [US6] Build the soak harness for 64 simulated players under sustained destruction in `Server/Soak/SoakHarness.cs`
- [ ] T143 [US6] Write the SC-001 test asserting ≤ 150 ms p95 world-update latency and ≤ 10% update-rate spread at target player count, in `Assets/Tests/PlayMode/ScaleResponsivenessTests.cs`
- [ ] T144 [US6] Write the SC-014 test asserting sustained and peak bandwidth budgets hold for every participant at scale in `Assets/Tests/PlayMode/BandwidthBudgetTests.cs`
- [ ] T145 [US6] Write the SC-015 test asserting Mobile-HE hits its memory and frame budgets across the km-scale world in `Assets/Tests/PlayMode/LowTierBudgetTests.cs`
- [ ] T146 [US6] Validate no thermal throttle over a 20-minute Mobile-HE session in `Assets/Tests/PlayMode/ThermalSustainTests.cs`

---

## Final Phase: Polish & Cross-Cutting

- [ ] T147 [P] Build the brush editor tool for authoring test worlds in `Assets/VoxelEngine/Tools/BrushEditor.cs`
- [ ] T148 [P] Implement deterministic region serialisation for reproducible test worlds in `Assets/VoxelEngine/Tools/RegionSerialiser.cs`
- [ ] T149 [P] Implement session replay from recorded event logs in `Assets/VoxelEngine/Tools/SessionReplay.cs`
- [ ] T150 [P] Add profiling counters for pool occupancy, resident regions, and per-channel bandwidth in `Assets/VoxelEngine/Tools/Diagnostics.cs`
- [ ] T151 Write the SC-012 playtest protocol for visual/collision agreement in `specs/001-destructible-voxel-engine/playtest-protocol.md`
- [ ] T152 Verify console certification and mobile store constraints against the transport implementation, recording findings in `specs/001-destructible-voxel-engine/research.md`

---

## Dependencies

```text
Phase 1 (Setup) ────────────────────────────┐
   T008 → T009/T010 → T011 → T012 ──────────┼──> gates T070–T077 (rendering)
   T006, T007 (constitution guards) ────────┼──> active from first commit
                                            │
Phase 2 (Foundational) ─────────────────────┘
   Storage (T016–T023) ──> Occupancy (T024–T027) ──> Edits (T030–T035)
                                            │
                                            ├──> Determinism harness (T036–T037)
                                            ├──> Collision (T065–T069)
                                            ├──> Rendering (T070–T077)
                                            └──> Net (T040–T064)
                                                    T053 → T054 → T055 → T062
                                            │
   ┌────────────────────────────────────────┘
   ▼
Phase 3 (US1) ──> Phase 4 (US2) ──> Phase 5 (US3)
                        │
                        └──> Phase 6 (US4) ──> Phase 7 (US5) ──> Phase 8 (US6)
                                                                      │
                                                                      ▼
                                                              Final Phase
```

**Critical path**: T009 → T011 → T012 → T070 → T078 → T085 → T111 → T142.

**Hard sequencing constraints** (violating any means rework, not delay):

- T054 (`tickIndex`) before T062 (reconciliation). Adding the index later means rewriting the log and everything reading it.
- T012 (M0 go/no-go) before any Phase 2 rendering task.
- T038 (allocation budget) before the pool allocator is considered final. Growth policy shapes the allocator.
- T022 (uniform collapse) before any long-session test. Without it memory leaks in a way only long soaks reveal.
- T014 (parity rig) before T037. The harness must exist alongside the code it validates.
- T006 and T007 (constitution guards) from the first commit. Retrofitting an analyzer rule onto existing code means fixing every violation at once.

**Story independence**: US1 through US3 all land in M5 and share the structural machinery, so they are sequential rather than parallel. US4 onward are genuinely separable and could proceed in parallel with US2/US3 given separate developers.

---

## Parallel Execution Examples

**Phase 1** — after T001: T002, T003, T004, T006, T007, T013, T014, T015 all run in parallel. T008 unblocks the M0 spike chain.

**Phase 2** — three independent tracks once storage (T016–T023) lands:

- *Track A (simulation)*: T024–T039
- *Track B (networking)*: T040–T064
- *Track C (rendering + collision)*: T065–T077, gated on T012

Within Track B, all protocol tasks T042–T049 are parallel.

**Phase 4** — T085 and T091 parallel; T092 parallel with the debris chain.

**Phase 8** — T132 and T133 parallel; test tasks T138–T141 and T143–T146 all parallel once T136/T137 land.

**Final Phase** — T147–T150 fully parallel.

---

## Implementation Strategy

**M0 is no longer the blocking risk it was.** Narrowing to high-end mobile turned T009–T012 from an open question with no fallback into an expected-pass measurement with a defined threshold (≤ 9 ms at 0.75 render scale). Still do it first — a measurement that fails is far cheaper now than after M4 — but it need not hold up Phase 2 storage work, which has no dependency on it.

**The MVP is Phase 1 + Phase 2 + Phase 3 (US1)**: two players in a small world, destroying the same wall and seeing identical results. That is 84 tasks and it is a lot for an MVP — an honest consequence of building an engine rather than a feature. It nonetheless proves the four central claims at once: cheap edits, deterministic replication, shared state, and unified visual/collision representation. If US1 works at two players in one region, the architecture is sound and everything after is extension.

**Do not compress Phase 2.** Six of the nine plan risks are retired there, and the two most expensive retrofits in the project — the event log's tick index and the uniform-brick collapse invariant — are single tasks inside it that are cheap now and structural later.

**Reconsider Netick at the T062 boundary.** If the custom tick loop and reconciliation are running behind, R-001 recorded Netick as a viable fallback with a passing evaluation against the rollback criterion. That is the moment to make the call, not later.

**Keep the parity harness green continuously.** T037 (SC-003) and T139 (SC-013) are the architecture's central guarantees expressed as tests, and Constitution Principles I and IV in executable form. If either starts drifting, stop feature work and find out why — silent cross-hardware divergence is the failure mode this design is most exposed to, and it does not announce itself.
