# Experiment 017 — validation ownership and capture handoff

## Hypothesis
The Rossdam stall in exact run `33311348299` is caused by competing validation automation taking streaming demand at t=50, while the unreadable Moordell screenshot is a separate queued-frame camera handoff; neither requires changing production streaming semantics.

## Evidence
Exact source `3993431c740b6a2da16b1270e1ede22831e50955`, CI wrapper `b8089e2ca66a6942797869c41c3c7939682f5712`, run `33311348299`, artifact `9732113329`.

Before t=50 Rossdam telemetry is stable (`visible=275`, `missingVisible=0`, `residentGround=116.6m`, `coverage=True`). At t=50 the generic real-player smoke harness sets `AutoSurvey`; residency immediately leaves Rossdam (`visible=0`, `residentGround=14.2m`, `coverage=False`) and spends several seconds rebuilding. The same artifact's full-resolution `macro-moordell.png` is player-height/horizon framing with tiny distant geometry: `CaptureScreenshot` queued the image, then the Moordell continuation stopped applying the survey camera before that frame rendered.

## Action
On the dormant `kentridge-macro-world` driver only, reassert `AutoSurvey=false` / `AutoRecede=false` in `LateUpdate`, hold Moordell survey framing through the existing post-capture dwell, and add <=1 Hz diagnostics over only the already-required target content columns. Extend the real-driver PlayMode regression for automation ownership and survey hold. No production scheduler, load radius, CharacterMotor, device budget, or replay duration changes.

## Verdict / next step
The prior run is rejected for closure but provides a proven smallest discriminator. Final exact-SHA CI must show uninterrupted Rossdam convergence and readable survey captures. If Rossdam still stalls, the new diagnostic must identify the exact pending presentation column before any shared production scheduling change.
