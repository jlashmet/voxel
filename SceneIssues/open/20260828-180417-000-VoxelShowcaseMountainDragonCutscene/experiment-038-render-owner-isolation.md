# Experiment 038 — attribute persistent magenta before another fix

## Rejected evidence
Run 34001756898, feature a4a3df0d1756fb495f7faa477ce6b007c82dfaca, CI transport 256b01f03aecbcff10d2783375529d9efdd653cb. Original artifact 9980400666 (ZIP SHA-256 a48cb9af4b03c16c6df6a4ad66f8f4510d310f883525acc4c1d38faad13452e0). All seven SceneIssue route captures are below acceptance: approach/base show large error-magenta surfaces and flat gray masses. Do not promote the candidate bake or close the assignment.

Player-build.log lines 3309–3329 explicitly retain/serialize the new Voxel/ProceduralFarFeature shader: two variants per Metal stage, four internal programs. This falsifies the claim that adding that resource alone fixes the current output; it does not prove which other shader/material/draw is responsible. player-run.log contains no explanatory shader exception and reports semantic far instances during the broken approach.

## Discriminating experiment
The temporary ShowcaseRenderIsolationDiagnostic runs only with this exact SceneIssue's normal command-line replay. It waits for 01-mountain-approach.png, pauses the existing replay inputs (no teleport), then writes labelled all-before / no-semantic-far / no-component-renderers / no-voxel-surface / all-restored captures. It inventories material name, shader, support, instancing, pipeline tag, pass names, base colour and keywords plus component ownership. Every suppressed setting is restored, including on exception/destruction. World state/collision/material contents stay unchanged. These modified-rendering frames are NOT acceptance evidence.

Compare magenta pixel counts and geometry against both full-rendering frames before deciding a fix. Classifier: R/B >=240 and G <=16; baseline failure requires at least max(64, pixelCount/1000) matching pixels. This is an error-colour diagnostic, not a substitute for visual review or a universal art-quality metric. The original broken frame causes a deliberate scene-run failure even if traversal succeeds.

## Separate evidence defect
ModuleValidation/Results/Players is keyed only by module. In this artifact the Rendering directory retains Water player logs/screenshots, not the earlier FarWorld scene; Showcase/SceneRuntime similarly retains the last scenario. Preserve per-scenario outputs before final acceptance so successful earlier players remain independently inspectable. Do not infer FarWorld visual success from Water screenshots.

## Status
Diagnostic implementation prepared; no new render-owner result or corrected screenshot claimed. Await exact-SHA CI, then remove temporary exclusion instrumentation after recording the cause and retaining a focused regression.
