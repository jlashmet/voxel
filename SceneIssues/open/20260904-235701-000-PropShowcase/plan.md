# PropShowcase plan

## Observed state
Fetched and synchronized with `origin/master`; the branch was exact-SHA validated at `8f2e56aacc17fb48ffdf7c3d5d402f360e5b0549` by transport `4328f121d5f5b0c79388a5ed2284fb4bc3dba14c`, workflow run `33991512115`. That run passed repository-owned module validation and standalone SceneIssue replay. `origin/master` subsequently advanced to `3654c13f72ed157c53b340443a766795d772f596` and must be merged before final promotion.

The deterministic independently previewable set is **529 entries**: 440 stable decoration identities, 25 reusable descriptor factories, 8 mine-cave kinds, 8 natural-cave kinds, and 48 `WorldObjectKind` values. Parameter choices remain variants rather than separate identities. Exclusions are building-scale structures, terrain, characters, VFX-only records, raw materials, duplicate mechanism presets already represented by `WorldObjectKind`, and scene/program catalogues that only compose the same canonical primitives.

## Selected architecture
`Game.Structures.Runtime` owns canonical catalogue enumeration and production realization/presentation adapters. `PropShowcase` owns only browser UI, lifecycle, neutral support environment, and camera framing. Voxel-backed entries use production authoring/rendering; procedural/thin/world-object entries use production presenters. Presenter-owned geometry and support objects now live on an independent world-space presentation root rather than the camera.

## Visual discriminator
Direct inspection of final exact built-player evidence rejected visual acceptance despite green automation: frame `SceneIssue/Screenshots/frame_002_t020.0.png` selects `Merchant Sign` (`decoration:19`, `ThinSurface`, presenter owned=1) while the right preview is visually blank.

Two plausible causes remain: (1) the fixed camera position views a one-sided wall-mounted thin surface from its semantic back face, so backface culling hides it; or (2) the wall support plane is placed/oriented in front of the surface and occludes it. The next discriminating step is to inspect the production thin-surface vertex winding/normal contract relative to `DecorationPlacement.Facing`, then align showcase camera/support placement to that semantic front/back relationship without adding per-prop policy.

## Affected modules / validation
Player-visible runtime changes affect Structures and Composition/Showcase. Structures owns `Validation/PropShowcaseProductionValidation.*`; Showcase owns `Validation/PropShowcaseRuntimeValidation.*`. `Assets/Scenes/PropShowcase.*` is integration evidence only. Focused catalogue tests cover 529-entry parity, uniqueness, production realization, canonical enumeration, and procedural geometry.

## Remaining gates
Fix the demonstrated wall-thin visibility defect -> merge current master -> exact-SHA targeted validation -> inspect representative standalone frames at `production-quality` -> complete stress/cost and checklist evidence -> open-to-closed bookkeeping -> final PR + auto-merge -> required `affected` gate -> verify the closed SceneIssue on master.
