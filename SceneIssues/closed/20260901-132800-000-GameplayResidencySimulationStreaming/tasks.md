# Gameplay residency / simulation streaming — tasks

**Plan:** [plan.md](plan.md)  
**Execution rule:** residency coordinates required fidelity only. Owning domains retain authoritative identity/state/rules; validation must exercise production paths rather than parallel substitutes.

## A. Baseline / ownership inventory

- [x] **R01 — Refresh from current master.** Inventoried current Characters, CharacterAI, WorldObjects, Encounters, Persistence/Application, GameplayReplication, WorldBuilder and VoxelEngine Streaming authorities before implementation.
- [x] **R02 — Inventory world streaming.** Proved physical residency needed a Streaming-owned ownership-safe lease because engine eviction could bypass service-level load/evict conventions.
- [x] **R03 — Inventory character lifecycle.** Reused stable `CharacterId` plus existing query/lifecycle/kinematics seams; residency never replaces identity.
- [x] **R04 — Inventory CharacterAI simulation LOD.** Identified the missing coarse semantic simulation seam without reusing detailed perception/navigation work.
- [x] **R05 — Inventory WorldObject streaming.** Kept `WorldObjectId` and changed object state under WorldObjects/persistence ownership.
- [x] **R06 — Inventory Encounter lifecycle.** Kept `EncounterId`, membership and lifecycle under Encounter ownership.
- [x] **R07 — Inventory replication interest.** Kept client replication/interest distinct from server simulation residency.
- [x] **R08 — Inventory persistence.** Kept residency demands/transitions transient; persistence stores owner semantic state only.
- [x] **R09 — Inventory WorldBuilder scopes.** Reused stable Region/Settlement/Site/Npc semantic refs rather than ordinals or captured scene coordinates.
- [x] **R10 — Resolve vocabulary/ownership.** `Dormant/Coarse/Detailed` is gameplay fidelity only; it does not alias loaded, visible, spawned or replicated.

## B. Residency API / deterministic coordinator

- [x] **R20 — Establish module boundary.** Added semantic `Game.Residency.Api` / `Runtime`; runtime consumes foreign APIs rather than foreign Runtime assemblies.
- [x] **R21 — Define fidelity.** Added ordered `Dormant`, `Coarse`, `Detailed` semantics.
- [x] **R22 — Define targets/scopes.** Targets use stable semantic IDs/regions and expose no `GameObject`, `Transform`, collider, renderer or packet handles.
- [x] **R23 — Define demand lease/token.** Independent requesters own and release only their own minimum-fidelity demand.
- [x] **R24 — Define demand aggregation.** Highest required fidelity wins with deterministic ordering.
- [x] **R25 — Define transition state.** Diagnostics expose desired/current fidelity, readiness and failure without mutable internals.
- [x] **R26 — Define semantic reasons.** Demand diagnostics retain requester/category/reason without campaign policy in shared runtime.
- [x] **R27 — Add core regressions.** Covered acquire/release order, overlapping and duplicate demands, requester cleanup, failed promotion and deterministic replay.

## C. Physical-world residency handshake

- [x] **R30 — Map Detailed to physical prerequisites.** Detailed spatial promotion acquires the public Streaming residency lease first.
- [x] **R31 — Preserve ownership during asynchronous readiness.** Detailed realization waits for physical readiness and never fabricates Streaming truth.
- [x] **R32 — Order demotion safely.** Detailed consumers quiesce before physical lease release, including coordinator teardown.
- [x] **R33 — Handle streaming failure/cancellation.** Failed/pending transitions remain at a valid lower fidelity and do not retry-storm until demand changes.
- [x] **R34 — Add world-handshake regression.** Public Streaming capability regression proves acquire/readiness/quiesce/release ordering without a parallel streamer.

## D. Characters / CharacterAI

- [x] **R40 — Character adapter.** Fidelity changes preserve the same `CharacterId`.
- [x] **R41 — Define Dormant character representation.** Dormant retains owner-required durable semantic state without detailed machinery.
- [x] **R42 — Define Coarse AI update.** Coarse AI advances semantic autonomous-life state without detailed perception/navigation execution.
- [x] **R43 — Define Detailed realization.** Detailed promotion restores authoritative placement through existing character kinematics and semantic coarse state.
- [x] **R44 — Preserve owner state.** Character owner facts survive fidelity transitions unless their owning domain legitimately mutates them.
- [x] **R45 — Interrupt/pin semantics.** Control, interaction/cutscene and encounter-style requesters can independently pin Detailed.
- [x] **R46 — Character cycle regression.** Dormant → Coarse → Detailed → Coarse → Dormant preserves one identity and expected semantic activity.
- [x] **R47 — Two-character scaling fixture.** Independent characters can occupy different fidelity levels; there is no global town-loaded switch.

## E. WorldObjects

- [x] **R50 — Reuse stable WorldObject identity.** Presentation realization does not own `WorldObjectId` lifetime.
- [x] **R51 — Reuse sparse retained state.** Existing WorldObject/persistence authorities retain changed state; residency duplicates none of it.
- [x] **R52 — Separate interaction availability from presentation.** Detailed interaction requires realized presentation while dormant semantic persistence remains valid.
- [x] **R53 — WorldObject reload regression.** Changed generated object state survives demote/unrealize/reload/promote with the same ID.
- [x] **R54 — Unchanged-object proof.** Unchanged generated definitions require no retained per-object runtime state merely for residency.

