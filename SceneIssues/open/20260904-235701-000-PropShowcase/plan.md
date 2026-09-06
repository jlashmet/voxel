# PropShowcase plan

## Acceptance and ownership
Browse every independently previewable production prop exactly once, render its production realization, retire prior state, and prove useful framing/materials/contact plus bounded switching through exact standalone-player evidence. Only production-quality visuals pass; no gate or checkbox is waived.

Current set: 529 entries (440 decorations, 25 reusable presets, 8 mine-cave kinds, 8 natural-cave kinds, 48 world-object kinds). Parameters are variants; buildings, terrain, characters, VFX-only records, raw materials and duplicate aliases are excluded.

Structures owns enumeration/presenters and PropShowcaseProductionValidation. SceneRuntime owns the browser, resource probe and PropShowcaseMaterialValidation. Materials owns its shared material adapter, scalar/cache regressions and PropMaterialCompositionValidation. All local scenes invoke production paths; parent/top-level showcases cannot substitute for module ownership. Python CI orchestration is headless and uses subprocess regressions.

## Current source and immutable CI
Resumed `9697d365c986a070f6a78db2af99e8c0f449df15`, including material ownership, teardown isolation and resource instrumentation. This revision additionally corrects the demonstrated trapdoor mount. Master was read at `356b2e0e4d2818901c73bbc6b1788f8d6850356d`; guides are unchanged. Shell fetch failed DNS; remote GitHub refs/files were read instead. Final master merge remains outstanding.

User-named request `e83a7fd822dab1c40d59f0f84ccd65937071fd28` / run `34003328146` completed FAILURE at 2026-09-06T02:37:23Z without replacement. Four Showcase cases passed, but persistent orchestration started the next PlayMode phase before teardown completed. Standalone replay passed. See `review-34003328146.md`.

Request `57ab96ca508e70a4d768aa5ddefc6b7343bb531c` / run `34007356710` was already queued for source `9697d365` when the CI ref was rechecked; preserve it. It does not contain the subsequent trapdoor correction. An unreferenced candidate request `e23d0cf4` was never assigned to a branch or submitted.

## Visual discriminator and selected fix
Production shading and table/sign camera corrections are visible, but overall output remains prototype/blockout: featureless Merchant Sign, unsupported Forge Hearth bars, blue Door/Trapdoor slabs, and initial READY before anvil publication.

For the upright trapdoor, hypotheses were a bad presentation rotation versus incorrect baseline mount. Its closed production plan has zero rotation. The catalogue instead groups Trapdoor with vertical Door dimensions `(12,24,4)` and the realizer defaults to wall facing `+Z`. Correct only the neutral hatch baseline to `(12,4,24)` and floor normal `+Y`. Existing open/close pitch and explicit authored world placements remain unchanged. Six behavioral cases cover canonical query reuse, deterministic realization, unchanged upright doors and open/close plans. The Structures-owned scene now presents the real hatch and asserts its rendered bounds. Blue primitive proxy art is a separate unfinished production defect, not cured by this mount fix.

## Cost and remaining gates
Mount correction adds no per-frame work or allocation; it preserves baseline volume. New tests/scenario are not executed. Preserve active CI, obtain fresh exact-source validation afterward, inspect every mount/backend capture, fix recorded finish/loading defects, review actual resource measurements without claiming two-hour stability, complete all acceptance/metadata, close open-to-closed, merge master, then PR + auto-merge and verify closure on master.
