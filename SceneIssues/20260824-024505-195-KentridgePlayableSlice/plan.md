# SceneIssue 024505 — floating structure above Kentridge square

## Goal

Identify the enormous apparently floating structure above the Kentridge square at the saved player
camera pose and make the authored world read as coherent architecture. If the immediately preceding
Hightown catalogue-ownership fix already removed the same cross-town contamination, prove that
causal relationship rather than adding a second speculative production change.

## Scope and constraints

- Replay the single saved 1637×1140 `Kentridge Player Camera` pose at 58-degree FOV; there are no
  circles, so inspect the full central floating silhouette and surrounding square.
- Preserve authoritative CPU voxel occupancy and the GPU presentation pipeline.
- Identify the responsible catalogue definition/placement before changing production code.
- Continue on `fixes`; use Unity only through `tools/unity-run.sh` and remove the temporary replay
  resource before committing.
- Record every experiment immediately and add a focused invariant for the proven cause.

## Acceptance criteria

1. Exact-pose current-head replay establishes whether the floating structure remains after the
   prior settlement-catalogue ownership fix.
2. Authoritative catalogue evidence identifies the original structure and why it appeared there.
3. A focused regression prevents that invalid placement or proves the existing boundary regression
   covers it.
4. A clean production-player replay at the saved pose shows no unexplained floating structure and
   no regression to the surrounding square/buildings.
5. Evidence and issue resolution are committed separately and pushed before moving on.

## Work

- [x] Read the issue metadata and inspect its sole screenshot/full-frame defect.
- [x] Replay the exact saved pose on current `fixes`.
- [x] Identify the responsible authored placement and focused regression.
- [x] Run affected tests and final production-player replay.
- [x] Review, commit/push, and resolve the manifest separately.

## Findings

- The original frame shows a multi-storey gabled shell floating directly above the paved square;
  nearby ground-level buildings and street furniture are normally supported.
- The saved camera is inside the authored magic shop. Its ordered shell first meets the centre ray
  2.2 metres away at voxel `(1017,268,604)`. The pre-fix Hightown pass placed district-terrace
  carves/fills, anonymous fabric, and a terrace dwelling through the magic-shop envelope; commit
  `0459ec9a` removed those cross-town overlaps. Current post-opening replay shows one continuous,
  supported magic-shop wall rather than the captured floating fragments.
- A high southwest overview and a closer west-side production-player replay show the repaired
  building tied into its stone base and neighboring court. No detached shell remains. The first
  south-frontage pose was occluded by terrain and is retained as an inconclusive experiment rather
  than being treated as evidence.
- With all temporary diagnostics and replay resources removed, the permanent
  `HightownVoxelCatalogueDoesNotEmitSouthOfTheCountryMidpoint` regression passed exactly 1/1 in
  local EditMode. The production renderer remains the GPU voxel pipeline; the repaired CPU data is
  authoritative occupancy only.
