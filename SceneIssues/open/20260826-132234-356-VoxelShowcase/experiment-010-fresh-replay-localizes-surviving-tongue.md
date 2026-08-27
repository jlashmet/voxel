# Experiment 010 — fresh replay localizes the surviving tongue

## Hypothesis
After removing the Moss-producing civic→upper taper, the upper marked rectangle would disappear because the district terraces already paint the join Dirt.

## Runtime result
Falsified. Exact source `44a410690af445de3f723e19472d80b7697637a3` passed its targeted regression and run `33094358010` completed the saved-camera real-player replay, but direct inspection of `RealPlayer/verification-final.png` still shows the upper circle containing one hard rectangular green tongue; the lower circle is clean. `tools/showcase-bake-cache.sh` fingerprints `Assets/Game/WorldBuilder`, so this source-changing replay is not explained by a stale semantic-world bake.

## Source discriminator
The surviving mark projects onto the civic/upper west overlap. `upper-shoulder` has west envelope x=900-72=828 dm; `civic-summit` has west envelope x=920-72=848 dm. That 20 dm axis-aligned mismatch matches the rectangle. The removed taper was wrong because it synthesized Moss over a 72×72 dm block; removing it was insufficient because nothing at correction precedence reclaimed the exposed 20 dm strip.

## Next experiment / bounded fix
Keep both terrace geometries and cores unchanged. In the existing precedence-16 upper correction, paint only local x=0..20 dm, z=72..144 dm as RoadSurface/Dirt. Regression samples world x=830/840 dm at z=260 dm as Dirt, verifies x=850 dm is untouched by the repair, preserves upper-core paving, and keeps `MaxPrimitives=3`.
