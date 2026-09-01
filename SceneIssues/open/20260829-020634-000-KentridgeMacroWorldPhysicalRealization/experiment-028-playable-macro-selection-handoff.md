# Experiment 028 — playable macro selection handoff

## Symptom
Exact run `33480730488` is workflow-green, and the focused production-storage acceptance still proves all 16 generic blockout shells/roofs rasterize. In the exact built player, however, the corrected end-frame diagnostic reads `material=0` at every authored Moordell/Rossdam/Fairy/Orc timber-wall and roof probe. Full-resolution Fairy/Orc captures show terrain/corridor content but no four-building settlement blockout.

## Competing hypotheses
1. The macro catalogue is present in the playable, but publication/storage later drops or overwrites generic settlements.
2. The playable never receives the one-shot macro-layout selection, so `KentridgeCombinedVoxelCatalogue.Build(...)` returns only local Kentridge content; visible remote/corridor content is not proof that the macro catalogue was installed.

## Root-cause discriminator
`TopDownWorldLayoutSelection` is explicitly one-shot. `KentridgeCombinedVoxelCatalogue.AddSelectedMacroWorld` returns the local catalogue when `TryConsume(seed, ...)` fails. `KentridgePlayableSlice.OnEnable` immediately builds the Kentridge catalogue and does not itself establish a macro selection.

`KentridgeMacroWorldPlayableCatalogueStreamingTests.PlayableKentridgeCatalogueRequiresExplicitOneShotMacroSelection` now reproduces that exact boundary without changing production behavior: after deterministically clearing the one-shot handoff, the production Kentridge catalogue must contain no Fairy/Orc macro-town definitions; after selecting the source-backed layout with the same root/cell semantics used by the physical planner, the same production builder must contain both.

## Decision rule
- If focused CI confirms this discriminator, treat missing playable composition selection as the demonstrated owner. The fix must live in Kentridge playable composition and select the existing source-backed macro layout before the existing production catalogue build; do not change the shared one-shot selection contract, physical planner, streaming radius, evidence camera, or building geometry.
- If the discriminator fails, inspect the managed assertion and continue root-cause isolation before another production change.
