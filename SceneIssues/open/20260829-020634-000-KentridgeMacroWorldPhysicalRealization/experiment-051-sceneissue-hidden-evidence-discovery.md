# Experiment 051 — SceneIssue hidden evidence discovery

## Question
Why does the focused `KentridgeMacroWorldValidation` player request Application New Game while the same macro evidence profile in the SceneIssue replay remains at `FrontEnd/MainMenu`?

## Evidence
- Exact module run `34184870430` logs `MACROEVIDENCE opening-time-scale=12`, `SYSTEM24 frontend: lifecycle=FrontEnd screen=MainMenu`, then `MACROEVIDENCE application-new-game-requested`.
- Exact SceneIssue replay in `34188460121` logs `MACROEVIDENCE opening-time-scale=12` before the same `SYSTEM24 frontend` state, but never logs an Application New Game request and produces no macro traversal/capture phases.
- The dedicated module bootstrap adds `KentridgeMacroWorldEvidenceDriver` to the visible `KentridgePlayableSlice` object. The SceneIssue profile installer instead creates `Kentridge Macro World Evidence` with `HideFlags.DontSave` and adds the driver there.
- The application-start companion used `FindFirstObjectByType<KentridgeMacroWorldEvidenceDriver>()`. Its five-second retry changed timing but not the discovery API, so the repeated failure falsifies the earlier late-install hypothesis.
- The same `34188460121` SceneIssue player shows the current GPU renderer fully converged (`missingVisible=0`, no pending/in-flight coverage work), separating this startup problem from renderer convergence.
- The repository-derived persistent test editor in `34188460121` separately crashed natively inside Burst `LibraryCompiler.GetAotCompilerOptions`; there were no C# compile errors or failed test results to attribute to the source change.

## Root cause and correction
SceneIssue evidence is intentionally hidden from ordinary scene-object discovery. Use a validation-safe lookup (`Resources.FindObjectsOfTypeAll`) and require an active/enabled evidence component, throttled during the existing bounded discovery window. Add an EditMode regression that creates the evidence component on a `HideFlags.DontSave` GameObject and proves the application-start discovery sees it while ignoring it when disabled.

This correction is validation-only lifecycle plumbing. It does not force-generate content, widen streaming residency, raise budgets, bypass Application/session orchestration, or change production world authority.
