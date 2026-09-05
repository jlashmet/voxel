# Gameplay residency / simulation streaming — tasks

**Plan:** [plan.md](plan.md)  
**Execution rule:** residency coordinates required fidelity only. Owning domains keep authoritative identity/state/rules. Prefer existing APIs and adapters over new parallel runtimes.

## A. Baseline / ownership inventory

- [x] **R01 — Refresh from current master.** Baseline `ed5c6f908361228819b3368bcd8427d4b44d89e3`; Characters, CharacterAI, WorldObjects, Encounters, Persistence/Application, GameplayReplication, WorldBuilder and VoxelEngine Streaming APIs are present.
- [x] **R02 — Inventory world streaming.** `IRegionStreaming` owns queue/publish/read-resident/evict around stable `int3` region + seed/mip requests. It lacked ownership-safe pins, while runtime eviction can bypass service-level `Evict`; required Streaming-owned lease primitive is now the selected fix.
- [x] **R03 — Inventory character lifecycle.** `CharacterId` is stable; `ICharacterRegistry` owns create/remove/bind/kinematics and exposes read-only `ICharacterQuery`. Residency must never replace/remove an identity merely to change fidelity.
- [x] **R04 — Inventory CharacterAI simulation LOD.** Existing AI has enabled/Autonomous/Tactical modes and detailed Observe→Policy→Execute ticking, but no coarse semantic simulation seam. Residency will adapt a new narrow AI fidelity seam rather than retain detailed perception/navigation for Coarse.
- [x] **R05 — Inventory WorldObject streaming.** `WorldObjectId`, behavior snapshots, registry Capture/Restore and existing persistence own semantic state. Residency may realize/unrealize presentation but cannot store door/chest/lever state.
- [x] **R06 — Inventory Encounter lifecycle.** Stable `EncounterId`, participants, lifecycle and Capture/Restore are encounter-owned. Active encounter/site/participant policy can request residency but remains outside the coordinator.
- [x] **R07 — Inventory replication interest.** GameplayReplication publishes/reconstructs current semantic snapshots/deltas and synchronization state; it exposes no simulation-residency lifetime ownership. Residency will not equate Detailed with per-client replicated/visible.
- [x] **R08 — Inventory persistence.** Persistence contributors serialize authoritative semantic sections; schema guard explicitly excludes Unity/presentation/transport and AI scratch. Residency demand/transition state is transient policy and is rebuilt after restore rather than persisted as duplicate owner state.
- [x] **R09 — Inventory WorldBuilder scopes.** WorldBuilder exposes stable Region/Settlement/Site/Npc refs behind authoring handles. Residency targeting can use their stable semantic IDs without Kentridge roles, scene coordinates or ordinals.
- [x] **R10 — Resolve vocabulary/ownership.** `resident` remains VoxelEngine physical residency; `Active/Resolved` remains Encounter lifecycle; AI `Enabled/Autonomous/Tactical` remains controller state; replication `Synchronized/GameplayReady` remains client sync; gameplay fidelity uses only `Dormant/Coarse/Detailed` and does not alias loaded/visible/spawned/replicated.

## B. Residency API / deterministic coordinator

- [x] **R20 — Establish module boundary.** Create/reuse a semantic game-level residency Api/Runtime boundary only if the inventory demonstrates no existing owner. Runtime may depend on foreign APIs, never foreign Runtime assemblies.
- [x] **R21 — Define fidelity.** Add/reuse semantic `Dormant`, `Coarse`, `Detailed` levels with documented meaning and ordering.
- [x] **R22 — Define targets/scopes.** Address spatial scopes and, where needed, specific semantic entities using stable IDs; no `GameObject`, `Transform`, collider, renderer, packet, or concrete engine Runtime types.
- [x] **R23 — Define demand lease/token.** Independent requesters acquire minimum fidelity and release only their own demand.
- [x] **R24 — Define demand aggregation.** Highest required fidelity wins deterministically; stable tie/ordering rules must not depend on hash/dictionary iteration.
- [x] **R25 — Define transition state.** Expose desired/current fidelity plus promotion/demotion readiness/failure diagnostics without leaking mutable coordinator internals.
- [x] **R26 — Define semantic reasons.** Diagnostics identify requester/category/reason without putting campaign-specific policy into shared runtime.
- [x] **R27 — Add core regressions.** Cover acquire/release order, overlapping demands, duplicate requests, requester cleanup, failed promotion, and deterministic replay of the same demand sequence.

