# 23 Application frontend, menus, settings & session start flow — implementation plan

**Target module:** `Assets/Game/Application/Api` / `Runtime` (`Game.Application.Api`, `Game.Application.Runtime`). Unity screen/view implementation may live under Runtime or a thin client presentation subassembly while the API remains engine-neutral.

## Acceptance and ownership

Application owns local frontend lifecycle, screen/navigation state, semantic user intents, supported local preferences, loading/readiness presentation, and startup failures. It delegates gameplay/session authority to Sessions, SessionOrchestration, Persistence, SessionPresentation, Outcomes, and Input APIs. It must not construct gameplay domains, open sockets, poll raw physical input, own scene-name lifecycle, pause simulation globally, or decide game outcomes.

## Implemented approach

`ApplicationFlowCoordinator` now drives `Boot -> FrontEnd -> StartingSession -> InGame -> ReturningToFrontEnd`, plus `Exiting`, serializes conflicting intents, waits for semantic `GameplayReady`, handles New/Continue/Host/Join/Leave/Quit through owning APIs, maintains nested local screen/InputContext state, persists approved preferences and binding overrides, and presents outcome state without owning resolution. Module-local Application validation exercises the same production coordinator path.

## Blast radius / cost

The production diff is centered on `Game.Application.Api` / `Runtime` with semantic API dependencies on Sessions, SessionOrchestration, Persistence, SessionPresentation, Outcomes, and Input. No gameplay tick-rate, interest-radius, renderer, voxel-authority, or device-budget policy changed. Repository-derived exact-SHA CI selected and passed all affected test assemblies and module validations plus standalone Kentridge integration.

## Validation result

Exact feature SHA `a148f6fbf014c8446feaf309df14c1fe215955cc` passed targeted request `552dcc59ee098a8cd5c7b41489be6d5ed5e4e7ce` / workflow run `33913393877`: affected EditMode/unit assemblies, module-local Application/Input built-player validation, dependent module validations, and canonical standalone `KentridgePlayableSlice` all succeeded.

## Remaining gates

Implementation and assignment-specific validation are complete. Remaining repository workflow only: commit closure bookkeeping, merge current `origin/master` into `fixes/agent-8`, open/update PR to `master`, enable auto-merge, and require the PR `affected` gate to pass before considering the assignment complete.
