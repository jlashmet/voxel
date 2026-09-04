# 16 Authoritative session persistence & restore — implementation plan

**Target module:** `Assets/Game/Persistence/Api` / `Runtime` (`Game.Persistence.Api`, `Game.Persistence.Runtime`). Storage backend remains behind an API seam.

## Observed baseline / acceptance

Current `master` has semantic persistence shapes in Characters (`ICharacterRegistryPersistence` / `CharacterRegistryState`), Inventory (`IInventoryStatePort` / `InventoryStateCapture`), Vitality (`IVitalityService.Capture/Restore`), Progression (`ProgressionSnapshot`), WorldObjects (`WorldObjectStateSnapshot` plus ordered runtime capture/restore), and Encounters semantic state. Repository searches found no `PlayerPrefs`, `persistentDataPath`, duplicate session save service, or existing durable voxel/world save file mechanism to replace. System 14 production orchestration is not yet on current master, so System16 must expose the fresh-graph factory/barrier seam rather than importing another agent's unmerged Runtime.

Acceptance is a versioned semantic session envelope, coherent capture revision, explicit compatibility/corruption/storage outcomes, atomic publication, validate-before-apply restore into a fresh normal graph, stable gameplay identity preservation, no transport/presentation/runtime-object serialization, and no historical one-shot replay.

## Hypotheses / discriminating result

- **A:** Existing subsystem semantic snapshots are sufficient; Persistence should own only orchestration, durable envelope/codec/store and contributor seams.
- **B:** Existing subsystem restore methods alone are sufficient to preflight a whole restore.

Inventory falsified B: owner restore APIs may mutate while validating. Therefore the Persistence contributor contract has a non-mutating `Validate` phase separate from `Restore`. All sections validate before any apply; a fresh graph is completed/advertised only after every apply succeeds.

## Selected implementation

1. `GameSessionSnapshotHeader` carries format version, stable save/session/content/world ids, authoritative revision and listing metadata.
2. Ordered semantic sections are supplied through `ISessionSnapshotContributor`; Persistence.Runtime depends only on Persistence.Api. `DelegateSessionSnapshotContributor<T>` lets composition adapt owner public contracts without Runtime imports.
3. `SessionPersistenceService` captures under `ISessionCaptureBarrier`, requires every section to match the same revision, checks content/world/schema compatibility, validates all restore sections, then applies them in deterministic restore order to a graph created by `ISessionRestoreGraphFactory`.
4. `SessionSnapshotBinaryCodec` uses a deterministic binary envelope plus SHA-256 integrity. `SessionSchemaGuard` rejects Unity/scene/transport/presentation type declarations.
5. `FileSessionSaveStore` stages writes separately and publishes/replaces only completed files; staged/backup files are never listed as current saves.
6. World truth remains an externally owned `world` contributor keyed by the stable `SessionWorldId`; because no durable voxel/world store exists on current master, System16 does not invent a second voxel serializer.
7. Save cadence/autosave/checkpoint policy remains outside core.

## Validation-scene exception

Persistence is a pure headless/domain module (`noEngineReferences: true`) with no meaningful player-visible scene behavior. Module-local EditMode integration tests are the focused validation surface. Creating a Persistence scene would add presentation policy without exercising additional behavior. Repository exact-SHA CI still supplies the canonical standalone `KentridgePlayableSlice` application gate.

## Remaining gates

Run exact-SHA repository-selected Persistence/domain integration validation and standalone application validation. After green exact-SHA evidence, complete verification checkboxes, audit the final serialized boundary, move this SceneIssue directly `open/` → `closed/`, merge current `origin/master`, and promote through PR + auto-merge only.
