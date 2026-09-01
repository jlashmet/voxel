# Plan — VoxelShowcase Dragon Mesh Voxelization

## Acceptance / ownership
Deliver a reusable conventional triangle-mesh → canonical sparse-voxel authoring path, proved on the recovered detailed curved Dragon. Runtime world truth must be only discrete authored voxels; source triangles are Editor/presentation input only. Shared importer/codec/replay/metrics remain mesh-agnostic under Structures; Dragon/source/palette/comparison policy remains Showcase composition. Independent non-dragon reuse is covered.

Built visual proof uses the module-owned standalone-player fixture with the matched `Mesh -> Voxels` exhibit and semantic ten-view capture sequence. Top-level VoxelShowcase remains integration evidence, not the feature fixture.

## Proven state / blockers
Recovered source: zip SHA `f48cab5ab5b7edf6a84cc7bf14797c73d0ac61bf597ef76a587589a4522aeb0f`; contained STL 2,144,152 triangles; deterministic support-free OBJ 29,734 triangles / SHA `f1f44d59f7d9c775b600ac0b9ad066a15a3c652bf685a12b2344b8c383ff12b1`. Exact reconstruction passed CI `33451165954`. Commercial-use permission is recorded, but original upstream URL, author, and named license text remain unavailable external acceptance blockers. A 2026-09-01 public search for both exact source hashes and `mountain_dragon_supported.stl` found no exact match; similar public Mountain Dragon listings are not treated as provenance for these bytes.

Exact bake generation passed CI `33451568424`: 98,100 cells, 99×107×107, 0.30 source units/voxel, 594 sparse bricks, canonical SHA `83370421048606be2dc658315ec9acc2cae39d2a7a20011151d7d561267bec41`. Runtime transport SHA `758612c8b63316e3757a7695bfdb07f99ee5709f3706c504688d657017ecc961` preserves that canonical identity. Anatomy/fail-closed regressions exist.

Exact-parent run `33476063393` proved the corrected reusable fidelity metric and required module EditMode gates on feature SHA `645b7f37979103f4a1d8b57278f786469a6d2356`. Exact run `33485072972` proved the matched ten-view built comparison and passed direct human visual inspection.

The earlier destruction proof compile failure in run `33488495875` was isolated to an illegal validation assembly dependency. Commit `51586ddd50d0c8af4e0089242a103290ecb5bc4d` removed that dependency and kept the proof inside Showcase composition by calling the public `ShowcaseWorld.Explode` seam directly.

## Current exact-CI evidence
Exact targeted-CI request `445e50b76da60807667f54b7a581e6f4d90ecf56` completed successfully as run `33490519425`. Requested Dragon artifact tests, automatic module EditMode validation, the Dragon built-player fixture, canonical Kentridge built integration, top-level VoxelShowcase replay, screenshot previews, artifact upload, and final status all passed.

The Dragon player placed all 98,100 voxels through `ShowcaseWorld.PlaceBakedMeshStructure` in 60.122 ms. It recorded storage usage 35,656,896 / 4,999,999,488 bytes with `storage_under_pressure=False`. Settled renderer diagnostics during the final capture interval remained low-cost (worker p95 ~0.08 ms, residency p95 ~0.007 ms, admission frame ~0.09 ms). The checked-in Base64 runtime transport is 15,801 bytes. Durable evidence is recorded in `verification-ci-33490519425.txt`.

The same exact player then performed the canonical destruction proof after all ten pristine comparison captures: target `272,278,215` changed from material 6 to empty, 1,144 voxels changed, collision changed blocked→unblocked, and source enabled colliders remained zero. Post-edit frames were inspected directly and the player exited without assertion/runtime failure.

The VoxelShowcase placement composition defect is already repaired by commit `b4b87cefcc1174d9c43cebc35b62d3eb62cc2def`: the optional inspector TextAsset dependency was removed and `TryPlaceSelectedStructure` now obtains the pinned transport through `MountainDragonBakedArtifact.Load()`. The shared codec remains mesh/Dragon-agnostic. Independent router tests prove one-shot selection/control ownership, but the top-level built replay does not synthesize B/Space, so end-to-end built input placement remains deliberately unchecked.

## Blast radius / merge state
A fresh compare against current `master` (`b4d8c1978d0c9b4be9239de8c1108ce95d1aa83e`) shows the long-lived feature branch is substantially diverged (feature ahead 272 / behind 242 from merge base `142b1134bd9d6a9eb1d60e55a296afaf6d9e7b3e`). Feature-owned production changes remain concentrated in Structures mesh import, Editor mesh adapter/tooling, Showcase composition/validation, tests, and the pinned resource bake; no intentional change weakens global voxel scale, terrain/building storage semantics, collision rules, or device budgets. Current master must be merged normally before the final exact-SHA gates and promotion.

## Next gates
1. Keep exact upstream provenance as an external blocker; do not infer or weaken acceptance.
2. If practical without adding non-acceptance harness machinery, directly exercise one-shot VoxelShowcase B/Space placement in a built path; otherwise leave it explicitly unproven rather than substituting editor-only evidence.
3. Immediately before final exact-SHA validation, merge current `origin/master` into `fixes/agent-1`, resolve only in-scope conflicts, and re-run required focused/module-player gates through `ci-test/fixes/agent-1`.
4. Inspect all final captures after that merge and confirm no startup/runtime exceptions.
5. Only when every acceptance item, including provenance, is complete: set fixed metadata, move this assignment directly `open` → `closed`, fetch/merge any newly advanced master, and non-force push that exact feature head to `origin/master`.
