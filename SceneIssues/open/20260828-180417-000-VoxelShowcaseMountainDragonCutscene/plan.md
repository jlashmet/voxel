# Plan

## Observed defect / acceptance
- Human review rejected the prior closure: the built VoxelShowcase showed the normal castle/terrain view but no visually substantial mountain or readable winding ascent.
- Closure requires durable built-application captures from normal approach, path entrance, representative switchbacks, supported summit dragon, and proximity dialogue; the complete route must use normal CharacterMotor movement with no teleport/jump shortcut.
- The checked-in startup bake must contain the accepted result. Structural/catalogue assertions are supplemental.

## Competing hypotheses / discriminator
1. **Stale checked-in startup asset.** The VoxelShowcase bake predates the mountain and suppresses later source realization.
2. **Runtime realization/render defect.** Current source exists but production realization/presentation loses it.
3. **Authored geometry / proof defect.** The source may be substantial yet still render as an artificial slab, or the acceptance sampler/replay may not match actual integer rasterization and player timing.

Discriminator: compare bake provenance to the mountain landing, inspect authored dimensions/topology, then validate a current-source bake and walk the exact built player.

## Results
- `ShowcaseWorld.bytes` was last refreshed Aug 25; mountain source landed Aug 28. Hypothesis 1 is confirmed. `GenerateForBakeBlocking` authors fresh source, so the baker itself is not restoring the stale runtime image.
- Current authored path geometry is substantial/traversable by construction: 100 m overall footprint, 28 m rise, 8 m summit radius, 3 m path width, six 36 m runs with ~4.6 m rise and explicit supported landings.
- Prior rejected player captures were inspected directly and show no readable mountain route.
- Shared presentation is corrected: `MountainDragonShowcaseDriver` only supplies player coordinates/time and binds `CutsceneDialogueOverlay`; proximity/story/cutscene logic and `OnGUI` presentation live in reusable modules.
- Startup provenance binds content signature + payload SHA-256, and focused acceptance samples the realized bake rather than only source primitives.
- CI request `33235920288` never reached Unity because `replay_seconds=110` violated the workflow contract (20-60 seconds).
- Corrected request `33236501834` exposed obsolete named argument `displayMilliseconds`; both feature-owned callers now use `displayDurationMilliseconds`.
- Follow-up `33236605080` exposed the missing direct `Game.Cutscenes.Api` reference in `VoxelEngine.Showcase`; the SceneRuntime asmdef now declares it.
- Run `33236729056` is the first authoritative current-source result: compilation and deterministic bake passed; the bake produced 199 regions / 11,236,267 bytes. The standalone player reached `mountain-approach`, `path-base`, `turn-1`, and `ramp-2-low` through normal CharacterMotor movement before the workflow's 60-second replay limit.
- That run's focused test sampled air at the exact mathematical level-0 ramp high endpoint (`int3(-435, 264, -325)`) even though rendered/player evidence proves the path exists. The semantic assertion is therefore too brittle for integer ramp rasterization and must sample a guaranteed interior surface/column.
- The replay harness currently leaves AutoWalk at ordinary walk speed (5.5 m/s); the same production motor supports 12.1 m/s sprint. The evidence replay must request normal sprint movement and use an internal timeout below the workflow's 60-second ceiling.
- Human inspection of the fresh approach/base captures still fails the reopened visual acceptance: the single perfectly regular frustum reads as a giant constructed pale slab/pyramid rather than a natural mountain. The switchback path itself is readable, so naturalization must preserve its existing ramps/landings.

## Selected implementation
- **Semantic proof:** retain exact landing/summit assertions, but sample each ramp at an interior X/Z location and scan only its authored Y span for path material. This validates realized path occupancy without asserting an intentionally empty integer endpoint cell.
- **Traversal proof:** add an opt-in `AutoWalkSprint` flag to `VoxelShowcase`; the SceneIssue waypoint harness enables it only while replay is active. Movement still flows through `CharacterMotor.Step` with its production sprint speed and collision/streaming; no teleport, jump, transform shortcut, or synthetic proximity trigger.
- **Natural silhouette:** replace the one perfect full-width mountain frustum with a bounded asymmetric cluster: a reduced central summit-bearing frustum plus offset lower side/back shoulder frustums of varied radius/height. Keep the existing path support boxes, ramps, flat turn landings, final summit connector, summit coordinates, and placeholder placement unchanged. Keep the current stone/foundation material for this iteration so visual change is attributable to silhouette rather than an unrelated material-system change.
- Add/adjust a structural regression so the mountain realization cannot silently collapse back to a single symmetric frustum while retaining the existing connectivity/path assertions.

## Blast radius / cost
- Source changes are bounded to the reusable WorldBuilder mountain catalogue, the focused startup-bake regression, and the opt-in showcase evidence movement path. Normal keyboard movement remains unchanged because sprint override is active only with AutoWalk evidence replay.
- Naturalization adds only a handful of bounded frustum primitives to the one-time WorldBuilder/bake program. `MaxPrimitives` is derived from emitted instructions, so no manual budget widening is required. Cold bake remains the relevant cost gate; steady-state encounter/presentation cost is unchanged (one reusable 2-D proximity update per frame plus dialogue presentation only while active).

## Remaining gates
- Regenerate and track the exact current startup bake + manifest.
- Exact-SHA focused acceptance must pass against that bake.
- Built player must complete every evidence waypoint within the 60-second CI replay and show the greeting through normal proximity.
- Human review must accept approach/base/switchbacks/summit captures as a natural substantial mountain with a readable walkable ascent.
- Then green exact-SHA targeted CI on `ci-test/fixes/agent-4`, metadata promotion, open→pending→closed workflow, latest-master merge, and non-force master push.
