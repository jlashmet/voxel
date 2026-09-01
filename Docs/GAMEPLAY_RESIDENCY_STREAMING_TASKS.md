# Gameplay Residency / Simulation Streaming Tasks

Companion plan: `Docs/GAMEPLAY_RESIDENCY_STREAMING_PLAN.md`

Implementation must preserve existing domain ownership. Do not create parallel character, WorldObject, encounter, persistence, replication, or world-streaming systems merely to satisfy these tasks.

## A. Baseline and ownership inventory

- [ ] Inventory current physical world-streaming APIs, region/cell identities, residency events, pin/reference semantics, and current consumers.
- [ ] Inventory Character API/runtime identity, lifecycle, pose, registry, and any current unload/despawn assumptions.
- [ ] Inventory CharacterAI state that must survive detailed-simulation suspension and identify which state is scratch/transient.
- [ ] Inventory WorldObject registry, generated identity, sparse retained-state persistence, presentation lifecycle, and current streamed registration/unregistration behavior.
- [ ] Inventory Encounter instance/membership lifecycle and determine what currently assumes all participants/sites are continuously realized.
- [ ] Inventory gameplay replication interest APIs and distinguish authoritative simulation relevance from per-client replication relevance.
- [ ] Inventory system-16 persistence contracts and confirm no current save schema depends on Unity presentation/runtime object identity.
- [ ] Inventory WorldBuilder site/settlement/region identities that can serve as stable gameplay residency scopes.
- [ ] Record existing overlapping "loaded/active/relevant/resident" concepts and assign each one a single owner; do not add another synonym when an existing semantic concept is sufficient.
- [ ] Document concrete gaps found by the inventory before creating new public contracts.

## B. Define the residency contract

- [ ] Add a small semantic residency level type with the initial levels `Dormant`, `Coarse`, and `Detailed` unless the baseline proves a different existing vocabulary should be reused.
- [ ] Define a stable residency target/scope identity that can address at least a spatial scope and, where needed, a specific semantic entity without Unity object references.
- [ ] Define an owner-safe demand token/lease model so multiple independent requesters can require minimum fidelity without releasing each other's demand.
- [ ] Define demand metadata sufficient for deterministic diagnostics: requester/category, target, minimum level, and semantic reason.
- [ ] Define how multiple demands combine: highest required fidelity wins; no requester directly forces another domain down in fidelity.
- [ ] Define query/notification semantics for current desired fidelity and transition status without exposing mutable coordinator internals.
- [ ] Keep the public API engine-independent where practical and free of `GameObject`, `Transform`, collider, NavMesh, renderer, packet, or concrete VoxelEngine Runtime types.
- [ ] Add deterministic unit tests for demand acquisition, combination, release order, duplicate/repeated requests, and owner-safe cleanup.

## C. Physical-world residency handshake

- [ ] Identify or add the narrow VoxelEngine Streaming API capability needed to request/release a gameplay-owned physical-region pin and observe readiness.
- [ ] Ensure gameplay code references only the streaming API; concrete runtime construction/wiring remains in Composition.
- [ ] Define the transition state for `Detailed requested but physical world not ready`.
- [ ] Ensure Detailed gameplay realization does not start until the required voxel/site region is ready.
- [ ] Ensure releasing a gameplay residency demand releases only the gameplay-owned world pin and does not unload a region retained by another consumer.
- [ ] Add a behavioral regression proving two independent world-residency consumers cannot release each other's pin.
- [ ] Add a behavioral regression proving a failed/delayed world load leaves semantic gameplay identity/state intact and yields a useful diagnostic.

## D. Spatial scope resolution

- [ ] Define how a persistent character maps to its current stable region/site/residency scope while not Detailed.
- [ ] Define how a generated WorldObject maps deterministically to its owning region/scope.
- [ ] Define how encounter/site bindings map to the physical regions they require.
- [ ] Support character migration between residency scopes without changing `CharacterId`.
- [ ] Ensure scope resolution does not depend on a loaded Unity presentation object.
- [ ] Add tests for scope migration, boundary crossing, and stable identity across scope changes.

## E. Character residency adapter

- [ ] Implement the Character-owned adapter/mechanism that can enter and leave Detailed simulation without replacing the character identity/runtime model.
- [ ] Explicitly classify character state as durable semantic state versus detailed-simulation resources/scratch state.
- [ ] On Detailed → Coarse/Dormant, complete the current authoritative mutation boundary before releasing detailed resources.
- [ ] Preserve `CharacterId`, vitality/defeat state, required pose/location facts, activity/goal state, and existing inventory bindings across transitions.
- [ ] Do not introduce `DormantCharacter`, `SceneCharacter`, `NetworkCharacter`, or another shadow authoritative actor type.
- [ ] On Coarse/Dormant → Detailed, wait for physical world readiness and realize a valid authoritative pose using retained state/site realization rather than arbitrary spawn search.
- [ ] Reuse existing character registry/binding paths so cutscene/NPC/encounter references resolve to the same character before and after streaming.
- [ ] Add a deterministic round-trip regression: Detailed → Dormant → Detailed preserves stable identity and required semantic state.

