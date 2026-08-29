# Plan — VoxelShowcase missing water

## Observed / acceptance
Capture `20260828-172924-078` has one `VoxelShowcase` frame at camera `(59.45,20.35,-1.55)` with five marked regions over one broad green startup shelf that should read as authored water. Runtime evidence showed all five marks share one transient startup fallback owner. Acceptance: that fallback carries authored water semantics during startup, preserves critical-ring ownership/recentering, and is visually proven in a built player.

## Competing hypotheses / evidence
1. **Receiving-bank content:** bounded castle repair converts the five marked offsets to water while preserving cascade and dry outer shore, but the polygon could still appear transiently.
2. **Far-field metadata:** recapture retains authored lowered height/material, but metadata alone did not remove the generic startup shelf.
3. **Startup fallback semantics/ownership:** replay showed all five marks were covered and cleared together by one emergency fallback. This was the discriminating root cause.

## Selected fix / regression
`VoxelFarTerrain` now builds a bounded semantic startup fallback from the same terrain/material ownership inputs as authoritative far terrain. `StartupFallbackPreservesAuthoredWaterHeightAndMaterial` seeds a known authored-water coarse region and asserts production fallback vertices use lowered water height and water albedo.

## Focused visual validation
Per user direction, the final built-player gate uses `Assets/Scenes/WaterStartupFallbackValidation.unity` instead of rebuilding the full showcase. The validation-only bootstrap seeds the exact regression water region, runs production `VoxelFarTerrain`, then freezes copies of its production-generated startup meshes so CI can inspect the transient surface deterministically. Run `33279274759` on transport SHA `5aba178e7588d7638b6961741f5ff8381cddbeda` is green; its parent is exact feature SHA `5a86122c4ec91b1e6b52afa3b035cd59486a4f7f`. The PlayMode regression passed 1/1, the real player built successfully, both 1600×900 frames visibly render the authored blue water surface, and the player log contains no runtime error/exception/assertion failure.

## Blast radius / cost
Production behavior from the fix is startup-only: under 3,000 fallback vertices and no steady-state sampling after authoritative publication. Authored far metadata adds 256 material bytes per affected region. The validation scene/component runs only when explicitly loaded, and capture routing changes only the exact regression filter.

## State
All requested behavioral and built-player gates are green. Pending metadata records fix SHA `5a86122c4ec91b1e6b52afa3b035cd59486a4f7f`; proceed with pending/closed bookkeeping and final master integration only.
