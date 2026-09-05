# Plan

## Acceptance and ownership
`issue.json` is the contract (`captures: []`). Preserve the source-backed Mounting Force macro graph while delivering physical settlements, contiguous terrain-aware hard routes, reusable regional geography, Rossdam Lake, Southern Ridge/pass, real CharacterMotor traversal, durable built-player evidence, and bounded CPU/GPU/memory/streaming cost.

Owned validation surfaces:
- WorldBuilder: `Validation/MacroPhysicalWorld` through production catalogue/rendering.
- Showcase: `Validation/FeatureResidency` through production residency/readiness.
- Kentridge Playable: `Validation/KentridgeMacroWorld` through the real slice/evidence driver.
- Rendering: `Validation/GpuSurfaceMirrorRelocation` through production GPU mirror/extraction/publication.

## Current evidence and hypotheses
Run `33930622766` on exact source `3c839facd018eb8118811db1a7b81644375c419f` completed the 180-second standalone replay but is closure-red: the runtime catalogue had 434 definitions with no macro town/road/lake/ridge definitions, evidence never advanced beyond Moordell, ~35,981 Input-System-only legacy-input exceptions were logged, and repository module validation failed in the Structures PlayMode surface.

Hypothesis A: renderer publication still prevents macro evidence. Falsified for the missing catalogue: startup logs prove macro definitions never reached the playable catalogue.

Hypothesis B: the scene-selected one-shot macro layout is consumed before the playable catalogue builds. Proven: `KentridgeDefinition.Build()` publishes the selection, then temporary `ShowcaseWorld` generic town realization calls the macro-consuming catalogue path before `KentridgePlayableSlice` builds its concrete catalogue.

Selected corrections:
1. Generic `WorldBuilderVoxelCatalogue` now uses an explicit local-only Kentridge catalogue path; the concrete playable catalogue remains the one-shot macro owner. `KentridgeMacroWorldBootstrapOwnershipTests` now reproduces production Showcase-first startup order.
2. Kentridge's scene compatibility bridge now reads Input System devices; HUD semantic pressed/held state routes through that bridge. Global input settings remain unchanged.
3. The selected Structures behavioral test now invokes its public deterministic production validation synchronously instead of yielding an unrelated rendered frame whose URP debug updater polls legacy Input.

Current source after these corrections: `6b1ce62cdf85a1be0beb83f25b8fbdc1118bc1de`.

## Remaining gates
1. Exact-SHA targeted CI for the current source: requested GPU relocation/churn regression, repository-derived module validation, and required 180-second SceneIssue replay must all pass.
2. Inspect full-resolution evidence from all four owned module players plus the SceneIssue replay. Require readable Moordell/Rossdam/Fairy/Orc settlements, substantial Rossdam water + constrained route, Southern Ridge/pass, macro-network overview, and representative CharacterMotor traversal with no runtime exceptions.
3. Record final convergence, vertical residency, FPS/CPU/GPU/streaming and process/managed/native/GPU memory against existing budgets.
4. Merge then-current `origin/master`, revalidate the exact merged feature SHA, complete every task/acceptance item, move only this issue `open -> closed`, then promote through PR + auto-merge and monitor required PR checks until merged.
