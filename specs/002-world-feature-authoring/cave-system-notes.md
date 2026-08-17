# Cave System Notes

## Deterministic scope

The reusable cave path is integer/hash driven and bounded by `CaveConfig` extents, segment counts, branch count/depth, chamber ranges, and vertical offsets. Surface, structure-attached, and underground entrances use the same `CaveAuthoring` path.

## Loop/reconnection limitation (WB059)

Loops are intentionally unsupported. `CaveConfig.EnableLoops = true` is rejected by validation.

A loop may cross independently generated streaming regions. Supporting it safely requires a region-local portal/reconnection contract where every region derives the same connection from stable global identity, independent of generation order and existing mutable voxels. That contract does not exist yet, so the implementation does not hide a nondeterministic global search behind the loop option. The deterministic branch tree is the supported topology.

## Extension hooks

`CaveMaterialPalette` provides semantic opening, rock, accent, decoration, and water materials. `CaveHookPlanner` emits deterministic `Decoration`, `Resource`, and `Water` hooks at the guaranteed reachable main-path end. Game composition may interpret them; cave geometry does not own loot, harvestables, or mutable gameplay state.
