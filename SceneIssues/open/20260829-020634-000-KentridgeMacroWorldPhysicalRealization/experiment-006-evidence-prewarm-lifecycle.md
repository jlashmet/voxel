# Experiment 006 — remote evidence prewarm lifecycle

## Hypothesis
The validation-only `GenerateRegionBlocking` prewarm is not a safe substitute for normal player-centred streaming: it realizes remote feature regions during the opening, then those regions leave the resident ring before the evidence camera arrives, leaving later captures with terrain/roads but missing settlement feature meshes. Removing prewarm should restore the same streamed blockout visibility seen before prewarm was introduced and also reduce opening-time stalls.

## Action / source
Inspected exact source `0afbb5ca234b87f2606bc5bd5469d5cdac376cd2`, request `8e1e496099b64167e6210d562112467ff4da12dc`, run `33260866388`, artifact `9717246958`. Compared `macro-moordell.png` against artifact `9716890862` from green no-prewarm run `33259572439` at the same authored camera/focus.

## Result
Run `33260866388` is workflow-green and coverage-gated settlement screenshots are emitted, but Moordell/Rossdam/Fairy Village/Orc Village show terrain/roads without their required blockout buildings; lake/ridge/overview do not finish before the 60s harness. The older no-prewarm Moordell frame visibly contains a grounded stone building/roof at the same view. Current player logs also show large early blocking stalls while the prewarm sequence runs and gameplay does not release until ~41.4s despite the 4x validation timeline.

## Verdict
Prewarm is falsified as a useful evidence optimization and is the leading cause of the missing remote feature presentation. Remove validation prewarm rather than changing production world generation. Keep coverage-gated normal streaming, retain normal-time CharacterMotor traversal, and order adjacent evidence targets together so Rossdam->lake and Orc->ridge reuse resident geography.

## Next step
Delete only the evidence driver's remote blocking-prewarm path, reorder targets spatially, keep the validation-only opening acceleration and coverage gate, then issue one fresh exact-SHA request on the existing CI transport and inspect every frame.
