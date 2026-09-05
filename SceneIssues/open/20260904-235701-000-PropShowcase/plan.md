# PropShowcase plan

## Observed state
The branch is synchronized with current `origin/master` `cd77b927dbe463171f6cef86bb268a31ae8df4e4` through sync PR #307. The previous exact-SHA source `8f2e56aacc17fb48ffdf7c3d5d402f360e5b0549` was mechanically green via transport `4328f121d5f5b0c79388a5ed2284fb4bc3dba14c`, workflow run `33991512115`: repository-owned module validation and standalone SceneIssue replay both passed. Direct built-player review nevertheless rejected visual acceptance because one wall-thin representative was not visible.

The deterministic independently previewable set is **529 entries**: 440 stable decoration identities, 25 reusable descriptor factories, 8 mine-cave kinds, 8 natural-cave kinds, and 48 `WorldObjectKind` values. Parameter choices remain variants rather than separate identities. Exclusions are building-scale structures, terrain, characters, VFX-only records, raw materials, duplicate mechanism presets already represented by `WorldObjectKind`, and scene/program catalogues that only compose the same canonical primitives.

## Selected architecture
`Game.Structures.Runtime` owns canonical catalogue enumeration and production realization/presentation adapters. `PropShowcase` owns only browser UI, lifecycle, neutral support environment, and camera framing. Voxel-backed entries use production authoring/rendering; procedural/thin/world-object entries use production presenters. Presenter-owned geometry and support objects live on an independent world-space presentation root rather than the camera.

## Visual discriminator and selected fix
Frame `SceneIssue/Screenshots/frame_002_t020.0.png` from run `33991512115` selects `Merchant Sign` (`decoration:19`, `ThinSurface`, presenter owned=1) while the right preview is visually blank.

The production `DecorationThinSurfaceBatchBuilder` proves the mesh is correct: for `Facing +Z`, its normal and triangle winding face `+Z`. `DecorationShowcaseRealizer` gives wall-mounted entries `+Z`, while the old showcase framing placed the camera on `-Z`, so the camera viewed the semantic back face and backface culling hid the sign. The support surface sits behind the `+Z` thin surface, falsifying support-plane occlusion. The selected fix is composition-only: frame every realization from `FacingOf(realization)` while retaining bounds-derived scale/distance. Commit `e6b159942ee8afe870567c8adf22d41e04ae3028` implements that semantic-front framing; current master was merged afterward.

## Affected modules / validation
Player-visible runtime changes affect Structures and Composition/Showcase. Structures owns `Validation/PropShowcaseProductionValidation.*`; Showcase owns `Validation/PropShowcaseRuntimeValidation.*`. `Assets/Scenes/PropShowcase.*` is integration evidence only. Focused catalogue tests cover 529-entry parity, uniqueness, production realization, canonical enumeration, and procedural geometry.

## Remaining gates
Fresh exact-SHA targeted validation of the synchronized semantic-front head -> inspect wall-thin and all representative standalone frames at `production-quality` -> complete stress/cost and checklist evidence -> open-to-closed bookkeeping -> fetch/merge newer master if it moved -> final PR + auto-merge -> required `affected` gate -> verify the closed SceneIssue on master.
