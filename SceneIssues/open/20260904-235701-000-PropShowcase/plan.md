# PropShowcase plan

## Observed state
Fetched and synchronized with `origin/master`; the branch is currently based on master `a180749ed7c00d28bed6661fc9a3da4c9a9b61fc`. The production prop vocabulary is split across Structures runtime sources rather than one enumerable API, confirming the need for a narrow production-owned query boundary.

The deterministic independently previewable set is **529 entries**: 440 stable decoration identities, 25 reusable descriptor factories, 8 mine-cave kinds, 8 natural-cave kinds, and 48 `WorldObjectKind` values. Parameter choices such as work-table length remain variants of one production factory rather than separate identities. Exclusions are building-scale structures, terrain, characters, VFX-only records, raw materials, duplicate mechanism presets already represented by `WorldObjectKind`, and scene/program catalogues that only compose the same canonical primitives.

## Selected architecture
`Game.Structures.Runtime` owns deterministic catalogue enumeration plus the realization/presentation adapters. Canonical enums/catalogues/presets remain identity authority; `PropShowcase` owns no second content list. The missing `DecorationProceduralMeshRequest` visual consumer is supplied as a reusable Structures presenter using canonical geometry and material identity, with module-local validation proving the independent production consumer.

`Assets/Game/Composition/Showcase/SceneRuntime/PropShowcase.cs` is the integration controller: left-side browser UI, selection lifecycle, neutral support environment, production material composition, and automatic camera framing. Voxel-backed entries use production authoring/rendering; procedural/thin/world-object entries use their production presenters.

Final source review found one acceptance-critical composition defect: presenter roots and support environment were hosted on the camera object, so framing moved camera-relative geometry. The selected fix is a separate world-space presentation root for all presenter-owned geometry, support planes, and preview lighting while the camera moves independently.

## Affected modules / validation
Player-visible runtime changes affect Structures and Composition/Showcase. Structures owns `Validation/PropShowcaseProductionValidation.*`; Showcase owns `Validation/PropShowcaseRuntimeValidation.*`. `Assets/Scenes/PropShowcase.*` is integration evidence only. Focused catalogue tests cover 529-entry parity, uniqueness, production realization, canonical enumeration, and procedural geometry.

The first final exact run proved all automatic module validation but the standalone replay request itself used an invalid SceneIssue identifier instead of the required `SceneIssues/.../issue.json` path. A corrected same-SHA retry was admitted without replacing queued/running work; it is superseded for final acceptance by the world-space-root fix and must finish before a new exact request is issued.

## Remaining gates
Let the admitted CI request finish untouched -> merge newer master if required -> exact-SHA validation of the world-space-root head -> inspect standalone built-player screenshots and logs at `production-quality` -> record stress/switch-cost evidence -> complete checklist and resolution fields -> move open to closed -> PR + auto-merge -> required `affected` gate -> verify closed issue on master.
