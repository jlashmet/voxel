# Experiment 003 — engine-managed shader clock

**Hypothesis.** The grass-specific `_GrassTime` uniform is the failed boundary. Unity's built-in shader `_Time.y` is known-good in the exact player because `AuthoredSky.shader` uses it and the clouds visibly change between the same late captures where every grass/ground pixel is unchanged.

**Action.** Replace only the grass wind formula's custom `_GrassTime` input with `_Time.y`. Remove the now-dead `_GrassTime` material property/CBUFFER slot and CPU material/property-block clock writes. Preserve packed geometry, amplitudes, spatial phases, camera-facing reconstruction, interaction, and draw count.

**Expected result.** The existing late stationary Kentridge camera must show changed blade silhouettes between time-separated captures while density/leakage diagnostics and packed topology remain unchanged.

**Falsifier.** If the built player's late grass raster remains identical while `_Time.y` continues moving the sky, reject clock delivery entirely and isolate the actual visible grass draw in a minimal render repro before changing production again.
