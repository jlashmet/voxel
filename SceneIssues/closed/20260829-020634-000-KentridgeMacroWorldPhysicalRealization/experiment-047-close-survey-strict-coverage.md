# Experiment 047 — close settlement survey + strict coverage window

## Exact source / request
- Feature source: `36c40e394a083892a0de84f02e62bb7c9e036b92`
- CI transport: `8235b110073ed1e71c144dcdfd5751661e9ae6d9`
- Workflow run: `33915899972`
- Artifact: `single-test-33915899972` (`9954166316`)

## Result classification
The explicitly requested production GPU liveness regression passed in 39.34s:
`VoxelEngine.Tests.PlayMode.GpuSurfaceMirrorRelocationRequestedValidationTests.DistantUnrelatedChangeChurnExecutesProductionGpuLivenessRegression`.
This validates the footprint-local mirror invalidation correction on the exact feature source.

Automatic module validation then failed only after entering `KentridgeMacroWorldValidation`: its 50s player scenario ended before the required `MACROEVIDENCE traversal=CharacterMotor-local` pattern. The module log was not deadlocked: `missingVisible` monotonically fell from 397 to 3 and `coreAbsent=0`; the harness terminated at exactly 50s immediately before strict opening coverage could release gameplay. This is a validation-window defect, not a missing bootstrap or campaign failure.

The required 180s SceneIssue replay had zero harness assertions and did reach real gameplay: local CharacterMotor traversal was 3.83m and Moordell authored content became ready at ~78s. It still never reached `capture-ready`: strict coverage remained false and final `missingVisible` was ~233-234. No mirror slot/directory refusal or eviction was reported. The full-resolution 94s and 174s frames also remained visually closure-red: the supposed close Moordell evidence was effectively a near-nadir map view with no readable grounded building massing.

## Framing / workload discriminator
The evidence driver authored settlement surveys at 70m height with only 60dm (6m) X/Z offset from focus. The validation survey helper widened the normal 58-degree Kentridge camera to 90 degrees. That makes the settlement nearly straight-down and pushes most visible settlement geometry into the step-2 band, where each extraction worker requests an approximately 18^3 source-brick footprint. This simultaneously conflicts with the issue's explicit requirement for closer Moordell/Rossdam/Fairy/Orc evidence and inflates the strict mirror-coverage workload.

## Selected correction
- `8425e7a8f96656d1d3ec95456d03ff3449ac2bcd`: validation-only settlement survey composition now overrides only active settlement evidence targets to a close oblique 31m-high view with 260dm X/Z offset, caps rather than widens FOV (the shipped 58-degree lens remains 58), and moves the CharacterMotor streaming authority to the same camera point before production streaming runs. Semantic target selection, content-settlement checks, strict renderer coverage, capture timing, normal gameplay cameras, world generation, residency radius, LOD thresholds, and renderer/device budgets are unchanged.
- `c93cd9ca06f30dad3c5267a7cfe888321aadc1bd`: the Kentridge macro module player window is extended from 50s to 120s so the production opening + strict-coverage path has time to reach the assertions it already requires.

## Required proof
The next exact-SHA CI must keep the requested GPU discriminator green, allow repository-derived module validation to proceed through all mandatory module-local players, and show that the close settlement composition reaches strict Moordell coverage with readable physical settlement evidence. The 180s SceneIssue replay must then advance beyond Moordell toward Rossdam/Fairy/Orc/ridge/network evidence without any budget increase or coverage weakening.
