# Experiment 028 — playable macro selection handoff

## Symptom
Exact run `33480730488` is workflow-green, and focused production storage still proves all 16 generic blockout shells/roofs rasterize. In the exact built player, the corrected end-frame diagnostic reads `material=0` at every authored Moordell/Rossdam/Fairy/Orc timber-wall and roof probe. Full-resolution Fairy/Orc captures show terrain/corridor content but no four-building settlement blockout.

## Hypotheses and result
1. The macro catalogue is absent because the playable never supplies the one-shot `TopDownWorldLayoutSelection` handoff.
2. The playable does supply the macro catalogue, but runtime generation/publication later fails to retain the generic settlement voxels.

Hypothesis 1 was initially plausible from `KentridgePlayableSlice.OnEnable`, but source inspection falsified it before any production change: the scene's actual compatibility `Game.Kentridge.PlayableSlice.KentridgeDefinition.Build` already builds the source-backed macro layout and calls `TopDownWorldLayoutSelection.Select(...)`. The exact failing source `fb4bc6d...` contains that code. Hightown authoring and campaign planning between that call and the catalogue build do not consume the selector.

The queued `33485694443` test exercises only the shared selector contract through the legacy backend caller. It may be useful regression evidence but is not root-cause authority and must not justify a production selection change.

## Corrected discriminator
`PlayableCompatibilityAuthoringLeavesMacroSelectionForCatalogueBuild` invokes the internal playable compatibility Kentridge/Hightown authoring adapters by reflection, preserving production API boundaries, then builds the same Kentridge production catalogue. It requires Fairy and Orc macro-town definitions to remain present after the real playable authoring order.

## Decision rule
- If the corrected discriminator passes, the selector/catalogue-install boundary is exonerated. Continue isolation at runtime region generation/publication using exact authored Fairy/Orc regions; do not change composition selection, planner, streaming radius, camera, or building geometry.
- If it fails, inspect the managed assertion to identify the actual handoff consumer before any production fix.
