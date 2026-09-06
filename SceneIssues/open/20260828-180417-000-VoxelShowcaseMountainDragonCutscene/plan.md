# Plan

## Acceptance
Built `VoxelShowcase` must show one substantial grounded natural mountain, readable continuous shared-road ascent from accessible terrain, usable summit, visibly supported allowed cube dragon, and proximity dialogue exactly `Hello, I'm Mr. Dragon.` Closure requires green exact-SHA standalone-player/module validation, exception-free normal grounded traversal, `production-quality` human visual review, source-matched checked-in startup payload/manifest, and unchanged 240 s / 14 GiB bake guards.

## Ownership
- WorldBuilder: semantic `MountainLandformSpec`/surface and canonical road intent/resolution/corridor lowering.
- Showcase composition/runtime: Mountain Dragon placement, evidence route, encounter wiring, and normal player traversal.
- `Game.Cutscenes`: reusable timed dialogue runtime/presentation plus module-local validation.
- Rendering: producer-agnostic far-feature geometry/material presentation and module-owned FarWorld player proof.
- Composition: startup-bake provenance/headless contract.
- Master-owned `ShowcaseInputSystem`: canonical physical input authority; no duplicate legacy-shaped facade.

## Proven results
Earlier exact runs established road grade/cut-fill, reusable landform, route/collision fixes, startup provenance, and normal Input-System traversal; experiments 010–036 retain details. Run `33985235532` proved the previous broad automatic-plan fallback could exhaust CI despite relevant players passing, so module ownership/planner classification was corrected.

Exact run `33991882237` (source `71b1f4da...`, transport `86c9d2ac...`) passed the requested startup-bake test and all 17 persisted EditMode assemblies with zero effective failures. Its module gate failed deterministically because Cutscenes requested invalid `runSeconds: 6`; the standalone SceneIssue replay nevertheless completed `92/92`. Fresh captures were rejected because large regions were Unity error magenta. Experiment 037 records the discriminator.

## Selected correction / current state
- Cutscenes scenario is contract-valid at 10 seconds and asserts the production dialogue-active log.
- `ProceduralFarFeatureRenderer` now loads renderer-owned `Resources/ProceduralFarFeature.shader` and fails closed for missing/unsupported/wrong shader; EditMode and FarWorld player validation cover packaging/support.
- Duplicate explicit requested-test execution is suppressed only when the exact leaf is already covered by its selected owning persistent assembly; planner ownership has no intended repository-wide fallback.
- Current master `ef475182...` was merged with feature work in two-parent merge `b7c26717...`; resolution preserves master input/HouseShowcase/Structures work and removes the superseded compatibility input authority. Later commits are SceneIssue documentation only.

## Next discriminator
Run the exact current branch head only through `ci-test/fixes/agent-4`, requesting `VoxelEngine.Showcase.Tests.EditMode.ShowcaseStartupBakeArtifactTests.CurrentSourceBakeExportsPayloadAndMatchingManifest` plus this SceneIssue with ~210 s replay. Require: no fallback paths; every repository-derived module/player green; 92/92 normal grounded replay; summit cutscene/dialogue; no runtime exception; and fresh captures free of white/magenta slab/AABB/error-shader artifacts.

If automation is green but visuals are below `production-quality`, diagnose the exact rendered relationship before more geometry/material changes. After visual acceptance, promote that exact payload/manifest into tracked startup resources, make normal editor bake emit matching manifest, prove clean-checkout consumption, complete all tasks/criteria, close `open -> closed`, merge then-current master if it advanced, and promote only by PR + auto-merge.