## F. Character AI simulation LOD

- [ ] Separate long-horizon semantic AI state from detailed perception/navigation/planner scratch state according to system 04's approved design.
- [ ] Implement/complete a coarse update path that can advance at least one real autonomous-life activity without detailed perception or pathfinding.
- [ ] Ensure Coarse and Detailed AI operate on the same character and authoritative AI state model rather than separate implementations.
- [ ] Define deterministic cadence/configuration for coarse updates; do not tie it to Unity frame rate.
- [ ] Define transition behavior when Detailed tactical activity interrupts a coarse life goal and when detailed simulation later releases.
- [ ] Add a reuse regression using a non-combat town NPC and one tactical/encounter character through the same residency abstractions.
- [ ] Verify dropping to Coarse/Dormant cannot accidentally advance or duplicate one-shot interactions, quest observations, inventory transactions, or combat outcomes.

## G. WorldObject residency adapter

- [ ] Reuse the existing WorldObject stable identity/state/persistence model; do not create a second unloaded-object state store.
- [ ] Confirm unchanged generated WorldObjects can be reconstructed deterministically without retaining live behavior instances while their region is absent.
- [ ] Ensure changed objects retain only the existing required sparse semantic state keyed by stable `WorldObjectId`.
- [ ] On region/detail realization, reconstruct/register the WorldObject and overlay retained state through the existing authority.
- [ ] On unload, release runtime/presentation registration without deleting logical object identity or sparse state.
- [ ] Add a regression with a mutable mechanism or container: mutate state, unload, reload, and verify same `WorldObjectId` and authoritative state.
- [ ] Add a regression proving Unity `GameObject`/collider recreation does not change WorldObject identity.

## H. Encounter residency integration

- [ ] Add an encounter-owned residency demand for active encounter sites/regions and required participants.
- [ ] Initial policy: active encounters require Detailed residency unless the encounter owner later proves a safe coarse mode.
- [ ] Ensure persistent NPC participants are the existing `CharacterId` instances, not encounter-local duplicates.
- [ ] Ensure temporary participant lifecycle remains owned by Encounters/Characters and is cleaned up normally.
- [ ] On encounter cleanup/resolution, release only encounter-owned residency demands.
- [ ] Add a regression where another demand keeps a participant/site Detailed after encounter cleanup.
- [ ] Add a regression where an encounter pins an otherwise distant site, waits for world readiness, realizes participants, resolves, and releases its pin.

## I. Procedural WorldBuilder content residency

- [ ] Define the boundary between deterministic generated world definition and live gameplay realization for a settlement scope.
- [ ] Prove a sparse procedural settlement can exist from stable seed/definition data without eagerly creating every Character runtime, WorldObject behavior, encounter instance, or presentation object.
- [ ] Ensure generated NPC/WorldObject stable IDs reproduce exactly from the same world seed, authored constraints, and generation policy.
- [ ] Evaluate whether immutable generated semantic definitions should be retained, cached, or deterministically regenerated per scope; choose the simplest correct policy and document it.
- [ ] Persist only required mutable/generated deltas rather than serializing an enormous fully expanded world graph when deterministic regeneration is sufficient.
- [ ] Ensure authored anchors such as Kentridge NPCs/sites and fully generated settlement entities use the same residency mechanisms.
- [ ] Add an independent procedural-town fixture proving the residency system does not depend on Kentridge-specific identities.

## J. Network interest integration

- [ ] Keep system 06 network interest separate from authoritative gameplay residency.
- [ ] Define the semantic adapter from authoritative character/WorldObject/encounter state to current-client snapshot/delta relevance.
- [ ] Ensure a client becoming interested never causes creation of a second authoritative gameplay entity.
- [ ] Ensure a client losing interest never destroys authoritative server entity/state.
- [ ] Ensure newly interested/late-joining clients receive current relevant snapshots rather than historical simulation replay.
- [ ] Reduce high-frequency movement/activity replication for distant uninterested clients while preserving durable/convergent state semantics.
- [ ] Add a two-client regression: server keeps a town NPC coherent, nearby client receives detailed state, distant client receives no unnecessary high-frequency updates, then later receives a current snapshot on entry.

## K. Presentation lifecycle integration

- [ ] Define the presentation binding from stable semantic identity to Unity realization without making presentation the authoritative registry.
- [ ] Ensure presentation can create/destroy models, animation, audio, VFX, and applicable proxies as relevance changes.
- [ ] Ensure presentation rebuild resolves to existing `CharacterId` / `WorldObjectId` rather than allocating new gameplay identity.
- [ ] Verify presentation teardown does not mutate authoritative vitality, inventory, quest, story, encounter, or WorldObject state.
- [ ] Add focused validation proving repeated presentation unload/reload does not duplicate subscriptions, interactions, colliders, audio sources, or semantic events.

## L. Transition policy, hysteresis, and budgets