## C. Physical-world residency handshake

- [x] **R30 — Map Detailed to physical prerequisites.** A detailed spatial target must acquire the required VoxelEngine streaming residency through its public API before detailed gameplay realization begins.
- [x] **R31 — Preserve ownership during asynchronous readiness.** Residency may wait/transition, but must not fabricate terrain/structure readiness or bypass the streaming owner.
- [x] **R32 — Order demotion safely.** Quiesce detailed gameplay consumers before releasing their world-residency demand; prove no consumer reads an unloaded spatial substrate.
- [x] **R33 — Handle streaming failure/cancellation.** Surface semantic transition failure/cancellation and remain at a valid lower fidelity rather than partially realizing gameplay.
- [x] **R34 — Add world-handshake regression.** Fake/test implementation of the public streaming capability proves ordering without implementing a parallel world streamer.

## D. Characters / CharacterAI

- [x] **R40 — Character adapter.** Promote/demote the same `CharacterId`; no replacement NetworkNpc/ResidentNpc/LoadedCharacter identity.
- [x] **R41 — Define Dormant character representation.** Preserve only owner-required durable semantic state; do not retain detailed path/perception/presentation machinery unnecessarily.
- [x] **R42 — Define Coarse AI update.** Support at least one autonomous-life transition (for example Work → TravelHome → AtHome) without detailed navigation/perception stepping.
- [x] **R43 — Define Detailed realization.** Restore authoritative pose/placement and detailed AI through existing character/world APIs at a valid believable location derived from coarse state.
- [x] **R44 — Preserve owner state.** Vitality, inventory binding, relationships, quest/story identity, and other authoritative domain facts survive transitions unchanged unless their owners legitimately mutate them.
- [x] **R45 — Interrupt/pin semantics.** Active player control, cutscene/interaction, combat/encounter, or another demonstrated requester can independently require Detailed without residency owning that policy.
- [x] **R46 — Character cycle regression.** Prove Dormant → Coarse → Detailed → Coarse → Dormant preserves one `CharacterId` and expected semantic activity/state.
- [x] **R47 — Two-character scaling fixture.** Prove independently relevant/distant characters can occupy different fidelity levels without one global town-loaded switch.

## E. WorldObjects

- [x] **R50 — Reuse stable WorldObject identity.** Presentation/registry unload must not destroy authoritative `WorldObjectId` meaning.
- [x] **R51 — Reuse sparse retained state.** Changed object state is restored by the existing WorldObject persistence authority; residency stores no duplicate door/chest/lever state.
- [x] **R52 — Separate interaction availability from presentation.** Detailed interaction requires a valid realized target, while dormant persistence remains valid without a Unity object.
- [x] **R53 — WorldObject reload regression.** Change an existing generated object's authoritative state, unload/demote its presentation/region, reload/promote, and verify the same ID and resulting state.
- [x] **R54 — Unchanged-object proof.** Demonstrate generated unchanged objects do not require per-object retained runtime state merely because their semantic definition exists.

## F. Encounters / semantic pins

- [x] **R60 — Encounter residency adapter.** Active encounter/site/participant requirements request fidelity through the common coordinator rather than direct streaming/runtime ownership.
- [x] **R61 — Prevent unsafe demotion.** Active participants or required encounter space cannot silently demote below the fidelity required by encounter rules.
- [x] **R62 — Release after lifecycle completion.** Encounter completion/cleanup releases only encounter-owned residency demands; player/story/other demands remain intact.
- [x] **R63 — Encounter pin regression.** Move all players outside ordinary proximity while an explicit active encounter requirement remains; required target stays Detailed, then demotes after the pin releases.

## G. Networking / presentation separation

- [x] **R70 — Keep server residency independent of client interest.** A server-simulated entity may remain Detailed/Coarse even when a specific client receives no updates.
- [x] **R71 — Reuse existing interest management.** Adapt semantic residency/relevance information only where needed; do not build a second replication graph.
- [x] **R72 — Snapshot-on-interest regression.** A later-relevant client receives current character/WorldObject state, not historical replay or a new gameplay identity.
- [x] **R73 — Presentation lifecycle adapter.** Unity presentation may instantiate/despawn/rebuild from authoritative state without becoming the lifetime owner.
- [x] **R74 — Audit client/server terms.** No API contract conflates `Detailed` with `replicated`, `visible`, or `GameObject exists`.

