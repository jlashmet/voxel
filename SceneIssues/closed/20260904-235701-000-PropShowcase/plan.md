# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching through exact standalone-player evidence. Canonical scope is 529 entries: 440 registered decorations, 25 presets, 8 mine-cave kinds, 8 natural-cave kinds, and 48 world-object kinds. Structures owns enumeration/shared presenters; SceneRuntime owns browser/resource orchestration; Materials owns procedural-material composition. Each runtime owner has module-local validation; the top-level PropShowcase scene is integration evidence only.

## Final result
The repeated Forge Hearth visual failure was isolated before a third fix. Root cause was canonical Hearth authoring dropping `DecorationPlacement.Facing` and hard-coding the open side to world `-Z`. The production authoring path now carries semantic facing through all four horizontal cardinal directions and is guarded by `Game.Structures.Tests.ForgeHearthFacingAuthoringTests`.

Exact request `8e5d78f5b2dcdae1fd8e2a92d85ff5bd278a13ba` / run `34017941950`, directly validating feature SHA `b470db110e4a8edb3029e881656ac508bb20e057`, completed successfully. Repository-driven EditMode/PlayMode validation, all required module-local standalone players, and the canonical Kentridge standalone integration gate passed. Direct PropShowcase player review shows a usable 529-entry left catalogue/right preview, production-faithful representative props, horizontal Trapdoor, framed Merchant Sign, and a grounded/open Forge Hearth with coherent masonry/fire presentation. Initial voxel-backed publication progresses through loading to ready before capture.

Three resource cycles kept owned runtime counts stable at 5 objects, 3 renderers, 3 colliders, 1 light, 0 particles, 4 global meshes and 53 global materials. Unity allocated memory moved from 155,181,215 to 155,325,175 bytes (+143,960) across the sampled cycles. Stress navigation completed 110 switches with peak/current owned preview count 1, mean switch 2.744 ms and max 4.677 ms, with no runtime exceptions or stale preview accumulation.

## Completion
Every acceptance criterion and required checkbox is satisfied. Closure metadata records the exact verified feature SHA. Final promotion is current-master integration on `fixes/agent-9`, then PR + auto-merge and required `affected` gate; the feature head is never pushed directly to `master`.
