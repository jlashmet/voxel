# 23 Application frontend, menus, settings & session start flow — implementation plan

**Target module:** `Assets/Game/Application/Api` / `Runtime` (`Game.Application.Api`, `Game.Application.Runtime`). Unity screen/view implementation may live under Runtime or a thin client presentation subassembly while the API remains engine-neutral.

## API

Local app lifecycle, semantic NewGame/Continue/Host/Join/Leave/Quit requests and outcomes, screen/navigation model, settings/preferences contracts, loading/readiness state, and startup failure reasons.

## Runtime

1. Implement local application flow coordinator: Boot -> FrontEnd -> StartingSession -> InGame -> ReturningToFrontEnd.
2. Delegate New Game/Continue to #14/#16 and multiplayer formation to #07; never construct gameplay domains directly.
3. Implement screen navigation and nested `Ui` input contexts using existing Input API.
4. Add user-preferences store and supported settings, including Input System binding overrides through the input adapter.
5. Distinguish in-game menu from global Time.timeScale pause.
6. Handle semantic teardown before returning to frontend and distinct Leave Game vs Quit Application.

## Dependencies

07/08 Sessions, 14/16 run start/restore, Input, 20 presentation, later Outcomes for end screens.

## Tests / proof

New Game, Continue, host/join, nested menus/context unwind, settings persistence, failed startup, leave/return, built-player frontend flow.

## Do not build

No gameplay authority, hardcoded save policy, raw socket disconnect, legacy key polling, or scene-name-driven game lifecycle.
