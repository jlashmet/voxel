# Plan — VoxelShowcase missing water

## Observed / acceptance
Capture `20260828-172924-078` marks five regions across one broad teal/green shelf and says the whole patch should be water. Camera `(59.45,20.35,-1.55)`. Acceptance: the built player exposes the authored receiving-water geometry at every marked sightline; cascade and dry outer shore survive; no startup proxy shares that footprint.

## Competing hypotheses / evidence
1. **Storage geometry:** Experiment 001 repaired the authored lower-river receiving bank, but exact-player replay still showed the marked polygon: necessary, not sufficient.
2. **Far semantic presentation:** Experiment 002 retained lowered-water metadata/material through far-field capture/rendering. The polygon still appeared transiently: necessary, not sufficient.
3. **Startup fallback ownership:** Experiments 005–008 and exact-player run `33257610930` proved all five marked centers were covered by one flat diagonal emergency fallback at 15.8/25.8 s; it disappeared by 35.8 s after authoritative ring handoff and stayed gone through 55.8 s. The failed focused assertion selected an unpublished/global ring mesh, not a persistent scene defect.

The remaining capture-specific cause is camera relocation during startup/replay: the emergency fallback could remain centered on its previous camera while the real capture camera moved, letting a 4 km corner cross the current view until another async ring published. Production commit `a5f0f474e0f9f91e64c3c3ca017c52f6a3ebc150` recenters the startup fallback when the camera moves and keeps both published ownership and the current critical-ring footprint excluded. Regression commit `8fbd62ab3474dbe90dbc37ba7d27623cbaecebaa` deterministically relocates the camera before ring publication and asserts the fallback follows without moving underneath either critical footprint.

## Blast radius / cost
Scope remains limited to castle receiving-bank compatibility repair, far-field lowered material metadata, and startup fallback ownership. Analytic terrain, moat policy, near publication, destruction, water gameplay, and steady-state clipmap behavior are unchanged. Added steady work is one camera-delta check per `LateUpdate` only while the emergency fallback exists; rebuilds remain tiny 8-vertex/8-triangle startup-only meshes when movement crosses the existing threshold. Far lowered metadata remains +256 material bytes per affected region.

## Current state / remaining gate
Current `master` `bc0593070e10497da89d651e1ab3c61772335f95` was merged into `fixes/agent-7` conflict-free. The previous final CI source was a product/test failure and cannot satisfy the gate. Next: issue one fresh exact-SHA targeted request for `StartupFallbackRecentersAfterCameraRelocationBeforeRingPublication` plus 60 s replay of this SceneIssue. Keep the issue `open` until focused CI, built-player scene validation, and all original marked poses are green; only then fill pending metadata and promote/close.
