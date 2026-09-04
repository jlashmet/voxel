# Gameplay residency / simulation streaming — implementation plan

**Target ownership:** introduce one game-level residency coordination boundary at `Assets/Game/Residency/Api` / `Runtime`. Domain state remains owned by Characters, CharacterAI, WorldObjects, Encounters, Inventory, Story/Progression, Persistence, WorldBuilder, GameplayReplication, and VoxelEngine Streaming.

## Observed baseline / acceptance

Baseline is `origin/master` `ed5c6f908361228819b3368bcd8427d4b44d89e3`. Character, CharacterAI, WorldObject, Encounter, Application/Persistence, GameplayReplication, WorldBuilder and VoxelEngine Streaming APIs are present. Characters already own stable `CharacterId`, registry state and kinematics; WorldObjects own stable `WorldObjectId` and snapshots; Encounters own stable participants/lifecycle; replication publishes current semantic state independently of simulation residency; WorldBuilder exposes stable Region/Settlement/Site/Npc refs.

Acceptance remains: stable gameplay identity/state must outlive `Dormant`/`Coarse`/`Detailed` simulation, physical-world residency, client interest and Unity presentation; independent demands compose by maximum fidelity and release independently; Detailed spatial realization waits for world readiness and quiesces before physical release; no duplicate domain/persistence/replication/streaming authority.

## Hypotheses / discriminating results

1. **Existing `IRegionStreaming` is already an ownership-safe physical-residency primitive.** Falsified. It exposes queue/publish/resident/evict only, while `Streaming.Runtime.ResidencyManager` can evict directly through Storage policy, so gameplay cannot safely emulate a pin by load-now/evict-later.
2. **A narrow Streaming-owned lease plus one game-level demand coordinator is sufficient.** Selected. Streaming owns ref-counted physical pins and makes all existing eviction paths respect them. Gameplay Residency owns only semantic demand aggregation, deterministic transition ordering/readiness/diagnostics and adapter orchestration.

## Chosen architecture

`semantic target + independent fidelity demands` → **Gameplay Residency coordinator** → owner adapters. Coordinator stores no Character/WorldObject/Encounter state. Shared fidelity is `Dormant < Coarse < Detailed`; highest request wins. Spatial Detailed promotion obtains an `IRegionResidencyLease` through `VoxelEngine.Streaming.Api`, waits for `IsReady`, then realizes the owning adapter. Demotion quiesces the adapter first and only then disposes its physical lease.

Runtime may depend on foreign **Api** assemblies only. Stable target IDs are semantic values, never `GameObject`, `Transform`, renderer/collider, packet, runtime implementation, ordinal or captured-scene coordinate. Server simulation residency remains independent of client replication interest/presentation lifetime.

Streaming already has distance hysteresis. Add gameplay dwell/hysteresis only if R90 demonstrates semantic transition churn.

## Validation / remaining gates

Streaming lease prerequisite commit: `71b31edefcc6d5511b158f7a9c5b66a4d1c355c5`; focused regression proves multiple pins release independently and engine distance eviction skips pinned regions. Next: deterministic Residency Api/Runtime + tests, then Character/AI, WorldObject, Encounter and composition reuse proofs, module-local runtime validation, persistence/cost/boundary audits, exact-SHA CI and built-player evidence. Close only when every `tasks.md` item and acceptance criterion is proven.
