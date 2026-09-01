# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Deliver a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed curved Dragon. Runtime world truth must be only discrete authored voxels; source triangles are Editor/presentation input only. Shared importer/codec/replay/metrics remain mesh-agnostic under Structures; Dragon/source/palette/comparison policy remains Showcase composition. Independent non-dragon reuse is covered.

Built visual proof uses the module-owned standalone-player fixture with the matched `Mesh -> Voxels` exhibit and semantic ten-view capture sequence. Top-level VoxelShowcase remains integration evidence, not the feature fixture.

## Proven state / blockers
Recovered source: zip SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL 2,144,152 triangles; deterministic support-free OBJ 29,734 triangles / SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Exact reconstruction passed CI `33451165954`. Commercial-use permission is recorded, but original upstream URL, author, and named license text remain unavailable external acceptance blockers; do not invent them.

Exact bake generation passed CI `33451568424`: 98,100 cells, 99×107×107, 0.30 source units/voxel, 594 sparse bricks, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Runtime transport SHA `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961` preserves that canonical identity. Anatomy/fail-closed regressions exist.

Exact-parent run `33476063393` proved the corrected reusable fidelity metric and required module EditMode gates on feature SHA `645b7f37979103f4a1d8b57278f786469a6d2356`: requested pinned-source fidelity passed; automatic `MountainDragonBakedArtifactTests`, pinned-source fidelity, and independent box reuse all passed. The metric now bounds query samples while measuring against the full opposite surface / continuous source triangles, avoiding the prior sparse-sample-spacing defect.

Exact-parent run `33473139368` proved `MountainDragonBakedArtifactTests` placement and top-level VoxelShowcase standalone replay green after the editor assembly-boundary repair.

## Current work
The module-owned fixture under `Assets/Game/Composition/Showcase/Validation/MountainDragonVoxelization/` contains the split-screen matched source/voxel exhibit, production `ShowcaseWorld` voxel side, presentation-only source mesh with colliders removed, equivalent pose/scale/orientation/terrain contact, production rendering/material rules, semantic ten-view capture plan, and placement/runtime-memory logs.

Two built-player failures with the same missing-readiness/capture symptom were isolated before another fix, per issue protocol. Run `33476063393` failed at 4,096 bricks during `ShowcaseWorld.GenerateRegionBlocking`. After the materially different 16,384-brick fixture change, exact-parent run `33477581134` completed terrain and failed later inside `BakedVoxelStructure.ReplayTo` while `PlaceMountainDragon` materialized Dragon blocks. Focused and automatic EditMode tests remained green in both runs. This minimal repro proves the defect is the fixture bypassing production storage sizing, not script binding, rendering, fidelity, or generic structure replay.

Production `VoxelShowcase` derives mixed-brick capacity from `DeviceTierBudget.GetForTier(DeviceTierBudget.Detect()).BrickPoolCapacity`, converts bytes with `VoxelEngineBootstrap.ClampMixedBrickCapacityToBudget`, and passes the same byte ceiling into `ShowcaseWorld`. Commit `89bf1837fba3c555bf6fab873c70c6bd5839a899` makes the module fixture use that same semantic tier-budget path and removes the guessed 4k/16k slot policy. Shared storage/runtime behavior is unchanged.

Exact-parent run `33479919740` did not exercise that runtime fix because Unity stopped at compilation: `MountainDragonVoxelValidationShowcase.cs` referenced `VoxelEngine.Tiering.Api` while `Game.Composition.Showcase.asmdef` lacked the corresponding assembly dependency. Commit `092daab402eee78b5b944fd8bf16129afc18aa11` adds only the existing `VoxelEngine.Tiering.Api` asmdef reference. This is a compile-boundary repair, not a third runtime capacity change; the production-tier-budget behavior still requires its first executable validation.

A separate composition defect remains: `VoxelShowcase.TryPlaceSelectedStructure` still requires optional inspector `m_MountainDragonVoxelBake` and generic-codec decoding, while `MountainDragonBakedArtifact.Load()` already owns the pinned runtime transport. Repair this in Showcase composition only; do not teach the shared codec Dragon/MDVP policy.

## Next gates
1. Validate the production-tier-budget module fixture on the current exact feature SHA through `ci-test/fixes/agent-1`; inspect all ten built-player captures and required logs directly.
2. Repair VoxelShowcase placement mode to consume `MountainDragonBakedArtifact.Load()` through Showcase composition, then validate one-shot placement/world truth.
3. Add/validate destruction + rendered/collision truth after voxel edits and record runtime/blast-radius evidence.
4. Continue exact-source provenance search as an external blocker without weakening acceptance.
5. After every acceptance item is complete, merge current master, run final exact-SHA focused/module-player gates, inspect captures, close the issue, and non-force promote the exact feature head to master.
