# PropShowcase plan

## Observed state
Fetched `origin/master` at `6bd0992630ae27f2e30ebc32d65ba098cf987d25` and fast-forwarded `fixes/agent-9` before feature work. The production prop vocabulary is split across Structures runtime sources rather than one enumerable API, so hypothesis 2 is confirmed.

The deterministic independently previewable set is **529 entries**:
- 440 stable decoration identities: base 1–114, registered expansions through 400, guild-signature 401–440;
- 25 reusable descriptor factories across legacy room/furniture, dining, lighting, storage, martial, and textile presets;
- 8 mine-cave decoration kinds;
- 8 natural-cave decoration kinds;
- 48 `WorldObjectKind` values.

The earlier discovery count of 534 over-counted five parameter choices as if they were separately registered canonical identities. Parameters such as work-table length remain variants of one production factory and do not become independent browser identities. Intentional exclusions are building-scale structures, terrain, characters, VFX-only records, raw materials, world-object mechanism presets already represented by their `WorldObjectKind`, and scene/program catalogues that only compose the same canonical primitives. Those are aliases/compositions, not additional independent prop identities.

## Selected architecture
Add the narrowest production-owned read/realization boundary under `Game.Structures.Runtime`: a deterministic catalogue query that derives entries from the canonical catalogues/presets and a production authoring/presentation dispatcher that invokes existing emitters/backends. `PropShowcase` owns no second identity list.

The dedicated integration controller belongs under `Assets/Game/Composition/Showcase/SceneRuntime`, whose existing assembly already consumes `Game.Structures.Runtime`, world-object runtime, rendering, materials, and ShowcaseWorld composition. The controller will rebuild a bounded production `ShowcaseWorld` preview per selection using `ShowcaseWorld.CreateStructureAuthoringSession(...)` plus the existing rendering composition where the selected entry is structure-authored, and the existing production world-object/presentation sinks for their owned backends. This gives deterministic disposal, production voxel/material rendering, stable grounding, and semantic bounds for automatic camera framing. Thin/procedural/world-object presentation must use production presentation semantics; no preview-only primitive/fake fallback is acceptable.

A backend audit found one required production gap: `ThinSurface` already has `DecorationThinSurfaceBatchBuilder`, but `DecorationProceduralMeshRequest` is only generated and has no reusable visual consumer anywhere in the repository. Because the binding acceptance requires procedural-mesh entries to render through production semantics, this feature must add the narrow reusable Structures presentation consumer and prove it in Structures validation rather than implement a preview-only substitute.

## Affected modules / validation
Production changes: `Assets/Game/Structures/Runtime`, module-local `Assets/Game/Structures/Tests`, and a new focused `Assets/Game/Structures/Validation/` scene because Structures currently lacks a suitable player-visible validation surface. Integration changes: `Assets/Game/Composition/Showcase/SceneRuntime`, `Assets/Game/Composition/Showcase/Validation`, `Assets/Scenes/PropShowcase.*`, and `ProjectSettings/EditorBuildSettings.asset`. The Showcase module is now an affected player-visible/runtime module because it owns the dedicated scene controller, so its own focused validation surface will exercise browser/selection integration separately from the top-level `PropShowcase` scene.

## Remaining gates
Implement catalogue + realization/presentation boundary -> parity/switching tests -> Structures and Showcase module-local validation -> PropShowcase UI/selection/framing -> exact-SHA targeted CI -> inspect durable player screenshots at `production-quality` -> stress/resource evidence -> complete every task -> open→closed bookkeeping -> merge latest master -> PR + auto-merge -> required `affected` gate -> verify closed issue on master.
