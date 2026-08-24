# SceneIssue 193321 — staircase silhouette on hero arch

## Goal

Remove the non-smooth staircase silhouette in the four marked regions along the inner curve of the
`ArchLookdev` hero arch while preserving the authored voussoir and masonry character.

## Scope

- Exact 1637×1140 replay of the saved `Hero Arch Camera` pose and 34-degree field of view.
- Inner-arch geometry generation, voxel/profile resolution, curve sampling, and surface extraction.
- Focused deterministic coverage for the proven geometric invariant.

## Constraints

- Continue on the shared `fixes` branch and work only this oldest open capture.
- Inspect all four circles clustered around the upper inner arch, not just the overall silhouette.
- Treat purposeful block joints separately from unintended stepped curvature.
- Use `tools/unity-run.sh` for Unity and record every experiment immediately in this directory.
- Commit production/test/evidence first; resolve `issue.json` separately with the real fix SHA.

## Initial evidence

- The screenshot shows an otherwise radial voussoir arch whose inner opening breaks into visibly
  horizontal/vertical steps near the crown and upper-left quadrant inside all four circles.
- The camera is close, narrow-FOV, and captured after 11 seconds, so the issue appears to be stable
  authored/extracted geometry rather than streaming convergence.
- The UI reports `CUT STONE · Q4 VOXELS`, making quantization or the inner-curve construction policy
  the first subsystem to inspect.

## Findings

- Exact current-head production replay confirms the stable staircase across all four marked areas.
  Intentional radial voussoir joints remain visually distinct from the lattice-aligned intrados.
- Front/soffit analytic crossings are already accurate to 0.111 voxel worst case. The visible steps
  are at the rear opening boundary, where retained profile emission has sides but no rear cap.
- Fix attempt 1 added the missing rear cap but the exact visual replay was unchanged because that
  cap faces away from the camera. A 0.125-voxel rear-only intrados guard is the next measured fix.
- Fix attempt 2's 0.125-voxel rear guard is still visibly stepped. The earlier crossing bound was
  not measured on the exact capture bay or rear layer, so that exact diagnostic is now required.
- The exact 28-span composed-bay diagnostic measures 0.111 voxel worst at mid-depth but 0.200
  voxel inward at the rear structural layer, including 0.164 voxel at the marked crown. The
  smallest Q4-representable rear-only guard that covers the measured bound is 0.25 voxel.
- Fix attempt 3 increased the rear-only guard to 0.25 voxel and passed 6/6 focused tests, but the
  exact visual replay remains stepped. Three full-scene fix attempts are now exhausted; the next
  required step is a bare-bones backing/opening/profile reproduction before any fourth change.
- The required two-primitive reproduction fails at the first backing layer: its opening crosses
  0.075 voxel inward while the long taper covers only 0.027, leaving a 0.049-voxel exposed edge.
  Layers z=3 and z=4 also have deficits; the rear is already covered. The isolated cause is zero
  guard at the front shoulder, not an insufficient rear endpoint.
- A 0.125-voxel near guard interpolated to the 0.25 rear bound makes the isolated depth invariant
  green at every layer while leaving the exact front face coordinates unchanged. The bare fixture,
  exact bay, and profile topology suite passes 8/8; exact-camera proof remains outstanding.
- The exact replay remains stepped after the full-depth coverage change. The scalar fixture found
  a real but non-causal deficit and is not yet a faithful visual reproduction. Paired diagnostic
  builds must now identify whether retained profiles, the backing, or structural ring owns the
  visible pixels before further production edits.
- Disabling retained profiles exposes a stronger version of the same staircase across the whole
  opening. The binary mesh owns the reported edge; the retained profile only masks part of it.
  Structural-ring-only versus composed-backing isolation is next.
- The structural arch alone (piers, arc wedges, and retained profiles) preserves the exact marked
  staircase after all bay backing/composition is removed. This is the faithful minimum visual
  reproduction: the binary arc-wedge surface, not the bay, owns the exposed edge.
