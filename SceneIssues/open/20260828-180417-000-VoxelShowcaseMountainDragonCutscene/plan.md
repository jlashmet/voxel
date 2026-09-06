# Plan

## Acceptance and ownership
Built VoxelShowcase must show a substantial grounded mountain, readable continuous shared-road ascent from accessible terrain, supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Run `34001756898` on feature `a4a3df0d...` passed automation and traversed 92/92 waypoints, but all seven route captures are **unacceptable**: magenta regions and gray slab masses obscure the mountain/road. The build explicitly retained `Voxel/ProceduralFarFeature` Metal variants; packaging that shader alone did not fix the symptom. No specific draw owner is proven. Experiments 010–038 retain discriminators; do not promote the rejected candidate bake.

Current diagnostic source is `affc45d54e08362ed6c7515a537bfb386eca4590`; exact request `019f5562d8b9d2575de0024d71ccbdb55dca028f`, run `34006671692`, remains queued at the latest check. Never replace it. Later feature commit `86311702...` independently preserves per-scene/scenario module-player outputs; the queued source predates that correction. Experiment 039 records four local Python orchestration regressions, not Unity visual proof.

Latest fetched master is `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; the feature already contains earlier `ef475182...`. Merge current master before final promotion, and earlier if compatibility requires it, without disturbing queued CI.

## Two hypotheses / next discriminator
1. Semantic far-feature submissions produce the magenta pixels (material/pass/instancing or producer data).
2. Another draw path produces them; the previous shader change targeted the wrong owner.

The queued temporary observer pauses the normal approach, inventories live materials/shaders, and captures identical-camera all-rendering, no-semantic-far, no-component-renderers, no-voxel-surface, and all-restored frames. It restores all settings and fails the player if the untouched baseline contains substantial error-magenta. Exclusion frames are diagnostic only. Read those results before another shader/geometry fix; no budget, collision, or acceptance changes.

## Independent required bake handoff
The ordinary Showcase editor baker writes only bytes; its regression previously manufactured the missing sidecar afterward. Move manifest creation/import into that same baker. The existing production-bake regression must remove an old sidecar, invoke the real baker, validate its emitted manifest and imported Resources text, then export the unchanged pair. This is the already-required normal-baker task, not accepted-payload promotion. Owned proof remains Showcase EditMode plus existing Showcase/runtime module players and the exact SceneIssue replay; no new runtime module or parallel writer. This change awaits the next exact run after the queued diagnostic; leave final checkboxes unproven.

## Remaining gates
Identify draw owner, implement the narrow demonstrated fix, retain production-path regressions, remove temporary exclusions, and rerun exact module/player validation including both later handoff corrections. Require production-quality full-rendering route captures, 92/92 grounded traversal, supported summit/dialogue, no exceptions, and unchanged costs. Only then commit the visually accepted payload/manifest, prove clean-checkout consumption, finish every task, close open→closed, and promote by PR + auto-merge.