- [ ] Define load and unload thresholds separately so entities/regions do not thrash at a boundary.
- [ ] Add configurable hysteresis/minimum dwell behavior only at the residency policy layer.
- [ ] Define bounded per-tick transition budgets and priority ordering so a large boundary crossing cannot stall the authoritative simulation.
- [ ] Prioritize player-critical, encounter-critical, and cutscene-critical Detailed demands ahead of background Coarse work.
- [ ] Ensure transition ordering is deterministic for a fixed authoritative input sequence.
- [ ] Add stress tests with many targets crossing thresholds and verify stable bounded transition work.

## M. Persistence / restore integration

- [ ] Confirm residency tokens, Unity objects, network-interest state, pathfinding buffers, and other transient machinery are excluded from durable saves.
- [ ] Persist only owning-domain semantic state plus stable world-generation identity and existing sparse world deltas.
- [ ] On restore, compose the normal runtime graph first, restore semantic state, then derive fresh residency from current demands.
- [ ] Add a save/restore regression where a previously Detailed character and modified WorldObject restore with correct semantic state but no assumption that their prior runtime/presentation instances still exist.
- [ ] Verify restore does not replay historical transitions or duplicate one-shot gameplay effects.

## N. Diagnostics and observability

- [ ] Add inspectable diagnostics for each residency target: current level, desired level, active demand sources, spatial scope, transition state, and blocked prerequisite if any.
- [ ] Add counters/metrics for Dormant/Coarse/Detailed entity counts by domain without high-cardinality per-entity metric labels.
- [ ] Add transition counters and failure/retry reasons.
- [ ] Make diagnostics available to development tooling without placing debug policy into domain APIs.
- [ ] Ensure diagnostics can answer why an entity is still Detailed when expected to unload.

## O. Architecture boundaries

- [ ] Add/extend assembly-boundary tests so gameplay residency uses domain APIs and VoxelEngine Streaming API only; no foreign Runtime references outside Composition/tests/tooling exceptions.
- [ ] Verify the coordinator does not become a universal state/query service for character health, inventory, quests, encounter internals, or WorldObject behavior.
- [ ] Verify each domain owns capture/transition of its own state.
- [ ] Verify Net owns replication interest, not authoritative entity lifetime.
- [ ] Verify Presentation owns Unity realization, not authoritative identity.
- [ ] Verify VoxelEngine.Streaming owns physical region residency, not gameplay-domain lifecycle.
- [ ] Verify no `IsLoaded`/`IsActive` duplicate state is introduced where an owning domain already exposes the required lifecycle fact.

## P. Reuse and acceptance fixtures

- [ ] **Persistent NPC fixture:** same `CharacterId` survives Detailed → Coarse → Dormant → Detailed with vitality/activity/location intact.
- [ ] **Coarse-life fixture:** town NPC completes or advances one meaningful background life transition without detailed perception/pathfinding/presentation.
- [ ] **WorldObject fixture:** changed generated mechanism unloads/reloads with same `WorldObjectId` and state.
- [ ] **Encounter fixture:** active encounter pins site/participants Detailed, then releases only its own demands.
- [ ] **World prerequisite fixture:** Detailed request waits for real physical region readiness and never substitutes fake terrain/site realization.
- [ ] **Multiplayer-interest fixture:** nearby/distant/late observer behavior reuses the existing network stack and current-snapshot path.
- [ ] **Persistence fixture:** semantic state round-trips while residency/presentation machinery is recreated normally.
- [ ] **Procedural settlement fixture:** a fully generated town demonstrates cheap semantic existence and bounded runtime realization without Kentridge-specific coupling.

## Q. Performance and blast-radius validation

- [ ] Measure baseline CPU/memory/runtime entity counts before residency changes in a representative populated world fixture.
- [ ] Measure Dormant/Coarse/Detailed costs independently where practical.
- [ ] Demonstrate that increasing total semantic world population does not linearly increase detailed per-frame simulation/presentation cost when most content is dormant.
- [ ] Demonstrate bounded transition work when entering a populated settlement rather than one-frame realization of the entire scope.
- [ ] Confirm existing voxel world-streaming budgets/regressions remain green.
- [ ] Confirm existing character, AI, WorldObject, encounter, persistence, replication, and Kentridge integration tests remain green.
- [ ] Inspect any newly exposed correctness or quality defects; fix only defects required by the acceptance/invariants rather than opportunistically refactoring adjacent systems.

## R. Completion gate

- [ ] Review the final implementation against `Docs/GAMEPLAY_RESIDENCY_STREAMING_PLAN.md` and record any intentional design changes.
- [ ] Confirm every public contract is semantic/configuration-driven and has a clear single owner.
- [ ] Confirm at least Character + WorldObject + Encounter are independent consumers of the residency coordination layer.
- [ ] Confirm a fully generated settlement and authored Kentridge content use the same residency model.
- [ ] Confirm no parallel domain runtime was introduced.
- [ ] Confirm exact-sha automated tests and required built-player/integration gates pass before calling the implementation complete.