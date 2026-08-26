# Plan — 20260826-132402-138-VoxelShowcase

## Observed defect / acceptance
The saved VoxelShowcase pose shows a large triangle blocking the circled doorway. Preserve the original capture. Acceptance is: the exact saved pose is clear, a focused production-path regression is green, and the final feature diff contains only this issue's fix/evidence.

## Hypotheses
1. **Leading:** retained arch-profile ownership uses only the topology triangle centroid, so a coarse same-material triangle can bridge from the clear aperture into the retained annulus while its centroid remains inside the aperture and escape suppression.
2. A retained arch cap/stitch/profile triangle is itself malformed and spans the doorway; changing continuous-topology ownership would therefore be unrelated.

The discriminator is the focused `RetainedProfileOwnsTriangle` regression plus exact-pose replay. H1 is falsified if the regression passes unchanged or a fixed ownership predicate does not remove the captured triangle.

## Material results
- Source trace identified `CpuTransvoxelChunkCache.RetainedProfileOwnsTriangle` as centroid-only, while existing arch tests protect authored opening/profile dimensions, cap endpoints, materials, and wedge bounds.
- Added `ArchDoorwayTopologyOwnershipTests.RetainedProfileOwnsTriangle_WhenTriangleBridgesClearOpeningIntoAnnulus` to model a triangle whose centroid is in the aperture but one vertex crosses the retained annulus.
- Baseline request `de385650dfad9b53d0dc6993950b439af4198e35` was **not valid product evidence**: Unity stopped on script compilation before executing the test. The branch was subsequently refreshed to current master; rerun the baseline before production edits.

## Remaining gates
- [ ] Get a valid failing baseline for the focused regression on the current no-production-change feature head.
- [ ] Implement the smallest proven ownership fix, preserving material/depth/wedge exclusions.
- [ ] Push production/test commit to `fixes/agent-9` and record it as `fixCommit`.
- [ ] Run green exact-SHA targeted CI for the focused regression.
- [ ] Replay every original pose through the shared scene-issue replay path and commit `verification-final.png`.
- [ ] Update experiment/plan evidence.
- [ ] In a separate bookkeeping commit, move the entire capture `open -> pending`, set `status: pending`, fill `resolutionSummary`, `regressionTest`, and `fixCommit`, and leave `resolvedUtc` empty.
- [ ] Leave the verified feature head unchanged and wait for coordinator promotion/review. Do not push master or start another capture.
