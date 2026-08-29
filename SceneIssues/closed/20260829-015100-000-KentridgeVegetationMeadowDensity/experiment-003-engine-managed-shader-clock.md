# Experiment 003 — engine-managed shader clock

**Hypothesis.** The grass-specific `_GrassTime` uniform is the failed boundary. Unity's built-in shader `_Time.y` is known-good in the exact player because `AuthoredSky.shader` uses it and the clouds visibly change between the same captures where grass does not.

**Action / source.** On exact source `08116a0d6676dad0300cc5b44cd13f4c10de91b2`, replace the grass wind formula's custom `_GrassTime` input with `_Time.y`; remove `_GrassTime` material/CBUFFER/CPU publication state. Validate through request `24778463d6e58b81bff036c6e0e59743f18ca63a`, run `33246992214`, artifact `9713189596` (`sha256:de208d24ca4eb61ad718425035c012dfe817fd1acdaacd7e41d7d2f191458ab7`).

**Result.** Focused regression and standalone Kentridge capture are green. Density remains 11,478 semantic grass / 114,580 blades total, 57,589 blades in the primary meadow, 8 chunks, excluded-surface-grass=0. Human inspection still shows no blade pose change. Pixel comparison finds exactly 0 changed pixels in the grass/ground region from 39.2→49.2 and 49.2→59.2 seconds, while 68k–71k sampled sky pixels change between those pairs. The foreground below y=450 is also unchanged as early as 19.2→29.2; later scene changes are confined near the horizon.

**Verdict.** FALSIFIED. Clock delivery is not the discriminator: even the engine-managed clock that visibly animates sky produces no observable grass motion in Kentridge.

**Next step.** This is the third genuine failed production attempt. Per SceneIssue workflow, make no further production change until the visible grass draw is isolated in a minimal render reproduction. First determine whether the production grass shader/geometry can visibly deform in isolation; if it can, investigate Kentridge visibility/presentation rather than changing the clock again.
