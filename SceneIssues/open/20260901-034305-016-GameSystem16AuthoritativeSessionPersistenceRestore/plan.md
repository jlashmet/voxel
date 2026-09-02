# 16 Authoritative session persistence & restore — implementation plan

**Target module:** `Assets/Game/Persistence/Api` / `Runtime` (`Game.Persistence.Api`, `Game.Persistence.Runtime`). Storage backend remains behind an API seam.

## API

Versioned `GameSessionSnapshot` metadata/header, subsystem snapshot contributor/restore interfaces, persistence request/result, save metadata for frontend listing, compatibility failure reasons, and stable content/world identifiers.

## Runtime

1. Define coherent capture barrier/revision across authoritative subsystems and world state.
2. Register subsystem capture/restore contributors without Persistence depending on their Runtime assemblies.
3. Serialize only semantic state/stable ids; exclude Unity objects, transport ids, presentation state, AI scratch data.
4. Publish saves atomically through a pluggable store.
5. Restore by asking #14 to compose the normal graph then applying validated snapshots before Running.
6. Add schema/content compatibility handling and deterministic failure paths.

## Dependencies

14 orchestration, APIs of authoritative subsystems, existing voxel/world persistence mechanisms where available.

## Tests / proof

Mid-run save/restore, no historical one-shot replay, stable ids preserved, corrupt/incompatible save rejection, completed outcome persistence, fresh graph after restore.

## Do not build

No autosave/checkpoint policy in core persistence, PlayerPrefs coupling, or serialized scene object graphs.
