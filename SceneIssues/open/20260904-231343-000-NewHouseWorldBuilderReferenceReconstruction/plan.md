# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Similar style or green CI is not completion.

## Correct-reference evidence
Exact run `34007464553` on source `36df249f1ecec8d9e21863ba340da7aafa0db1fd` verified/preserved the pinned reference. It is a tall ornate three-register house: pale stone ground storey; central round-arched timber portal and narrow arched side windows; timber/plaster middle storey with one blue-shuttered arched window; steep blue front gable with a smaller arched window; lower transverse blue roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign. No garage, driveway, dormer, porch roof, or visible gutter requirement.

## Iteration history
Iteration 1 (run `34011602334`) was **prototype/blockout quality**: monolithic roof, giant crest, coarse openings/framing, flat heraldry, dotted ivy.

Iteration 2 (run `34014789739`) was still **prototype/blockout quality**: nearly full-width A-frame, blocky crest, oversized flat banner/sign symbols, coarse facade depth, sparse planting, unfinished rear.

Iteration 3 (request `a16d2bc3143abd42b7726c95f6f22a9a78d7b7f6`, run `34017908101`) remained **prototype/blockout quality**. The front vocabulary improved but conflicting base roof/side-wing geometry survived underneath additive refinement. Root cause isolate selected complete replacement of the conflicting roof composition.

Iteration 4 (request `9a7835a2b69819dc5dd2d218eaa30597045b9640`, run `34020127316`) completed successfully and proved the roof replacement removes the conflicting side mass. Direct built-player inspection still rejects it as **prototype/blockout quality**: the front gable became a large blank plaster triangle, the middle-storey opening collapsed visually, and the upper gable opening disappeared. The cause is authoring order, not another proportion hypothesis: the destructive roof-clear pass runs after the base openings and erases their upper geometry.

## Selected next fix
Keep the replacement roof. Immediately after its destructive carve/rebuild, re-author the two reference-visible upper arched openings and blue shutters through production voxel operations, before facade detail/ornament passes. Add a behavioral ordering regression that observes the roof-clear operation and proves middle-storey glass, gable glass, and shutters are emitted afterward. Do not change camera/materials or broad massing in this experiment.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable house authoring/refinement and module-local validation; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering stays semantic-free. Continue exact-SHA built-player compare/correct cycles until very close and production-quality, then complete every `tasks.md` item, merge current `origin/master`, close `open/`→`closed/`, PR + auto-merge, and required `affected` gate. Never use `pending/` or push directly to master.