- Removing all depth-axis faceted faces from that minimum reproduction does not move the steps.
  The continuous Transvoxel radial surface is the binary owner; profile-only completeness must be
  proven before selectively replacing profile-covered topology.
- Profile-only chunks render a smooth complete ring through the marked curve and preserve
  intentional radial joints. The causal invariant is replacement ownership: continuous radial
  triangles covered by retained profile faces must not also be appended, while unrelated geometry
  and recessed joint regions remain binary-backed.
- The authored-boundary contract audit suggested a half-cell mismatch because Transvoxel presents
  integer samples at `+0.5` while `ProfilePoint` uses the integer primitive centre. A direct test
  exposed that formula difference, but moving the profile by `+0.5` on both radial axes left the
  exact staircase unchanged. The production replay disproves profile-centre alignment as the
  visible cause; the experimental code and test were reverted.
- GPU cutover is intentionally incomplete rather than architecturally required for arches. Profile
  blocks, planar/faceted masonry, decorations, LOD4, and LOD8 still route through CPU production
  paths. Removing that routing without implementing equivalent GPU append/topology is invalid,
  and GPU Transvoxel alone would reproduce the shared density/topology math rather than smooth it.
- The exact crop shows the smooth retained front/soffit and a separate stepped opening behind it.
  Cell-face accounting proves the retained profile rear endpoint is exactly one voxel short:
  occupied samples end at `origin.z+Depth-1`, their rear face is `origin.z+Depth`, but `BackQ4`
  starts projection at the former. The corrected invariant is red 0/1 (expected 200, actual 184).
- Extending `BackQ4` alone makes all retained profiles disappear and exposes a stronger staircase.
  `TryReadProfileBacking` rounds that rendered endpoint into projected empty space and rejects each
  segment. `BackQ4` currently conflates geometry extent with the last occupied backing sample; the
  contract must represent and validate those independently.
- Separating backing depth restores profile emission but leaves the exact staircase unchanged.
  Rear endpoint accounting is non-causal and will be reverted. Together with the smooth
  profile-only diagnostic, the evidence supports the documented one-surface/one-primitive rule:
  retained profile coverage must replace duplicate voxel-derived topology rather than overlap it.
- The first selective replacement implementation passes its pure ownership/profile tests (5/5)
  but leaves the exact staircase visible. Live omission counts are required before adjusting the
  predicate or touching faceted topology; the fixture alone does not prove production coverage.
- Live counts proved the first filter omitted broad interior topology but retained joint/boundary
  strips. Assigning matching triangles by centroid through the raw wedge removes those duplicate
  strips. The exact replay is smooth across all marked regions while retained radial sides preserve
  voussoir joints; unrelated scene geometry remains.

## Acceptance criteria

1. Exact-pose current-head replay reproduces or disproves the staircase in every marked region.
2. Evidence distinguishes intentional block joints from unwanted curve quantization/extraction.
3. A focused regression directly measures the responsible inner-arch smoothness invariant.
4. The smallest fix improves the marked silhouette without weakening masonry joints or openings.
5. Final exact-pose replay and affected tests pass; experiments and artifacts are recorded,
   temporary wiring is removed, and resolution bookkeeping is committed separately.

## Work

- [x] Read the manifest, note, screenshot, camera metadata, and all four marked circles.
- [x] Replay the exact current-head pose and inspect marked-region geometry.
- [x] Identify the generating path and quantify the staircase invariant.
- [x] Add or extend the smallest focused regression; it fails 0/1 before the rear-cap fix.
- [x] Build and diagnose the required bare-bones reproduction after three failed fix attempts.
- [x] Implement the smallest proven geometry fix from the isolated cause.
- [x] Run affected Unity tests and final exact-pose replay.
- [x] Review, commit, push, and resolve the manifest separately.