## F. Encounters / semantic pins

- [x] **R60 — Encounter residency adapter.** Encounter lifecycle contributes demands through the common coordinator.
- [x] **R61 — Prevent unsafe demotion.** Active/resolving participants remain at the encounter-required fidelity.
- [x] **R62 — Release after lifecycle completion.** Encounter cleanup releases only encounter-owned demand leases.
- [x] **R63 — Encounter pin regression.** Explicit encounter demand survives loss of proximity and releases independently afterward.

## G. Networking / presentation separation

- [x] **R70 — Keep server residency independent of client interest.** Server simulation fidelity is independent of whether a specific client receives updates.
- [x] **R71 — Reuse existing interest management.** No second replication graph was introduced.
- [x] **R72 — Snapshot-on-interest regression.** A later-relevant reader receives current semantic truth with the same identity rather than history replay.
- [x] **R73 — Presentation lifecycle adapter.** Presentation may realize/unrealize without becoming semantic lifetime authority.
- [x] **R74 — Audit client/server terms.** Residency APIs do not conflate Detailed with replicated/visible/Unity-object existence.

## H. Procedural WorldBuilder integration

- [x] **R80 — Sparse generated settlement fixture.** Production WorldBuilder authoring creates a 64-NPC semantic fixture while only a bounded subset is Detailed.
- [x] **R81 — Stable generated IDs.** Rebuilding the same semantic inputs yields the same IDs across unload/reload-style residency use.
- [x] **R82 — No Kentridge policy leakage.** Shared targeting contains no named Kentridge role/NPC/coordinate policy.
- [x] **R83 — Authored-anchor compatibility.** An important authored semantic NPC uses the same Detailed-demand mechanism as generated NPCs.

## I. Thrash, cost, and diagnostics

- [x] **R90 — Demonstrate boundary churn.** Boundary-crossing regressions exercise transition frequency.
- [x] **R91 — Add minimal hysteresis/dwell only if needed.** Semantic enter/exit bands suppress demonstrated oscillation while explicit pins remain immediate.
- [x] **R92 — Expose diagnostics.** Read-only diagnostics report fidelity counts, demands, transitions and physical wait/failure state.
- [x] **R93 — Measure representative cost.** The 64-NPC fixture records 48 Dormant / 12 Coarse / 4 Detailed with bounded transition work; repository 30 Hz simulation and ≤0.5 ms Streaming main-thread budgets were not weakened.

## J. Architecture / persistence / cleanup

- [x] **R100 — API/runtime audit.** Residency.Api has no Unity presentation/runtime leaks; Residency.Runtime depends on foreign APIs rather than foreign Runtime assemblies.
- [x] **R101 — Single-owner audit.** No duplicate resident-character, streamed-WorldObject, encounter-streaming or persistence authority remains.
- [x] **R102 — Persistence round trip.** Production `SessionPersistenceService` restores character and changed WorldObject semantic owner state after residency cycling with stable IDs and no transition replay.
- [x] **R103 — No quest/story streaming requirement.** Cheap non-spatial progression/story state is not forced into residency lifecycle.
- [x] **R104 — Remove obsolete bypasses.** No conflicting game-level load/unload authority remains in the changed path.

## K. Final reuse / validation / closure

- [x] **R110 — Primary reuse proof.** Character + CharacterAI + Streaming traverse all three fidelity levels through the coordinator.
- [x] **R111 — Independent reuse proof.** WorldObject consumes the same coordinator without character-specific policy.
- [x] **R112 — Explicit-demand proof.** Encounter/control-style demand composes with proximity and releases independently.
- [x] **R113 — Run focused deterministic/module tests.** Earlier exact request `1ca35bbb8f5d4a08cb69ad44488971e4937fc4aa` proved all 18 repository-selected EditMode assemblies plus focused Residency coverage; final request repeated the affected suite successfully.
- [x] **R114 — Run required exact-SHA integration/player validation.** Exact request `951785fa43f947c214b681634f19e37ae75f825e`, parent feature SHA `e20eeb2cc4796d64c0360bd971298a806e187dcf`, run `33937054327`: automatic module validation and mandatory SceneIssue standalone replay both succeeded.
- [x] **R114A — Resolve demonstrated mandatory-player Input-System blocker.** Kentridge reader uses the Input System from its owning `Game.Kentridge.PlayableSlice` assembly; final SceneIssue player log contains no legacy-input/`InvalidOperationException` failure and ends with `assertion failures 0`.
- [x] **R114B — Repair invalid module-local player scenarios.** Residency/Streaming scenarios use the shared harness-supported 10-second minimum.
- [x] **R114C — Reference Input System from the actual owning Kentridge scene-runtime assembly.** Dependency is on `Game.Kentridge.PlayableSlice`; unused parent reference removed.
- [x] **R115A — Make coordinator teardown obey the R32 pin-release invariant.** Teardown drives normal demotion, retains the pin on pending/failed quiescence, and has ordering/pending regressions.
- [x] **R115 — Inspect final blast radius.** Final diff preserves transition ordering, stable owner state, Streaming pin ownership, replication separation, generated-content scaling and repository numeric budgets; current-master-only changes are unrelated SceneIssue metadata.
- [x] **R116 — Close only with all acceptance proven.** Exact-SHA acceptance is green; resolution fields and direct `open/` → `closed/` bookkeeping are committed before required current-master integration and PR + auto-merge promotion.
