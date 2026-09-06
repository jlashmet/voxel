# Experiment 037 — exact module/player and magenta discriminator

## Observation
Exact targeted run `33991882237` used transport `86c9d2ac40f4705a36aa153e4c23783f3751f507` for feature source `71b1f4da83492fbc158e96a84703b1734362ca4e`. The requested startup-bake test passed inside its owning persistent EditMode assembly, and all 17 persisted EditMode assembly summaries reported zero effective failures. The automatic module gate nevertheless failed before completing player validation.

The deterministic module-player failure was configuration, not infrastructure: `Assets/Game/Cutscenes/Validation/CutsceneDialogueValidation.player-scenario.json` requested `runSeconds: 6`, while the shared player validator accepts only 10–300 seconds. The standalone SceneIssue replay still ran through its `always()` path, reached `WAYPOINT_REPLAY 92/92`, completed, and emitted the required captures.

## Visual discriminator
Human review rejects those fresh production captures. Large regions are effectively Unity error magenta (approximately RGB 255/5/255), so the remaining visual failure is not an authored purple palette. `ProceduralFarFeatureRenderer` created its runtime material through a dynamically resolved pipeline shader, leaving shader validity/player packaging implicit.

## Selected correction
- Raise the Cutscenes validation scenario to a contract-valid 10 seconds and assert the production fixture's actual dialogue-active log.
- Give Rendering a renderer-owned `Resources/ProceduralFarFeature.shader`; `ProceduralFarFeatureRenderer` loads that exact resource and throws if it is missing, unsupported, or has the wrong shader name.
- Add EditMode shader/material coverage and require the existing module-owned FarWorld player validation to prove the shader is packaged and supported before readiness.
- Preserve the existing opaque material-index catalogue boundary; no Mountain Dragon/game material recipe is added to Rendering.

## Master compatibility
Current master restored canonical `ShowcaseInputSystem`. The branch's older `Game.Input.Runtime` compatibility workaround is therefore superseded. Merge resolution keeps master input authority plus Mountain Dragon replay/runtime coverage and removes the duplicate compatibility dependency.

## Next discriminator
Run exact CI on the merged current feature SHA. Acceptance requires no automatic-plan fallback, all repository-derived module/player validation green, exception-free 92/92 production replay, and fresh captures with no white/magenta slab/AABB/error-shader artifacts.