# New House WorldBuilder Implementation Plan

## Binding objective / reference
Recreate `Assets/Textures/Stylized/experiment1/house/10dddef5-de0a-4153-9c09-b1e8016830db.png` as closely as possible through the production WorldBuilder, Structures authoring, voxel storage/meshing/rendering, and normal material/texture systems. Pinned Git blob: `6d87b08d4c7c9bddc1705c0f34343aa79bc18423`. Supplied assets under `Assets/Textures/Stylized/experiment1/` are optional; normal-pipeline original/generated textures are allowed. Similar style or green CI is not completion.

## Correct-reference evidence and observed mismatch
Exact run `34007464553` on source `36df249f1ecec8d9e21863ba340da7aafa0db1fd` passed the pinned-reference hash regression, restored all three `NewHouseReferenceAuthoringTests`, module validation, and standalone replay. Its artifact preserved the exact reference bytes for direct inspection.

The pinned image is a tall ornate three-register house: pale stone ground storey with a large central round-arched timber portal and two narrow arched windows; a timber/plaster middle storey with one large arched blue-shuttered window; a dominant very steep blue front gable with a second arched window; swept/flared eaves; large warm crest finial; tall left chimney; flower boxes and heavy ivy; a blue hanging banner at left and bracketed sign at right. It is near-frontal, tightly portrait-framed, and has only a compact grounded base/steps—no garage, driveway, dormer, porch roof, or visible gutter requirement.

The prior render is materially wrong: too squat/wide, simple rectangular entry/lower openings, oversized four-panel middle bank, broad blank gable, straight/simple roof edge, tiny ridge ornaments, bright lawn/long path, sparse vegetation, and missing banner/sign.

## Selected iteration / hypotheses
Hypothesis A: wrong-reference massing/opening/detail decisions dominate visual error. Hypothesis B: framing/background presentation compounds it.

Iteration 1 is now authored on feature head `83f53d24de23e3f58d5f1fc4212a92e382366f2`: 84x56 footprint; taller 8/34/36 storey stack; 48-voxel steep gable; arched portal and three arched window roles; blue middle shutters; swept eave tips; single tall crest; corrected chimney; banner/sign; denser ivy/flowers; compact dirt/stone site; tighter portrait camera; and target/front-left/rear-right timing aligned to standalone ~10/20/30s captures.

## Ownership / gates
`Assets/Game/WorldBuilder` owns reusable/config-driven house authoring; site/camera/light remain validation composition. `Assets/Game/Materials` owns presentation; Rendering remains semantic-free. The supported production CPU A/B path is still used for this proof while GPU restoration is separate.

Next: exact-SHA module + standalone run, direct comparison to the pinned image, record remaining concrete defects, and iterate structural issues before cosmetics. Then finish every `tasks.md` item, production-quality/audit evidence, current-master compatibility, close `open/`→`closed/`, PR + auto-merge, and required `affected` gate. Never use `pending/` or push directly to master.
