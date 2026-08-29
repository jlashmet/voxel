# Plan — VoxelShowcase missing water

## Observed / acceptance
Capture `20260828-172924-078` has one `VoxelShowcase` frame at camera `(59.45,20.35,-1.55)` with five marked regions over one broad green startup shelf that should read as authored water. Acceptance: all five marks share the correct water semantics during startup, the temporary fallback relinquishes ownership after authoritative publication, and the focused built-player proof visibly renders the production fallback without requiring the full showcase world.

## Competing hypotheses / evidence
1. **Receiving-bank content:** the bounded castle repair converts the five marked offsets to water while preserving the cascade and dry outer shore, but built-player evidence showed the polygon could still appear transiently.
2. **Far-field metadata:** recapture now retains authored lowered height/material; still insufficient by itself.
3. **Startup fallback semantics/ownership:** runtime replay showed all five marks were covered together by the same emergency fallback and disappeared together after handoff. The production fix now samples authored lowered height/material for that fallback and preserves critical-ring exclusions/recentering.

## Selected fix / regression
`VoxelFarTerrain` builds a bounded semantic startup fallback from the same terrain/material ownership inputs as authoritative far terrain. `StartupFallbackPreservesAuthoredWaterHeightAndMaterial` seeds a known authored-water coarse region and asserts production fallback vertices use the lowered water height and water albedo.

## Focused visual validation
Per user direction, final visual CI uses `Assets/Scenes/WaterStartupFallbackValidation.unity` instead of booting `VoxelShowcase`. Its validation-only bootstrap seeds the exact regression water region, allows production `VoxelFarTerrain` to build its first startup meshes, then freezes copies of those production-generated meshes so the 10 s player screenshots can inspect the transient water fallback deterministically. `tools/showcase-player-capture.sh` maps only the exact regression filter to this scene.

## Blast radius / cost
Production behavior is unchanged by the validation scene. The new bootstrap runs only when that dedicated scene is loaded; normal scenes never instantiate it. Capture routing changes only one exact test filter. Runtime fix cost remains startup-only and bounded below 3,000 fallback vertices; authored far metadata adds 256 material bytes per affected region.

## Remaining gate
Run one exact-SHA PlayMode CI request for `StartupFallbackPreservesAuthoredWaterHeightAndMaterial`, with no SceneIssue replay and a 20 s focused-scene player capture. Inspect both screenshots for visible authored water and require green exact request status before any pending/closed metadata or master push.
