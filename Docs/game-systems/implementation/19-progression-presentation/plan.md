# 19 Quest & objective UI / progression presentation — implementation plan

**Target module:** `Assets/Game/ProgressionPresentation/Api` / `Runtime` (`Game.ProgressionPresentation.Api`, `Game.ProgressionPresentation.Runtime`).

## API

Coherent journal/tracked-objective read models derived from Progression snapshots, local selection/tracking/grouping state, semantic target descriptors where content exposes them. Tracking is local presentation state.

## Runtime

1. Consume one coherent Progression snapshot/revision rather than piecing together campaign collections.
2. Render quest/objective states without exposing hidden/spoiler content before the authoritative definition says it is visible.
3. Keep sorting, collapse, selection and tracking local/non-authoritative.
4. Rebuild after reconnect/restore from current snapshot.
5. Integrate with HUD only via small tracked-objective presentation API, not shared mutable state.

## Dependencies

11 Progression and 06 client replicated state; 17 HUD may consume compact projection.

## Tests / proof

Activation/completion transitions, journal rebuild, local tracking independent of authority, multiplayer shared progression, built-player validation.

## Do not build

No quest completion buttons, generic accept/decline, map/minimap system, or duplicate progression store.
