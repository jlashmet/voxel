# Plan

## Evidence
- `screenshot-001.png` marks four separate terrain regions that look coarser than their surroundings.
- `screenshot-002.png` was captured ~5.7 s later from the same camera position but a different rotation; the reported artifact is transient, so it is not treated as a pixel-identical comparison.
- Runtime visibility currently collects each LOD ring independently and concatenates every ready entry. The existing `SurfaceLodActiveCoverage` contract instead requires a complete parent to remain active until all eight current children can replace it atomically.

## Hypotheses / discriminator
1. **Cross-ring coarse/fine overlap during streaming (leading):** independently visible parent and child chunks can coexist while finer coverage converges. A behavioral visibility test must fail before the fix when parent + partial children are offered together.
2. **Static material/shader defect:** inconsistent with a transient report and would not be fixed by hierarchical visibility selection.
3. **Neighbor/transition rebuild defect:** would leave a seam/local boundary symptom rather than multiple coarse-looking interior patches; final saved-camera replay will check every marked region.

## Change
- Gate the scheduler's cross-ring visible set through hierarchical LOD ownership: keep the coarser drawable while finer coverage is partial; replace it only after all eight child cells are represented.
- Reuse scheduler-owned scratch collections so the per-frame visibility path does not add managed allocations.

## Regression / verification
- Add an EditMode behavioral regression for parent + partial children and atomic eight-child replacement through the production visibility selector.
- Run the exact regression plus the assigned saved-camera replay in the single final `ci-test/fixes/agent-4` request.
- Inspect all four original marked regions in the replay artifact and save `verification-final.png`.

## Blast radius / cost
- Solid-terrain visibility selection only; no extraction, storage, water, shader, material, or mesh-generation changes.
- Filtering is O(visible candidates × LOD depth) over four fixed LOD levels and reuses persistent lists/sets; no new per-frame GC allocation.
