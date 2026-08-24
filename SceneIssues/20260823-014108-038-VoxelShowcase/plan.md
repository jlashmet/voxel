# SceneIssue 014108 — waterfall occlusion and stray terrain sheet

## Goal

Restore the expected castle-ravine waterfall view and remove the invalid diagonal terrain sheet
crossing all four marked regions in the saved `20260823-014108-038-VoxelShowcase` capture.

## Scope

- Exact replay of the saved 1293×718 `Showcase Camera` pose.
- Castle waterfall authoring and its terrain/ravine clearance invariants.
- CPU/GPU surface extraction only if replay evidence proves the sheet is generated geometry rather
  than an authoring overlap.
- Focused deterministic regression for the proven cause.

## Constraints

- Continue on the shared `fixes` branch and work only this oldest open capture.
- Preserve CPU-authoritative integer voxel state and derive rendering from the same cells.
- Inspect every marked region and document every replay, test, and diagnostic as a numbered
  experiment in this directory.
- Use `tools/unity-run.sh` for local Unity; never invoke the Unity binary directly.
- Keep temporary replay fixtures and build artifacts out of the committed production tree.
- Commit production/test changes before the separate `issue.json` resolution commit.

## Initial evidence

- The original frame contains a very large flat green diagonal sheet from the lower-left foreground
  to the upper-right background, crossing all four circles.
- The sheet occludes the authored ravine opening where the note expects a waterfall.
- Waterfall authoring exists in `CastleLandscapeAuthoring.RavineWaterfall`; the screenshot alone
  does not yet distinguish missing authored cascade voxels from invalid terrain mesh topology.

## Findings

- Current-head exact replay disproves that the defect remains: the diagonal sheet is absent from
  all marked regions and the blue cascade is visible behind the central tower (experiment 001).
- The frame was captured after surface convergence, so disappearance is not a missing/late chunk.
- Commit `9275602c3610079a2966cd022b1a3f2fb13d8b62` corrected GPU regular-cell ownership across the
  negative chunk shell, matching the class of enormous cross-chunk triangle seen in the original.
- Existing castle authoring tests directly assert the upper stream, waterfall cascade, plunge pool,
  connected outlet, and absence of unsupported loose terrain shelves above water.
- Focused GPU boundary and vertex-parity validation passes 3/3. The broad castle landscape test
  fails earlier at its lower bridge river sample (expected Water, found Empty), so it cannot yet
  provide waterfall-state evidence (experiment 002).
- A new direct waterfall/clearance regression also fails at its first reconstructed-plan upper
  stream sample (Empty instead of Water). Compare the baked world's retained plan with the fresh
  plan before changing authoring (experiment 003).
- Loaded and reconstructed plans match. A volume scan proves Water spans the upper stream and 689
  sampled Cascade cells occupy the lip; replace brittle single-cell assumptions with bounded
  volume and clearance invariants (experiment 004).
- The refined direct waterfall regression passes 1/1, proving upper-stream Water, lip Cascade, and
  three empty ravine lanes. No new production edit is warranted (experiment 005).
- An exploratory broad-test refinement replaced its three brittle centre-voxel water samples with
  bounded volume assertions. It then reached a stale legacy bridge point; a relational search for
  the Water/wood crossing and masonry support failed too (experiment 006).
- The bridge failure is unrelated drift, so all exploratory changes to the existing broad test were
  reverted. This issue retains only the focused waterfall/clearance regression and preserves the
  bridge evidence for a future dedicated issue.
- The focused regression passed again on the final reviewed tree: 1/1 in 26.10 NUnit seconds with
  a successful 41-second Unity wrapper run (experiment 007).

## Acceptance criteria

1. Current-head exact replay reproduces and classifies the marked failure before production edits.
2. A focused regression fails for the proven invariant and passes after the smallest fix.
3. Relevant affected tests pass with a nonzero test count.
4. Exact saved-camera replay shows no diagonal terrain sheet in any marked region and shows the
   intended waterfall/ravine presentation.
5. Temporary replay wiring is absent, the final diff follows repository architecture, and
   `issue.json` records the fix in a separate resolution commit.

## Work

- [x] Read the capture note, camera fixture, screenshot, and all four marked regions.
- [x] Replay the current `fixes` head at the exact saved pose and classify the visible objects.
- [x] Inspect the smallest responsible authoring or extraction subsystem and existing tests.
- [x] Classify the lower-river test failure and add a focused waterfall regression without
      weakening or bypassing the broader failing assertion.
- [x] Preserve the existing causal GPU boundary fix and add only the missing capture-specific
      waterfall/clearance regression.
- [x] Run focused affected Unity validation: GPU boundary/parity 3/3 and direct waterfall 1/1.
- [x] Replay the exact saved pose and inspect every marked region; the terrain sheet is absent and
      the cascade/ravine are visible.
- [x] Review, commit, push, resolve `issue.json` separately, and confirm no `ci-test/fixes`
      branch exists locally or on `origin`.
