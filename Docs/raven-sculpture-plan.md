# High-resolution voxel raven sculpture

## Goal

Add a deterministic, textured raven sculpture that is authored through the canonical voxel
structure API, can be selected as a World Builder voxel-stamp object, and produces a high-resolution
isolated render for visual review.

## Scope and constraints

- Author the raven parametrically; do not check in captured voxel output or a conventional mesh.
- All geometry writes go through `IStructureAuthoringSession` so the voxel cells remain the source
  of truth for rendering, collision, destruction, and persistence.
- Keep generation deterministic and CPU-side. The presentation material palette may add color
  variation, but it must not create authoritative state.
- Use existing game material IDs so the render is textured by material variation without a custom
  renderer.
- Reuse `DecorationVoxelStampBackend` and the coarse floor-mounted `Fountain` family, with a
  stable raven-specific variant ID.
- Validate through the repository's targeted single-test branch and inspect its uploaded PNG before
  considering the work complete.

## Acceptance criteria

- [x] A recognizable perched raven silhouette is present from the isolated three-quarter view:
      hooked beak, brow/eyes, neck, breast, folded wings, layered primary feathers, legs/talons, and
      a fanned tail.
- [x] The sculpture uses at least four deliberately placed material regions for blue-black feather
      variation, beak/talons, eye accents, and cool iridescent highlights.
- [x] The authorer writes enough occupied voxels for a high-resolution sculpt while staying inside
      its declared local bounds and a bounded write budget.
- [x] A well-formed World Builder descriptor and placement route through
      `DecorationVoxelStampBackend`.
- [x] An EditMode regression test checks the descriptor, backend route, bounds, voxel density,
      material diversity, and writes a 1600×1600 PNG into the targeted-test artifact directory.
- [x] The exact targeted test completes successfully in CI and the rendered artifact is visually
      inspected.
- [x] Final diff is reviewed against `AGENTS.md`, `CLAUDE.md`, the constitution, and the
      world-feature-authoring spec.

## Work log

- 2026-08-24: Confirmed the current master already has a deterministic
  `VisualStructureCapture` raster path suitable for isolated asset renders.
- 2026-08-24: Reviewed the unmerged dragon-statue branch only as a pattern. The raven remains a
  separate master-based feature branch and does not depend on those 64 dragon commits.
- 2026-08-24: Implemented the first raven authoring pass entirely with integer ellipsoid and tapered
  stroke sampling. No authoritative raven occupancy depends on GPU output, meshes, randomness, or
  floating-point arithmetic.
- 2026-08-24: Added stable `RAVN` voxel-stamp routing and a focused EditMode visual/invariant test.
  The image is copied to `Artifacts/SingleTest` because the targeted workflow does not upload
  `TestResults/WorldbuildingVisuals`.
- 2026-08-24: First targeted run compiled and executed one test in 62 seconds, then correctly failed
  the footprint invariant because two occupied voxels touched a declared boundary. The sculpt
  itself is unchanged; the declared horizontal and upper padding is enlarged by four voxels before
  rerunning.
- 2026-08-24: Four concurrent commits added alternate `RavenStatue...` classes on the same feature
  branch. Their placement initializer referenced a nonexistent `MountMode` field and their float
  authorer duplicated the same stable `RAVN` identity. Preserved both public class names as thin
  compatibility facades over the single integer `RavenSculpture...` implementation so callers do
  not split between two geometries.
- 2026-08-24: Second targeted run passed, including bounds, density, materials, backend routing, and
  artifact creation. Visual inspection found that the older three-face preview obscured the sculpt
  with painter-order slicing and arbitrary hash colors. Switched the proof image to the independent
  six-face diagnostic renderer and added a render-only raven palette remap; authoritative material
  IDs and occupancy remain unchanged.
- 2026-08-24: A concurrent follow-up added a small asymmetric branch and lichen through the same
  integer authoring primitives. Retained it because it gives the talons a readable perch, and added
  semantic brown/green presentation colors for those two existing material IDs.
- 2026-08-24: Third targeted run passed with the independent renderer and semantic palette. The
  image was solid and correctly colored, but the renderer's fixed camera viewed the raven from
  behind. Added a render-only 180-degree display copy so the final artifact exposes the brow, eyes,
  beak profile, breast, and camera-side wing. The backend-authored source capture remains the one
  used for all invariants.
- 2026-08-24: A concurrent presentation pass generalized the diagnostic renderer with an explicit
  view direction while preserving its original overload, then selected a side-biased profile that
  shows the hooked bill and layered near wing without copying or rotating authoritative voxels. It
  also strengthened the material assertion from seven to all nine semantic regions.
- 2026-08-24: The first canonical request for feature commit `15a3fa10` was killed by
  `tools/unity-run.sh` when system free memory fell below the required 8 GB floor. The unchanged
  commit was requested again after pressure cleared.
- 2026-08-24: Canonical request commit `f8a5d244`, run `32797010778`, executed the exact
  `RavenSculpture_WritesHighResolutionTexturedPng` EditMode test and passed. Artifact
  `single-test-32797010778` contained the inspected 1600×1600 PNG.

## Repository hygiene blocker

Concurrent work created three additional CI refs:
`ci-test/raven-profile-15a3fa10`, `ci-test/raven-proof-6cc8ab0a`, and
`ci-test/raven-sculpture-eee7605e`. They are not authoritative. The available GitHub connector can
move refs but cannot delete them, so branch-count cleanup remains an external repository action.
The canonical task refs are `feature/sdf-raven-sculpture` and
`ci-test/feature/sdf-raven-sculpture`.
