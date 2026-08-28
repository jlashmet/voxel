# Experiment 004 — VoxelShowcase bake cache coverage

## Observation
Exact request `d633741fe940c31b783b01f912d55839527d5208` (source `e4f952ad29118443bbf487f786e98583df73204d`, run `33107330590`) passed `CapturedEastMarketLampKeepsPlanarSupportUnderLantern`, but its real-player replay still showed the foreground lamp foot detached from the shoulder.

## Competing explanations
1. The one-voxel embed is still insufficient in the rendered world.
2. The replay did not contain the current WorldBuilder-generated world.

## Discriminating evidence
The run log says the VoxelShowcase startup world was restored from the `ShowcaseWorld.bytes` cache. `tools/showcase-bake-cache.sh` fingerprints scene/composition/voxel-engine inputs but omitted `Assets/Game/WorldBuilder`, while the lamp placement/geometry fix is in `Assets/Game/WorldBuilder/Generation/Voxel/KentridgeStreetDressingCatalogue.cs`. Therefore WorldBuilder changes could reuse a bake made before the fix.

## Decision / falsifier
Do not deepen or otherwise retune the lamp based on that replay. Add `Assets/Game/WorldBuilder` to the semantic bake fingerprint and rerun the same exact behavioral test + saved-pose replay. The next run must show a cache miss/fresh bake (or an independently valid cache keyed by this WorldBuilder state) before its image is accepted as evidence. If that fresh replay still floats, the product hypothesis remains live and must be investigated from the rendered world.