## H. Procedural WorldBuilder integration

- [x] **R80 — Sparse generated settlement fixture.** Use production WorldBuilder semantic definitions (or the narrowest independent fixture available if procedural semantic expansion has not landed) to represent many NPCs/objects while only a bounded subset is Detailed.
- [x] **R81 — Stable generated IDs.** Same seed/semantic inputs yield identities that survive unload/reload and can be referenced by persistence/residency without ordinal/index coupling.
- [x] **R82 — No Kentridge policy leakage.** Generic residency targeting must not depend on `KentridgeRole`, named Kentridge NPCs, captured coordinates, or a particular scene.
- [x] **R83 — Authored-anchor compatibility.** Prove a named/important Kentridge-style character can be pinned Detailed using the same mechanism as generated characters.

## I. Thrash, cost, and diagnostics

- [x] **R90 — Demonstrate boundary churn.** Exercise repeated movement across a residency boundary and measure transition frequency.
- [x] **R91 — Add minimal hysteresis/dwell only if needed.** Prevent demonstrated promote/demote thrash without delaying explicit high-priority pins; configuration must be semantic, not scene-specific.
- [x] **R92 — Expose diagnostics.** Report counts by fidelity, pending transitions, active demand reasons, and physical-world wait/failure state through a read-only diagnostics seam suitable for tests/tools.
- [x] **R93 — Measure representative cost.** Record semantic entity count vs Dormant/Coarse/Detailed counts, update work, and transition churn for the generated-settlement fixture; compare against repository budgets/current baseline rather than inventing weaker limits.

## J. Architecture / persistence / cleanup

- [x] **R100 — API/runtime audit.** No foreign Runtime references outside Composition; no Unity presentation types in Residency.Api; no VoxelEngine.Runtime type leaks.
- [x] **R101 — Single-owner audit.** Search for new duplicate resident-character, streamed-WorldObject, encounter-streaming, or persistence state introduced by the change and remove it.
- [x] **R102 — Persistence round trip.** Persist/restore at least one character and changed WorldObject after residency cycling; restore through normal runtime graph and preserve stable IDs/state without replaying transitions.
- [x] **R103 — No quest/story streaming requirement.** Confirm cheap non-spatial progression/story state remains resident unless a demonstrated cost/requirement says otherwise; do not force all domains into this lifecycle.
- [x] **R104 — Remove obsolete bypasses.** Any directly discovered game-level load/unload path that conflicts with the new single coordinator is migrated or explicitly justified by distinct ownership.

## K. Final reuse / validation / closure

- [x] **R110 — Primary reuse proof.** Town NPC uses real Character + CharacterAI + world streaming paths through the residency coordinator across all three levels.
- [x] **R111 — Independent reuse proof.** WorldObject or second non-character consumer uses the same coordinator without character-specific policy.
- [x] **R112 — Explicit-demand proof.** Encounter/story/control pin composes with proximity demand and releases independently.
- [ ] **R113 — Run focused deterministic/module tests.** Use repository-driven affected module validation; do not hand-maintain an alternate test manifest or parallel harness.
- [ ] **R114 — Run required exact-SHA integration/player validation.** Any player-visible residency/realization acceptance must be proven through the production built-player path selected by repository CI.
- [ ] **R114A — Resolve demonstrated mandatory-player Input-System blocker.** The preserved exact run exposed legacy `UnityEngine.Input` polling in the canonical Kentridge player while Player Settings are Input-System-only. Keep the repair composition-scoped and prove the standalone-player gate no longer throws the exception.
- [ ] **R115 — Inspect final blast radius.** Confirm transition ordering, state continuity, streaming pins, replication behavior, generated-content scaling, and applicable device budgets without weakening unrelated assertions.
- [ ] **R116 — Close only with all acceptance proven.** Populate resolution summary, regression test, fix commit, validation evidence, move the SceneIssue directly to `closed/`, merge current master, and promote the exact validated head per `SceneIssues/README.md`.
