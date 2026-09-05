# Plan — HouseShowcase socketed prop composition

## Acceptance
Create `HouseShowcase` as a built-player integration scene with a production house selector, house-specific optional prop multi-select, visible seed, Regenerate, and practical exterior/interior inspection. The preview must use the real structure/decor/socket/material/voxel/rendering path. Same house + palette + seed must replay deterministically; Regenerate preserves house/palette, changes seed, and produces a materially different valid production result.

## Ownership / architecture
Implementation base was `51797c954490425964e602d6bb2252a0d7a7c5aa`. `GuildHouseProgramCatalog` owns all ten production `GuildHouseKind` registrations. Generated room programs own required/optional archetypes; canonical decoration recipes own identity, family, mount/socket compatibility, size, clearance, and backend. `DecorationPlacementResolver` and `RectangularDecorationSpaceAnalyzer` remain the placement authorities.

`HouseShowcase` is only an integration/UI consumer. Structures owns reusable catalog queries, furnishing policy, palette composition, socket placement, and production shell/facade authoring. The affected player-visible Structures module now owns `Assets/Game/Structures/Validation/GuildHouseFurnishingRuntimeValidation.unity` plus its player scenario.

## Investigation result / selected fix
Filtering placements after composition was rejected because it would bypass production selection semantics. A canonical query surface was added for house enumeration, semantic metadata, normalized decoration identity, and required/optional applicability. Palette-aware composition consumes generated rooms' production required/optional archetypes, resolves canonical descriptors, and delegates placement to the existing analyzer/resolver. Required fixtures remain mandatory; optional identities are filtered before placement. Selected-but-unplaced items report `RoomUnavailable` or `NoValidPlacement`; no magic-coordinate fallback is used.

A later built-player review found the Knight exterior still read as disconnected blockout slabs and the first Wizard interior reset clipped nearby geometry. The correction was made in shared production house shell/facade authoring and HouseShowcase framing, not with showcase-only primitives. Reusable facade/door/roof articulation and collision-safe inspection framing were added with Structures regressions.

## Final validation / remaining gate
Feature source `3debd2b00b48e10c22c18c4de4e0c7787f586d6f` was validated by exact request `2b032ea09368239fbb6ceff2ceceff10c8bacc31`, workflow run `33957863977`. Automatic module validation and standalone HouseShowcase SceneIssue replay both succeeded, and durable screenshot previews/result artifacts were emitted. All feature acceptance checkboxes are complete.

Remaining work is closure bookkeeping, merge current `master` if it advanced, then PR + auto-merge and the required PR `affected` gate.
