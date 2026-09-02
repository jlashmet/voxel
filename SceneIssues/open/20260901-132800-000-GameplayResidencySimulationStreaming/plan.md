# Gameplay residency / simulation streaming — implementation plan

**Target ownership:** introduce one game-level residency coordination boundary, likely `Assets/Game/Residency/Api` / `Runtime` after baseline inventory confirms naming and existing equivalents. Domain state remains owned by Characters, CharacterAI, WorldObjects, Encounters, Inventory, Quests/Story, persistence, and VoxelEngine streaming.

## Acceptance

- Gameplay identity/durable semantic state outlives simulation, physical-world, network-interest, and Unity-presentation residency.
- Use a small semantic fidelity model (`Dormant`, `Coarse`, `Detailed`, unless equivalent existing vocabulary should be reused), not one generic loaded flag.
- Multiple independent demanders combine safely; highest required fidelity wins and one requester cannot release another requester's demand.
- `Detailed` spatial simulation waits for required VoxelEngine world residency; demotion releases residency pins only after detailed consumers have quiesced.
- Characters retain the same `CharacterId`, vitality/inventory bindings, semantic activity, and other owner state across demote/promote cycles.
- CharacterAI can represent distant autonomous life coarsely without detailed perception/navigation while preserving authoritative semantic outcomes.
- WorldObjects retain stable `WorldObjectId` and sparse authoritative state while presentation/streamed registries unload and reload.
- Active encounter/story/control requirements may pin needed fidelity; residency does not absorb encounter or story policy.
- Server simulation residency is distinct from per-client network interest. Replication filters state; it does not define authoritative existence.
- Procedural WorldBuilder content may exist as cheap semantic definitions without instantiating every NPC/object/encounter.
- No duplicate character, WorldObject, encounter, persistence, replication, or voxel-streaming authority is introduced.

## Chosen architecture

`world/semantic definitions + durable state + fidelity demands` → **Gameplay Residency coordinator** → owner-specific adapters that promote/demote simulation detail. The coordinator owns only demand aggregation, deterministic transition ordering, readiness/transition state, and diagnostics. Owner adapters perform domain-specific realization/suspension through public APIs.

Detailed spatial consumers acquire physical-world residency through `VoxelEngine.Streaming.Api`; gameplay code never reaches into streaming Runtime. Add hysteresis/minimum dwell or equivalent anti-thrash policy only as needed to prevent demonstrated boundary churn.

## Dependencies / blockers

Gameplay systems 03/04/06/13 and their APIs may still be landing. Fetch master first. If a required contract is unavailable, record the blocker and continue with coordinator semantics, deterministic tests, WorldObject/streaming integration, or fixtures that do not invent substitute authorities.

## Reuse / validation

Primary proof: a town NPC transitions Dormant → Coarse → Detailed → Coarse/Dormant while preserving identity/state and using real world residency. Independent proof: a streamed WorldObject unloads/reloads presentation and preserves identity/state. Also prove one active encounter or explicit semantic pin can retain Detailed fidelity independently of player distance.

Measure active detailed/coarse counts and transition churn under a representative generated settlement/world fixture; do not weaken existing budgets. Final closure requires repository-selected module tests, affected integration/player gates, boundary audit, exact-SHA CI, and all tasks complete.
