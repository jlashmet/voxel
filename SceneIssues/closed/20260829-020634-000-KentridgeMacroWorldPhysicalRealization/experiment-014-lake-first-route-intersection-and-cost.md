# Experiment 014 — lake-first route intersection and cost

## Exact result
Run `33294931953`, exact feature source `9518ace5a682dcc6f57783f469d57623fc749c47`, is product-red. The requested PlayMode test emits NUnit XML and fails because `road-kentridge-rossdam` has `GeographyConstrained == false`; this is not a Burst/runner classification. The matching 60-second built player is runtime-clean but reaches only macro road, Moordell, Rossdam Lake, and Rossdam settlement before cutoff.

## Discriminator
Resolved Rossdam Lake is centred at `(868,3325)dm` with accepted half-extents `450 x 225dm`. The evidence driver's closest-route calculation plus its exact camera `(857,2780)dm` implies the direct Rossdam route passes closest near `(860,2920)dm`. Therefore the lake edge begins about 180dm north of the direct route: the semantic `GoAround` constraint is no longer exercised because the authored blocker does not actually block the direct route.

The same run falsifies lake-first scheduling. Moordell completes around the 29-second harness capture, lake readiness/capture consumes roughly the next 12 seconds, and Rossdam publication consumes the remainder to ~60 seconds. Moving the lake ahead of Rossdam does not reuse enough work to finish the southern evidence targets.

## Next experiment
Keep the deterministic `900 x 450 x 24dm` lake size but add a southward semantic offset so the direct route genuinely intersects it while the destination stays dry. Preserve full carved depth while replacing the full-depth non-solid water fill with a 2dm surface sheet; expected aggregate primitive scan falls from `16,591,536` to about `9,281,584` cells. Restore the previously faster `Moordell -> Rossdam -> lake -> Fairy -> Orc -> ridge -> network` evidence order. Validate through the existing focused behavioral target and exact built-player artifact; do not increase production budgets or the 60-second replay cap.