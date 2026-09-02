# 16 Authoritative session persistence & restore — tasks

**Plan:** [plan.md](plan.md)
**Owning module:** `Game.Persistence.Api` / `Game.Persistence.Runtime`
**Execution rule:** persist semantic facts sufficient to recreate the authoritative run. Do not serialize runtime objects, presentation, transport identity or historical one-shot event streams.

## API / schema

- [ ] **T16-001 — Inventory existing save/world persistence.** Map voxel/world saves, campaign snapshots, PlayerPrefs/scene serialization, test save helpers and all subsystem snapshot shapes already present.
- [ ] **T16-002 — Establish asmdefs/storage boundary.** Persistence.Runtime owns serialization/store coordination; physical save backend is behind API/internal seam and no domain Runtime is referenced.
- [ ] **T16-003 — Define `GameSessionSnapshot` header.** Version/schema, stable session/content/world identifiers, authoritative revision, timestamp/metadata needed by frontend listing; no transport ids.
- [ ] **T16-004 — Define subsystem snapshot contributor interface.** Each authoritative module captures/restores its semantic state through its public contracts/adapters.
- [ ] **T16-005 — Define capture/restore request/results.** Semantic failure reasons for unavailable barrier, corrupt data, schema/content mismatch and storage failures.
- [ ] **T16-006 — Define save metadata/listing contract.** Expose enough for system 23 Continue UI without loading gameplay Runtime objects.
- [ ] **T16-007 — Define compatibility/version policy.** Explicitly classify supported migration, unsupported schema/content and deterministic rejection behavior.

## Runtime

- [ ] **T16-010 — Implement coherent capture barrier.** Coordinate one authoritative revision across world and registered subsystems; reject/serialize concurrent mutation according to deterministic policy.
- [ ] **T16-011 — Register subsystem contributors via API.** Characters, Vitality, Encounters, Inventory, Progression, Sessions/Outcomes etc. contribute without Persistence importing Runtime assemblies.
- [ ] **T16-012 — Integrate existing voxel/world persistence.** Reuse existing mechanisms and stable world identifiers; do not create a second world snapshot system.
- [ ] **T16-013 — Serialize semantic/stable data only.** Add validation that Unity objects, scene refs, transport ids, UI state, audio/VFX state and AI scratch data cannot enter persisted schema.
- [ ] **T16-014 — Implement atomic publication.** Write staged save then publish/replace metadata atomically enough that interrupted writes cannot appear as valid complete saves.
- [ ] **T16-015 — Implement normal-graph restore.** Ask system 14 to compose a fresh production graph, validate snapshot, apply subsystem/world state before Running/GameplayReady.
- [ ] **T16-016 — Preserve stable gameplay identities.** CharacterId, PartyMemberId/PlayerSlot, InventoryId, WorldObjectId, progression/outcome identities round-trip; transport connection ids are regenerated.
- [ ] **T16-017 — Prevent historical one-shot replay.** Restore current authoritative state and event dedupe/revision baselines, not an event-log reenactment.
- [ ] **T16-018 — Keep save policy outside core.** Manual/autosave/checkpoint timing is application/content policy; Persistence only provides capability.

## Verification

- [ ] **T16-020 — Mid-run round-trip test.** Save mixed character/vitality/inventory/progression/world state, tear graph down, restore fresh graph and compare semantic truth.
- [ ] **T16-021 — Active encounter/session round-trip where supported.** Preserve current lifecycle/membership without replaying activation/combat/audio/VFX one-shots.
- [ ] **T16-022 — Resolved-outcome round-trip.** Resolved remains immutable after restore.
- [ ] **T16-023 — Corrupt/incomplete-save tests.** Reject deterministically without starting a partial gameplay graph.
- [ ] **T16-024 — Schema/content incompatibility tests.** Surface explicit compatibility failure to Application.
- [ ] **T16-025 — Atomic-write interruption test.** Partial/staged save is never listed as valid current save.
- [ ] **T16-026 — Multiplayer rehost identity test.** Gameplay identities persist while new transport connections are established.
- [ ] **T16-027 — Run automatic Persistence/domain integration tests.**

## Cleanup / close

- [ ] **T16-030 — Remove duplicate durable state stores.** Search campaign/scene/prototype saves that persist the same authoritative subsystem truth and migrate/delete them.
- [ ] **T16-031 — Serialized-type audit.** Inspect persisted schema for forbidden Runtime/Unity/transport/presentation types.
- [ ] **T16-032 — Close with fresh-graph proof.** A save recreates equivalent semantic state through normal system 14 composition, with no historical replay or alternate runtime path.
