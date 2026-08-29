# Experiment 002 — scaled grass material clock

**Hypothesis.** The shared material publication of `_GrassTime` from scaled `Time.time` freezes visible wind during the held opening dialogue even though the player keeps rendering.

**Action / source.** On source `feffedaabe9b8631d80aaf8d867d9c5a186c1bb2`, change that publication to `Time.unscaledTime`, retain the per-draw MPB clock, and add `ApplyLighting_AdvancesGrassMaterialClockWhileGameplayTimeIsPaused`. Validate with exact request `b598b19c88503ce9d59011f196dc404934bbef36`, run `33246401704`.

**Result.** Focused regression and standalone Kentridge harness are green, but the built grass/ground raster is byte-identical from 39.3→49.3→59.3 seconds while sky pixels change. The Kentridge runtime itself does not pause Unity `timeScale`; dialogue is held by the cutscene runtime while `Update()` and real rendering continue.

**Verdict.** FALSIFIED. Scaled-vs-unscaled CPU publication was not causal. A second custom `_GrassTime` publisher still produces no visible vertex motion.

**Next step.** Use the moving sky as a discriminator: it visibly animates from engine-managed shader `_Time.y` in the same player/render pipeline. Remove custom grass clock plumbing and drive the existing wind formula from that proven GPU clock.
