# Plan

## Observed defect / acceptance
- Human review rejected the prior closure: the built VoxelShowcase showed the normal castle/terrain view but no visually substantial mountain or readable winding ascent.
- Closure requires durable built-application captures from normal approach, path entrance, representative switchbacks, supported summit dragon, and proximity dialogue; the complete route must use normal CharacterMotor movement with no teleport/jump shortcut.
- The checked-in startup bake must contain the accepted result. Structural/catalogue assertions are supplemental.

## Competing hypotheses / discriminator
1. **Stale checked-in startup asset.** The VoxelShowcase bake predates the mountain and suppresses later source realization.
2. **Runtime realization/render defect.** Current source exists but production realization/presentation loses it.
3. **Authored geometry defect.** Mountain/path source is too small, steep, disconnected, or unsupported.

Discriminator: compare bake provenance to the mountain landing, inspect authored dimensions/topology, then validate a current-source bake and walk the exact built player.

## Results
- `ShowcaseWorld.bytes` was last refreshed Aug 25; mountain source landed Aug 28. Hypothesis 1 is confirmed. `GenerateForBakeBlocking` authors fresh source, so the baker itself is not restoring the stale runtime image.
- Current authored geometry is substantial/traversable by construction: 100 m base diameter, 28 m rise, 8 m summit radius, 3 m path width, six 36 m runs with ~4.6 m rise and explicit supported landings. Hypothesis 3 is not supported by source inspection; built traversal remains the authority.
- Prior rejected player captures were inspected directly and show no readable mountain route.
- Shared presentation is now corrected: `MountainDragonShowcaseDriver` only supplies player coordinates/time and binds `CutsceneDialogueOverlay`; proximity/story/cutscene logic and `OnGUI` presentation live in reusable modules.
- Startup provenance now binds content signature + payload SHA-256, and focused acceptance samples the realized bake at mountain core, path base/turns, summit support/path, and dragon.
- CI request `33235920288` never reached Unity because `replay_seconds=110` violated the workflow contract (20-60 seconds).
- Corrected 60-second request `33236501834` reached Unity and exposed obsolete named argument `displayMilliseconds`; both feature-owned `TimedCutsceneDialogueRuntime` callsites now use `displayDurationMilliseconds` without changing the shared API.
- Follow-up request `33236605080` cleared that error and exposed the next assembly-boundary defect: `VoxelEngine.Showcase` references `Game.Cutscenes.Presentation` but not the `Game.Cutscenes.Api` assembly that defines `IActiveCutsceneDialogue` in `CutsceneDialogueOverlay.Bind`. The SceneRuntime asmdef now has the direct API reference.

## Selected fix / gates
- Refresh and track the current generated startup bake + manifest; never accept a stale or mismatched payload.
- Use a generic issue-owned waypoint replay that steers existing AutoWalk heading through `CharacterMotor.Step`; it records named approach/base/switchback/summit/dialogue captures and exits nonzero on traversal timeout.
- Keep the route fixture tied to authored WorldBuilder centerlines in the focused regression.
- Blast radius: shared WorldBuilder primitive/catalogue, startup bake contract, shared cutscene presentation, the `VoxelEngine.Showcase` assembly dependency, and opt-in showcase evidence harness only. Cold bake evidence is 199 regions / 11.2 MiB; runtime encounter cost is one reusable 2-D proximity update per frame plus dialogue presentation only while active.
- Final gates: current bake, exact built-player route/captures + human review, green exact-SHA targeted CI on the assigned transport, metadata promotion/closure, latest-master merge, non-force master push.
