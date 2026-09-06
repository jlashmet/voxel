# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Supplied assets under `Assets/Textures/Stylized/experiment1/` are optional; normal-pipeline original/generated textures are allowed. Similar style or green CI is not completion.

## Correct-reference evidence
Exact run `34007464553` on source `36df249f1ecec8d9e21863ba340da7aafa0db1fd` verified/preserved the pinned reference and restored the house authoring regressions. Direct inspection established a tall ornate three-register house: pale stone ground storey; central round-arched timber portal and narrow arched side windows; timber/plaster middle storey with one blue-shuttered arched window; steep blue front gable with a smaller arched window; lower transverse blue roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign. No garage, driveway, dormer, porch roof, or visible gutter requirement.

## Iteration history
Iteration 1 (`4932e73c...`, run `34011602334`) was **prototype/blockout quality**: full-depth monolithic roof, giant crest, coarse openings/framing, flat heraldry, dotted ivy.

Iteration 2 (request `b4aaf777...`, run `34014789739`) was still **prototype/blockout quality**: shallower roof but nearly full-width A-frame, blocky crest, oversized flat banner/sign symbols, coarse facade depth, sparse planting, unfinished rear.

Iteration 3 (feature `de6039cb...`, request `a16d2bc3143abd42b7726c95f6f22a9a78d7b7f6`, run `34017908101`) completed successfully and preserved the exact reference plus target/front-left/rear-right captures. Direct inspection still rejects it as **prototype/blockout quality**. The front vocabulary improved, but the body remains too broad/flat, the steep blue roof still dominates, obsolete low side-roof slabs remain visible, the crest/banner/sign are still primitive, and the front-left audit exposes unfinished box-like side walls. Green automation does not satisfy visual acceptance.

## Root cause isolate and selected fix
The same silhouette/roof symptom survived two materially different fixes, so broad additive tuning is stopped. The discriminating isolate is the authoring order: `NewHouseReferenceRefinement.AuthorHouse` called the base `NewHouseReferenceAuthoring.AuthorHouse` and then layered corrective geometry over base roof helpers. The base full upper roof plus low side-wing roof remained authoritative geometry underneath the patches, so the result could not converge cleanly.

Selected fix: explicitly remove the complete conflicting upper roof volume and obsolete low outside wing volumes, then rebuild one low transverse shoulder roof plus one narrow shallow portrait gable from production voxel authoring. Rebuild the chimney after the carve, retain translation/site separation, and keep facade/audit refinement reusable. Regression now requires a carve spanning the complete old roof footprint plus removal of the low outside wings. Next gate is exact-SHA built-player evidence; if the replacement roof still misses the pinned silhouette, inspect that exact geometry before further cosmetic work.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable house authoring/refinement; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering stays semantic-free. Continue exact-SHA built-player compare/correct cycles until very close and production-quality, then complete every `tasks.md` item, merge current `origin/master`, close `open/`→`closed/`, PR + auto-merge, and required `affected` gate. Never use `pending/` or push directly to master.
