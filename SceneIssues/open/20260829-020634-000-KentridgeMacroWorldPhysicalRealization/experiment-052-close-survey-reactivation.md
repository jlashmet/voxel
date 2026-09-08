# Experiment 052 — reactivate close settlement survey composition

## Exact source / run
- Feature source: `e43ae0356e562de9eb153753a520899db8b895df`
- CI transport: `a1b72d214a7d3bb44b30b840d6e8c0f8190fd3aa`
- Workflow run: `34190241434`
- Artifact: `single-test-34190241434` (`10042303960`)

## What the exact run proved
The hidden-evidence lifecycle correction is valid. Persistent EditMode evidence includes a green `DontSaveSceneIssueEvidenceIsFoundByValidationSafeDiscovery`; both the focused macro module player and the full 180-second SceneIssue replay emit `MACROEVIDENCE application-new-game-requested`, real local `CharacterMotor` traversal, Moordell settlement capture, macro-road `CharacterMotor` traversal, and Moordell road-arrival capture with no runtime exceptions.

The remaining red moved cleanly to Rossdam presentation. Both players reach `content-ready target=rossdam`, but neither reaches `capture-ready target=rossdam` or `macro-network-overview` before 180 seconds. Current GPU publication keeps making progress but strict coverage remains incomplete; module tail telemetry is roughly `missingVisible=193-197`, `flight=8`, phase 2, with no-slot pressure. This is not evidence that Rossdam world content is missing.

## New discriminator
The previously selected experiment-047 close-settlement correction is not active in either exact player:
- Both exact logs contain zero `MACROEVIDENCE close-survey` events.
- Both still announce Rossdam with the evidence driver's legacy `cameraHeightM=70.0` survey.
- Full-resolution `macro-moordell.png` remains a near-nadir/top-down blockout view, matching experiment 047's previously rejected framing/workload rather than its selected 31 m close-oblique composition.

Source inspection explains why the correction silently went dormant:
1. `KentridgeMacroWorldValidationBootstrap` explicitly attaches only evidence + content-demand helpers. The module player has no SceneIssue profile, so `KentridgeMacroWorldSettlementSurveyComposition` is never attached there.
2. The SceneIssue survey installer reads only `-voxel-validation-profile`, while the standalone SceneIssue path selects the profile through `-voxel-scene-issue` JSON.
3. The survey helper itself uses ordinary `FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>()`, which cannot reliably discover the SceneIssue driver's `HideFlags.DontSave` host—the same hidden-object boundary already demonstrated in experiment 051.
4. The old helper pins CharacterMotor demand only in `LateUpdate`. The evidence driver at execution order -100 therefore restores the legacy 70 m demand in `Update`, production `KentridgePlayableSlice.Update` streams that old point, and only afterward does the -90 helper move the camera/motor. That contradicts experiment 047's intended streaming/presentation alignment.

## Selected correction
Reactivate the existing experiment-047 validation composition without changing acceptance:
- Explicitly attach close-survey composition in the dedicated macro module bootstrap alongside evidence and content-demand helpers.
- Let the SceneIssue installer derive `validationProfile` from either `-voxel-validation-profile` or the existing `-voxel-scene-issue` JSON.
- Use validation-safe `Resources.FindObjectsOfTypeAll` discovery for the hidden evidence driver and cover that exact behavior in EditMode.
- At execution order -90, override the close-settlement CharacterMotor demand in `Update`, after the -100 evidence driver but before production streaming, then apply the same close camera in `LateUpdate`.
- Require a Rossdam `close-survey` log in the macro module scenario so this correction cannot silently disappear again.

The restored pose remains the already-selected 31 m high / 22 m horizontal close-oblique view with the existing FOV cap. No renderer production code, streaming radius, GPU concurrency, upload/extraction budget, content generation, or strict coverage threshold changes.

## Required proof
The next exact source must show the close-survey log in the focused module player, keep tests and real CharacterMotor traversal green, and demonstrate whether strict settlement publication now converges quickly enough to advance beyond Rossdam. If the same Rossdam coverage symptom survives with the close survey demonstrably active, isolate that new exact state before any further production change.
