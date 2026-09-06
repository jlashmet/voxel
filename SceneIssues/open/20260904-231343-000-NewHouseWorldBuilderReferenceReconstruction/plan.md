# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Supplied assets under `Assets/Textures/Stylized/experiment1/` are optional; normal-pipeline original/generated textures are allowed. Similar style or green CI is not completion.

## Correct-reference evidence
Exact run `34007464553` on source `36df249f1ecec8d9e21863ba340da7aafa0db1fd` verified/preserved the pinned reference and restored the house authoring regressions. Direct inspection established a tall ornate three-register house: pale stone ground storey; central round-arched timber portal and narrow arched side windows; timber/plaster middle storey with one blue-shuttered arched window; steep blue front gable with a smaller arched window; lower transverse blue roof shoulders; swept eaves; compact warm crest; left chimney; flower boxes/ivy; blue-gold banner and bracketed sign. No garage, driveway, dormer, porch roof, or visible gutter requirement.

## Iteration 1 result — rejected visually
Source `4932e73c4805133f3c53f5691bd08e4c5a766318`, exact request `5af2ef99c9900cd8ecb7c1430a8d463aa12a329c`, run `34011602334` passed automatic module validation and standalone replay. Target `frame_001_t010.0.png`, front-left `frame_002_t020.0.png`, and rear-right `frame_003_t030.0.png` were directly inspected against the pinned input.

Classification: **prototype/blockout quality — visual rejection**. The front gable roof is a full-depth monolithic wedge instead of a shallow steep front gable over lower transverse shoulders; rear-right makes the excess roof volume unambiguous. The crest is a giant pole/tower rather than a compact ornament. Middle/gable openings and blue shutters are oversized and slab-like; timber belts/muntins are too coarse; banner/sign lack recognizable gold emblems; ivy reads as separated dark dots rather than connected foliage. These are existing unchecked roof/opening/detail/material tasks, not new scope.

## Selected next experiment
Hypothesis A remains dominant: roof depth and oversized facade assemblies create most silhouette/composition error. Hypothesis B (framing/background) is secondary. Iteration 2 will first make the steep gable shallow, expose the lower transverse roof shoulders, shrink the crest and openings, thin timber framing, add normal-pipeline gold ornament detail to banner/sign, cluster foliage, and use a broad muted sand plate. Keep the proven 10/20/30 target/front-left/rear-right evidence timing. If the same silhouette symptom survives this materially different roof fix, isolate its geometry cause before another broad change.

## Ownership / remaining gates
`Assets/Game/WorldBuilder` owns reusable house authoring; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering stays semantic-free. Continue exact-SHA built-player compare/correct cycles until very close and production-quality, then complete every `tasks.md` item, current-master compatibility, close `open/`→`closed/`, PR + auto-merge, and required `affected` gate. Never use `pending/` or push directly to master.
