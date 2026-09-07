# Experiment 012 — opening fixed, Rossdam streaming cost exposed

## Hypothesis
Validation-only dialogue dismissal plus one stable settlement survey demand would leave enough of the unchanged 60 s replay for all four generic settlements and geography targets.

## Exact run
Feature source `652f531c53044d77c4536726f55c30c687916c1d`; CI transport `2bc25d982216a350c330ce93ea979017ea0612c2`; run `33292088730`; artifact `9726298626`.

## Focused gate result
The requested editor test did not produce NUnit XML or a managed assertion result. Unity/Mono terminated in native Burst compiler code while hashing assemblies (`Burst.Compiler.IL.Hashing.CacheBuilder.ILHasher`, SIGSEGV). The subsequent built-player build/capture step completed, so this is classified as CI/toolchain infrastructure failure rather than a product assertion. It is not a green gate and is not usable for closure.

## Built-player result
The scheduling correction materially worked: the validation profile dismisses opening dialogue, restores `Time.timeScale=1`, reaches gameplay around t=15, records 3.61 m local CharacterMotor movement and 4.73 m macro-road CharacterMotor movement, then captures the macro road. All four Moordell presentation columns settle and `macro-moordell.png` is captured at full resolution.

Rossdam then becomes the active target and remains there until the 60 s harness exits with zero assertion failures. Renderer telemetry repeatedly reaches `jobs=0`, `missingVisible=0`, coverage true; screenshots progressively gain the Rossdam lake/settlement feature content, with a building roof only appearing near the end. The all-building content gate never opens. This is not renderer convergence noise.

## Discriminator / cost finding
Rossdam shares its residency with the first-pass lake. Current intent is 104 m x 54 m x 4.5 m authored depth. The water catalogue emits a rounded-box carve plus rounded-box water fill; their axis-aligned iteration bounds are about 42 million voxel cells before clipping/rounding. That is incompatible with the assignment's requirement to check world-scale generation/streaming cost when an ordinary 3 ms gameplay budget must publish the region during a 60 s evidence replay.

## Verdict
Rejected for closure. Keep the opening correction. Treat Rossdam as a real product streaming-cost issue and optimize the authored lake footprint/depth without raising budgets or weakening readiness.

## Next step
Bound the deliberate modern blockout lake to 90 m x 45 m x 2.4 m (still substantial and exactly at the production acceptance floor), preserving physical carve + non-solid fill and semantic shoreline routing. During settlement evidence, stream from the settlement centre until all four building columns settle, then move once to the survey camera for stable renderer coverage. Add a player-height road-arrival capture after Moordell is loaded.