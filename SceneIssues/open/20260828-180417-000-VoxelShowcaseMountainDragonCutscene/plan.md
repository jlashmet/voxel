# Plan

## Evidence
- Capture has no screenshots or marked regions; the authored note is the complete repro/acceptance contract.
- `ShowcaseWorld` already authors destructible voxel structures through shared structure/cave authoring, so a mountain/path does not require scene-local voxel writes.
- WorldBuilder already models sites, NPC placement, cutscene bindings and story rules; Cutscenes already owns choreography/dialogue execution.
- Story currently supports new-game, NPC-interaction, cutscene-complete and quest-complete triggers only; `CampaignRuntime` exposes the same event set. There is no reusable authored site-proximity trigger.

## Competing hypotheses
1. **Missing mountain/cutscene systems.** Rejected: reusable voxel authoring, NPC planning, cutscene execution and dialogue presentation already exist.
2. **Encounter content exists but is merely unwired.** Rejected for the required flow: no mountain/dragon content exists for VoxelShowcase and no proximity trigger can express approach-to-POI.
3. **Missing reusable proximity seam plus missing authored encounter.** Supported. Add a semantic site-proximity trigger/event in shared WorldBuilder/Story/Campaign runtime, then compose the mountain/path/placeholder/cutscene through shared authoring from Showcase.

## Fix / verification
- Add the smallest reusable WorldBuilder/shared-game primitive needed for site-proximity story triggers; keep world-space distance evaluation outside Story.
- Add reusable authored landmark/path/placeholder composition only where existing WorldBuilder voxel primitives cannot express the acceptance contract; VoxelShowcase may supply dimensions/placement/dialogue only.
- Add the mountain with a continuous walkable winding ascent, summit placeholder, and production proximity-triggered `Hello, I'm Mr. Dragon.` cutscene.
- Behavioral regression must exercise the production WorldBuilder path and assert substantial mountain mass, continuous ascent samples, stable summit placeholder placement, proximity dispatch, one-shot cutscene start, and dialogue cue.
- Replay/build the exact VoxelShowcase application flow and capture rendered evidence of the mountain/path/dragon plus the approach-triggered dialogue; tests alone are insufficient.

## Blast radius / cost
- Shared trigger addition is semantic and event-driven; Story remains position-agnostic.
- Landmark generation is bounded to the authored mountain footprint and runs during world build; no per-frame voxel generation.
- Proximity evaluation must be bounded/configured and inactive after one-shot completion so steady-state runtime cost is constant and small.
