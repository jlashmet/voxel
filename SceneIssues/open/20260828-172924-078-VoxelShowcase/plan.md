# Plan — VoxelShowcase missing water

## Observed / acceptance
Capture `20260828-172924-078` contains one 1440×801 `VoxelShowcase` frame at camera `(59.45,20.35,-1.55)` with five marked regions spanning one broad teal/green shelf; the note says the whole grass patch should be water. Acceptance: the built player exposes the authored receiving-water geometry at all five marked sightlines, preserves the cascade and dry outer shore, and never lets a startup proxy share the critical water footprint.

## Competing hypotheses / evidence
1. **Authored storage geometry:** Experiment 001 repaired the receiving bank, but exact-player replay still showed the marked polygon: necessary, not sufficient.
2. **Far semantic presentation:** Experiment 002 retained lowered-water material metadata through far capture/rendering. The polygon still appeared transiently: necessary, not sufficient.
3. **Startup fallback ownership:** Experiments 005–009 show all five circles were covered together by one flat diagonal emergency fallback during startup; it disappeared after authoritative ring handoff. Camera relocation before that handoff could leave the fallback centered on the old camera, moving a 4 km corner through the current view.

## Selected fix / regression
`VoxelFarTerrain` recenters the emergency startup fallback when the camera moves while preserving exclusion for both published ownership and the current critical-ring footprint. `StartupFallbackRecentersAfterCameraRelocationBeforeRingPublication` deterministically relocates the camera before another ring publishes and asserts the proxy follows without covering either critical footprint while retaining unresolved horizon coverage.

## Blast radius / cost
Scope is limited to castle receiving-bank compatibility repair, far-field lowered material metadata, and startup fallback ownership. Analytic terrain, moat policy, near publication, destruction, water gameplay, and steady-state clipmap behavior are unchanged. Added steady work is one camera-delta check per `LateUpdate` only while the emergency fallback exists; qualifying moves rebuild an 8-vertex/8-triangle startup-only proxy. Far lowered metadata remains +256 material bytes per affected region.

## Current state / remaining gate
`fixes/agent-7` was rebuilt as a clean ticket-only commit on current `master` `bc0593070e10497da89d651e1ab3c61772335f95`; no unrelated capture or CI-request file remains in the feature diff. Prior run `33268258959` executed the relocation regression successfully and completed the 60 s `VoxelShowcase` player build/replay, but the workflow result was cancelled, so it is diagnostic only. Keep the issue `open`. Next: issue one fresh exact-SHA request on `ci-test/fixes/agent-7` for the relocation regression plus this issue’s 60 s replay. Promote only after green exact-SHA focused CI, green built-player validation, and successful replay of all five marked regions.
