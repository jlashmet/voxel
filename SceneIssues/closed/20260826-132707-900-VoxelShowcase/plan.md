# Plan

## Evidence
- `screenshot-001.png` marks four separate terrain regions that look coarser than their surroundings; `screenshot-002.png` shows the transient artifact gone a few seconds later.
- Runtime visibility previously collected LOD rings independently, allowing drawable coarse parents and partial finer coverage to coexist while streaming converged.
- The existing active-coverage contract requires atomic parent-to-children replacement rather than overlapping ownership.

## Hypotheses / result
1. **Cross-ring coarse/fine overlap during streaming — confirmed.** A production visibility-selector regression reproduced partial finer coverage while a coarse fallback was still drawable.
2. **Static material/shader defect — falsified.** The symptom is transient and hierarchical ownership removes it without shader/material changes.
3. **Neighbor/transition rebuild defect — unsupported.** The marked regions were interior coarse patches rather than persistent local seams.

## Selected fix
- Gate the scheduler's cross-ring visible set through hierarchical LOD ownership.
- Keep a drawable coarse parent while finer coverage is incomplete; replace it only when all required in-band visible children have current ready or known-empty proof.
- Reuse scheduler-owned scratch collections so the per-frame path adds no managed allocation.

## Regression / verification
- `VoxelEngine.Tests.EditMode.SurfaceLodVisibilitySelectorTests` passed on the exact feature state `aff84746021e3afd34298befc34d5373bbb18b58`.
- Final targeted request `e77163ea06d21f7abcc013011db95a259c0436d8` (run `33028636241`) passed the requested PlayMode test, saved-camera real-player replay, artifact upload, and final `ci/single-test` publication.
- All four original marked regions are clear in the final replay; `verification-final.png` records the verified frame.

## Blast radius / cost
- Solid-terrain visibility selection only; no storage, water, shader, material, collision, or mesh-generation changes.
- Filtering remains O(visible candidates × fixed LOD depth) and reuses persistent lists/sets; no new per-frame GC allocation.
