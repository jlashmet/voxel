# Plan

## Acceptance and ownership
Built VoxelShowcase must show a substantial grounded mountain, readable continuous shared-road ascent from accessible terrain, supported allowed cube dragon, and normal proximity dialogue exactly `Hello, I'm Mr. Dragon.` Every issue criterion/checklist item remains required. WorldBuilder owns landform/road authoring; Showcase composes it; Cutscenes owns dialogue; Rendering owns presentation; Composition owns bake provenance. Preserve canonical input and unchanged 240 s / 14 GiB bake guards.

## Current evidence
Feature baseline: `a4a3df0d1756fb495f7faa477ce6b007c82dfaca`. Remote master remains `ef475182b866eabfe8e1d1a39c82bf7810a03f49`, already merged. Run `34001756898`, transport `256b01f03aecbcff10d2783375529d9efdd653cb`, passed its required module tests/players and completed the production route, but its actual seven route captures are **unacceptable**: large magenta regions and flat gray masses obscure the intended mountain/road. Green workflow status is not visual acceptance. Earlier discriminators remain in experiments 010–037.

The same run's player-build log explicitly includes `Voxel/ProceduralFarFeature`, with both Metal vertex/fragment variants retained. Packaging this shader therefore did not resolve the symptom. No specific draw owner has yet been proven. Module player artifact directories are also keyed only by module, so later players overwrite earlier players within the same module; FarWorld's retained directory contains Water evidence. That is a separate required evidence defect, not proof FarWorld rendered correctly.

## Two hypotheses / next discriminator
1. Semantic far-feature submissions still produce the magenta pixels (material/pass/instancing incompatibility or wrong producer data).
2. A different draw path produces them; the prior shader change targeted the wrong owner.

Add a temporary assignment-only built-player observer. After the ordinary first approach capture, pause replay inputs at the current position, inventory live materials/shaders, and capture identical-camera frames with all rendering, semantic-far disabled, component renderers disabled, voxel-surface submission disabled, and all rendering restored. Restore every setting in cleanup. No voxel/collision mutation, replacement geometry/materials, render-budget changes, or acceptance relaxation. Exclusion frames are diagnostics only. Substantial exact error-magenta coverage in the original frame must fail the scene run rather than pass silently. Use the existing module-local validation surfaces and exact CI transport; no new workflow or alternate branch.

## Remaining gates
Read the discriminator before another product shader/geometry change. Implement the smallest demonstrated fix, add a production-path regression, preserve distinct player evidence, and rerun exact built-player/module validation. Require all route captures to pass visual review, 92/92 grounded traversal, supported summit/real dialogue, no exceptions, and unchanged costs. Only then promote the matching startup payload/manifest, complete normal-baker manifest emission and clean-checkout proof, finish all tasks, close directly open→closed, and promote by PR + auto-merge.
