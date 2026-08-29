# Plan — SceneIssue 20260825-192751-413 VoxelShowcase

## Evidence / marked region
- One source capture/circle marks the extreme top-left FPS telemetry; replay pose is `Showcase Camera` at `(77.953941,24.55005,-3.345814)`, FOV `70`. Repository metadata and annotation geometry have been inspected. The GitHub connector does not expose the binary source PNG through its text/blob endpoints, so visual conclusions are grounded in the exact built-player replay captures at the same issue scene rather than pretending the inaccessible source bytes were opened.
- Initial built-player evidence isolated a ~`0.65–0.77 s/frame` solid-admission stall while arena upload/water were negligible. Removing global resident-world mirror recovery eliminated that cost.
- Region-demand recovery then failed because one 512³ Storage region contains 262,144 logical 8³ blocks; at 64 blocks/frame a demanded region needed 4,096 frames before readiness.
- Exact-block recovery materially improved runtime to ~`194–218 FPS` with solid admission around `~2 ms`, but exact-SHA run `33234469456` still stopped after three GPU completions. The player plateaued at `26 visible / 744 missing` for ~20 seconds while `jobs=12`, proving a liveness failure rather than slow frame execution.
- The live-Storage-generation fix was exercised by exact CI and rejected: the plateau was unchanged.
- Recovery-admission fairness was then exercised by exact run `33240075886`. It increased GPU completions from three to eight, confirming starvation as a contributor, but the traversal still lost every visible voxel draw at frame 146 and the built player froze at `23 drawn / 747 missing` through 45 s. All four timed replay captures plus `verification-final.png` were inspected; the broken near/mid geometry does not recover after t24.3.

## Competing hypotheses / discriminator
- Original global recovery CPU stall: **fixed**; admission is now milliseconds, not ~700 ms.
- Whole-region readiness: **fixed**; exact-block recovery produces early GPU completions without scanning the full 512³ Storage region.
- Pending stage retains an obsolete Storage generation: **rejected by exact-SHA runtime evidence**; refreshing the live mirror generation did not change the early-completion plateau.
- Storage/change-journal version-domain mismatch: **rejected by source evidence**; `RegionReadSource.Version` is the change-source version.
- Successful GPU write leaks the shared extraction lease: **rejected by source evidence**; the write state releases `_gpuExtraction` when polling leaves `Pending`.
- Geometry arena exhaustion: **rejected by exact-player telemetry**; `leaseFail=0` with substantial vertex/index/draw capacity unused.
- Frame saturation: **rejected by exact-player telemetry**; the broken plateau persists around `205–244 FPS`.
- Recovery admission starvation: **confirmed contributor, insufficient sole cause**; fairness moved completion from three to eight but exact coverage still failed.
- **Supported current hypothesis: optional nonresident halo mismatch.** The authoritative CPU exact snapshot requires core regions but treats an unavailable optional sampling-halo region as empty. GPU `Covers` previously required every padded brick-cache block to be mirror-ready. A halo-only block in a legitimately nonresident region can never recover and therefore leaves an eligible GPU stage pending forever. The persistent GPU directory already uses missing lookup entries as canonical empty.

## Fix
- Preserve the shared persistent mirror, exact-block demand queue, journal replay, fairness gate, 64-block recovery slice, and the rule that mirror mutation never occurs while an extraction is active.
- Pass the exact production chunk core bounds into GPU mirror coverage admission.
- For a nonresident block intersecting that core, continue to block admission.
- For a nonresident block that lies only in the optional sampling halo, accept canonical empty-by-absence instead of queueing impossible recovery.
- Keep resident-block change-generation validation/recovery unchanged.
- Do not add CPU fallback, blocking GPU waits, new allocations, larger buffers, shader changes, or wider recovery scans.

## Regression / acceptance
- Focused behavioral regression `GpuSurfaceMirrorRecoveryLivenessTests.DemandRecoveryCannotBeStarvedByCoveredGpuWork`: exact `VoxelShowcase`, isolated 320x180 render target, 96 m traversal, observes the shared mirror directly, requires demand recovery, at least four additional GPU completions, no >180-frame stalled recovery/active-extraction overlap, and `OptionalNonResidentHaloBlocksAccepted > 0` so the current discriminator is actually exercised.
- End-to-end regression `ShowcaseGpuMigrationTests.MovingShowcaseCompletesGpuSurfaceBuildsAndPreservesCoverage`: exact `VoxelShowcase`, 210 m traversal, >=8 GPU completions, zero eligible fallbacks, no blocking completion, moving p95 `<18 ms`, p99 `<25 ms`, stationary settle with no missing chunks, stationary p95 `<8 ms`.
- Exact built-player replay must restore near/mid geometry at the issue scene, eliminate the persistent missing-chunk plateau, and retain substantial high-FPS headroom.

## Blast radius / cost
- Scope is only solid step-1/step-2 GPU mirror admission. Water, HLOD algorithms, visibility policy, Storage writes, collision, world generation/content, shader layout, and geometry arena allocation are unchanged.
- Core correctness is unchanged: unavailable blocks that intersect the required chunk core still reject admission. Only nonresident halo-only blocks inherit the CPU snapshot's existing empty semantics.
- Recovery remains capped at 64 demanded blocks per preparation slice and journal replay at 128 records per slice; no per-frame allocation is introduced.
- Added work for the new rule is limited to integer block voxel bounds plus one AABB/core intersection when an uncovered block belongs to a nonresident region, and a diagnostic counter increment for accepted halo blocks.
- Closure requires green exact-SHA targeted CI plus green exact built-player visual/runtime evidence; no gate weakening.
