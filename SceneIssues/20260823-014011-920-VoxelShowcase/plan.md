# SceneIssue 014011 — coarse LOD material attribution

## Goal

Eliminate the blueish, spiral/contour-like coarse-terrain bands in the saved
`20260823-014011-920-VoxelShowcase` view, prove the result with focused regressions and an exact
camera replay, then resolve the capture before moving to the next open SceneIssue.

## Scope

- Shared CPU/GPU coarse density and material attribution.
- Focused EditMode density, ownership, cutover, and vertex-attribute parity regressions.
- Exact 1293×718 replay of the saved `Showcase Camera` pose.
- Branch repair needed for validation: duplicate verification-only oracle code was committed twice
  and prevented `fixes` from compiling; commit `37bca5769` removes only the duplicate copies.
- No changes to authoritative voxel state, unrelated LOD topology, tiering, or GPU cutover scope.

## Binding constraints

- Work one capture at a time on the shared `fixes` branch.
- Keep this plan and experiment evidence in this capture directory; `.claude/plans/` is retired on
  current `master` and must not be recreated.
- Preserve deterministic integer authoritative state and CPU/GPU derivation from the same cells.
- The three-attempt rule has already been met: the checked-in layered-slope fixture is the required
  bare-bones reproduction.
- Final validation uses only `ci-test/fixes`; local Unity runs are diagnostic evidence and cannot
  establish completion under the current AGENTS.md workflow.
- The feature commit and the later `issue.json` resolution commit must remain separate.

## Evidence and findings

- The original screenshot shows broad blue/grey contour bands and spiral-like material rings over
  the near coarse terrain.
- The earlier coarse-phase fix improved but did not eliminate the capture.
- CPU-only and GPU exact-camera replays at the same source revision were visually equivalent,
  ruling out a GPU-only topology/cutover cause.
- The minimal `Step2LayeredSlopeUsesVisibleTopSurfaceMaterial` fixture failed before the material
  fix with buried material 2 instead of exposed cap material 1.
- Commit `8cd28a5ea7133a4012a17112375f70384bee79ec` decouples geometry crossing distance from material
  selection and prefers an exposed +Y cap on both CPU and GPU paths.
- Duplicate commits `c07f68ec05` and `c032bf23d8` added identical `CpuDensityFieldSnapshot`,
  `SampleMixedNeighbourhood`, readback helpers, and `MixedSampleFieldMatchesTheCpuJob` bodies.
  Fresh local compilation proved these duplicates cause CS0101/CS0111 errors.
- Removing only the duplicate copies restored local compilation. The three repaired files exactly
  match the known single-copy tree at `c032bf23d8^`.
- Diagnostic local runs passed the focused coarse-density class 5/5 and the related
  GPU/ownership/cutover/vertex selection 12/12 after updating one stale source-text assertion to
  the shader's current nested guard. These results guide the repair but require CI confirmation.
- An attempted local exact-camera build was interrupted before producing a screenshot; no visual
  conclusion can be drawn from it.

## Failed or deprioritized hypotheses

- **GPU-only LOD2 cutover/topology** — disproven for the residual artifact by the exact CPU/GPU
  replay.
- **GPU Planar/Sharp inclusion mismatch** — real parity debt, but not causal because the CPU replay
  contains the same residual artifact.

## Acceptance criteria

1. `fixes` compiles with exactly one copy of every mixed-field oracle seam and test.
2. The smallest CI-requested test runs a nonzero test count and passes on `ci-test/fixes` in under
   five minutes after its job starts.
3. Relevant broader focused regressions are green when warranted.
4. A current-head exact saved-camera replay shows the blue/spiral coarse-terrain defect is gone on
   the materially relevant CPU and GPU paths.
5. Temporary replay/CI wiring is absent from the feature branch before resolution.
6. The production/test commit is recorded in a separately committed `issue.json` resolution.
7. `fixes` is pushed, `ci-test/fixes` is deleted once validation is green, and no extra branches
   are created.

## Work

- [x] Inspect the capture screenshot, note, camera pose, relevant implementation, and tests.
- [x] Apply and verify the coarse-phase correction.
- [x] Replay the exact saved view with GPU cutover disabled and rule out a GPU-only cause.
- [x] Build a minimal failing layered-slope reproduction before the production material fix.
- [x] Implement the deterministic shared CPU/GPU material-selection fix.
- [x] Diagnose and commit the duplicate-oracle compile repair on `fixes`.
- [x] Update the stale GPU source guard to the current nested phase/material structure.
- [x] Remove temporary local replay files and keep only durable findings.
- [x] Push the repair to `fixes`.
- [x] Create the first narrow request on `ci-test/fixes`; it is superseded by the plan-location
      correction before its result can be authoritative.
- [x] Commit and push this plan-location correction on `fixes`.
- [ ] Force-reset the existing `ci-test/fixes` branch to the corrected feature head, add a new unique
      request, push it, and monitor `ci/single-test` to a terminal result.
- [ ] Iterate on `fixes` and reuse the same CI branch if the requested test fails.
- [ ] Run broader focused CI validation if the smallest regression does not cover the repaired
      mixed-field and vertex-parity paths.
- [ ] Produce and inspect a current-head exact-camera replay without committing temporary wiring.
- [ ] If the replay is clean, resolve `issue.json` in a separate commit, push, and delete
      `ci-test/fixes`; otherwise update this plan before further production changes.
- [ ] Advance to `20260823-014108-038-VoxelShowcase` only after 014011 is fully resolved.
