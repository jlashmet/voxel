# SceneIssue 014636 — transient brown terrain patches

## Goal

Eliminate the broad brown geometry/material patches that appear momentarily across the terrain at
the saved `VoxelShowcase` camera pose and then disappear.

## Scope

- Exact 1293×718 replay of the single saved `Showcase Camera` pose.
- Streaming convergence, GPU/CPU mesh ownership, LOD replacement, and surface-material assignment.
- Terrain authoring only if authoritative voxel evidence proves the brown areas are intentional.
- A focused deterministic regression for the proven invariant.

## Constraints

- Continue on the shared `fixes` branch and work only this oldest open capture.
- The capture has no circles, so inspect the entire frame and preserve the original screenshot.
- Use `tools/unity-run.sh` for local Unity; never invoke the Unity binary directly.
- Record every replay, diagnostic, and test immediately as a numbered experiment here.
- Commit production/test work and evidence before the separate `issue.json` resolution commit.

## Initial evidence

- The original frame shows large irregular brown patches across both near and distant terrain,
  interleaved with normally shaded grass and several comb-like edges.
- It was captured about 356 seconds after scene load with 95 reported surfaces, so it is not simply
  an early blank frame; the note says the brown material subsequently disappears.
- The artifact spans multiple depth bands, making stale/replacement geometry or temporary material
  data more plausible than one authored local dirt feature.

## Findings

- Exact current-head replay reproduces the original brown patchwork during convergence. The closest
  matching frame has 420 drawn surfaces and 45 missing visible surfaces; the patchwork disappears
  when the view reaches 458 drawn and zero missing.
- This proves a partial-residency presentation handoff. The settled authoritative view contains
  only narrow authored dirt features, not the broad brown regions.
- Disabling GPU extraction reproduces the same transient, ruling out the GPU cutover.
- The far fallback and authoritative terrain bindings disagree below the base-height split:
  `GameShowcaseMaterials` returns dirt while `GameTerrainMaterials` returns grass. Missing near
  chunks therefore reveal brown fallback which disappears as green authoritative chunks publish.
- The existing `NearAndFarTerrainAgreeOnGroundCover` regression fails 0/1 on the current source at
  height 196: near material 13 (grass), far material 10 (dirt).
- After aligning the fallback binding, the exact replay stays grass-colored with 77 and then 22
  visible surfaces still missing, and settles at 458 drawn / zero missing without broad brown areas.
- Affected EditMode validation passes 9/9: four game material-role tests and five far-fallback
  contract tests. The latter's stale pre-013924 octave guard was aligned with the resolved `9/18`
  terrain invariant before the clean run.

## Acceptance criteria

1. An exact current-head replay determines whether the saved pose still exhibits brown transient
   patches and records convergence telemetry with a nonzero surface count.
2. Evidence identifies the responsible authoritative or presentation invariant.
3. A focused regression directly guards that invariant and executes a nonzero test count.
4. The smallest proven fix removes the transient patches without hiding authored dirt surfaces.
5. Final exact-pose replay remains clean after convergence; all experiments and artifacts are
   recorded, temporary wiring is absent, and resolution bookkeeping is committed separately.

## Work

- [x] Read the manifest, note, screenshot, camera metadata, and absence of marked circles.
- [x] Replay the exact pose on current `fixes` and capture convergence evidence.
- [x] Isolate whether the patches are authoritative material, invalid mesh data, or stale LOD state.
- [x] Add or identify the smallest direct regression for the proven cause.
- [x] Implement the smallest necessary production fix.
- [x] Run focused Unity validation and exact-pose final replay.
- [x] Review, commit, push, then resolve `issue.json` in a separate commit.
