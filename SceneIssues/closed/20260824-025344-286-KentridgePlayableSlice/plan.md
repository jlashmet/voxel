# SceneIssue 025344 — overlapping Kentridge houses and reversed stair

## Goal

Make the dense Kentridge frontage at the saved player view read as intentionally spaced,
individually coherent buildings, and make every visible stair connect two authored circulation
surfaces in the correct direction.

## Scope and constraints

- Replay the sole saved 1637×1140 `Kentridge Player Camera` pose at 58-degree FOV; there are no
  circles, so inspect the entire crowded frontage, central opening, and stair runs.
- Treat authoritative integer CPU occupancy as world truth and retain the GPU voxel presentation
  pipeline.
- Preserve the authored Kentridge street/plaza/plot topology, role identities, frontage, access,
  deterministic forms, and plot envelopes unless evidence proves the plan itself violates its
  declared spacing/access contract.
- Check placement envelopes and primitive ownership before treating the image as a renderer or
  half-voxel boundary-field defect. The authored-boundary half-cell rule governs curved surface
  samples and cannot explain whole buildings occupying the same volume or a stair facing away from
  its access anchor.
- Continue on `fixes`; run Unity only through `tools/unity-run.sh`, remove temporary replay/probe
  assets before committing, and record every experiment immediately.

## Acceptance criteria

1. Exact-pose current-head replay establishes which reported overlaps and stair defects remain
   after the preceding settlement-ownership fix.
2. Deterministic catalogue evidence names every structure/primitive contributing in the saved view
   and proves the responsible placement, envelope, orientation, or access invariant.
3. A focused regression fails for the proven cause and passes after the smallest architectural fix.
4. The relevant affected Unity tests pass with non-zero execution, and a final production-player
   replay at every captured pose shows coherent spacing and connected, correctly directed stairs.
5. Production/test/evidence and issue resolution are committed separately and `fixes` is pushed.

## Work

- [x] Read the issue metadata and inspect its sole screenshot/full-frame defect.
- [x] Read the binding authoring, placement, connectivity, and half-voxel boundary-field findings.
- [x] Replay the exact saved pose on current `fixes`.
- [x] Identify the overlapping instances and reversed stair from authoritative catalogue data.
- [x] Add regression coverage and implement the smallest proven fix.
- [x] Run affected tests and final production-player replay.
- [x] Review, commit/push, and resolve the manifest separately.

## Findings

- The captured view shows multiple facade/shell masses occupying a very narrow court. A dark stair
  rises toward the central upper opening while lower steps/landings do not form an obvious connected
  route, matching the report of a stair running backward into nothing.
- The authored-boundary contract's half-voxel finding is specific to sign agreement between curved
  occupancy and presentation samples. It requires measured lattice crossings for curved-surface
  defects; it is not a plausible explanation for metre-scale structure overlap or cardinal stair
  orientation. This issue must first be tested as semantic placement/connectivity authoring.
- Exact production-player replay on current `fixes` still shows the crowded structures and central
  reversed/disconnected stair after rendering has converged. The preceding Hightown ownership fix
  did not resolve this distinct Kentridge defect.
- Final per-instance occupancy measurement found 28 named-role/secondary-urban overlap pairs.
  Rebecca House intersects three anonymous fabric instances, its block access stair, and a retaining
  gallery. The same missing reservation affects seven other named buildings, so the fix must enforce
  the settlement's spacing invariant systematically rather than special-case the saved camera.
- The retained regression failed on the first Logan House reservation violation, then passed 1/1
  after the canonical adapter removed conflicting secondary placements using the existing 12 dm
  density-policy spacing. Stable named plots and road/plaza topology are unchanged.
- Exact-pose and southwest-overview production-player replays both completed without harness
  failures. The fixed view has one coherent Rebecca House and no clipped anonymous access stair;
  the overview retains a populated town, named buildings, roads, plaza, paths, lamps, wells, and
  non-conflicting secondary fabric rather than becoming visually hollow.
- The final clean source passed 39 unique affected EditMode tests: urban organization (4),
  Kentridge generation (10), two-town world (9), infrastructure (1), architecture geometry (6),
  campaign realization (3), semantic landmarks (1), and shape-program encoding (5). Every run
  executed a non-zero test count. The infrastructure fixture's old exact count exposed its
  intentional composition dependency and now pins all original stage inventories plus the exact
  conflict-free combined count of 39.
- An additional opening PlayMode fixture was attempted but correctly stopped by the Unity wrapper
  before producing a result when host free memory crossed the binding 8 GB safety floor. It is not
  used as passing evidence; the two completed built-player replays provide the runtime validation.
