# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching with exact standalone-player evidence. Only production-quality visuals pass; no gate or checkbox is waived.

Current set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants; buildings, terrain, characters, VFX-only records, raw materials and duplicate aliases are excluded.

Structures owns enumeration/presenters and its local production-validation scene. SceneRuntime owns the browser, resource probe and PropShowcaseMaterialValidation. Materials owns the shared procedural material adapter: this revision adds its missing Tests/EditMode assembly and Validation/PropMaterialCompositionValidation scene/scenario. The latter invokes the real production browser for 43 seconds through voxel/thin/procedural material representatives, not reconstructed geometry or a parallel renderer. Parent Showcase/top-level scenes remain integration consumers. Python CI orchestration is headless and uses subprocess regressions.

## Current source and completed request
Resumed `36141baec2c5c1e6d64ee1dc66b025e2ed9b52e8`, including isolation `79b6a2f4` and frame-separated resource instrumentation. Master was read at `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; the three authoritative guides are unchanged. Shell fetch fails DNS, so remote refs/files were read through GitHub. Final master merge is outstanding.

Request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` completed FAILURE at 2026-09-06T02:37:23Z without replacement. It tested older source `de0aa1fb`. The first PlayMode phase reports four passing cases, but the second starts before Unity exits play mode and throws from SaveModifiedSceneTask. The existing process-isolation repair targets that proven failure. It has prior Python coverage, not a green Unity run. Exact provenance and review: `review-34003328146.md`.

## Visual result and next discriminators
Fresh PNGs confirm production shading replaces rainbow normals and the table/sign camera fixes are visible. Overall quality is still prototype/blockout: Merchant Sign is a featureless rectangle, Forge Hearth disconnected box-like bars, and Door/Trapdoor plain upright blue slabs. Initial capture reports READY before the anvil is visible. These are required acceptance defects, not deferred enhancements.

Before changing art broadly, discriminate missing presenter/material semantics from canonical geometry deficits. Trace each rejected production kind through its existing authoring/presentation path; do not add showcase-only substitutes. Logged tiny/large/ceiling selections still need interval-three-second owned-player captures.

## Cost and remaining gates
Materials tests check actual canonical albedo/roughness, cache reuse and unknown-ID rejection; new Unity tests/scenes are not executed yet. Fresh exact-source CI must validate isolation, resource cycles and all owned/integration targets. Review measured same-endpoint resources without calling three short process-wide samples two-hour world-memory proof. Fix the recorded visual defects, satisfy every acceptance item, complete metadata, close open-to-closed, merge master, then PR + auto-merge and verify closure on master.
