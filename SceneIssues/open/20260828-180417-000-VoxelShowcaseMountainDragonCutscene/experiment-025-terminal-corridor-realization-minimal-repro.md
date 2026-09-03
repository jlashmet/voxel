# Experiment 025 - terminal terrain-corridor realization minimal repro

## Trigger

Exact-head run `33746226437` finally executed the fresh one-shot bake, automatic module validation and standalone SceneIssue replay on feature source `8cc7ca8368572fe8812fc2c88287ba797d63b91b`.

This run disproves the earlier assumption that current `resolved-89` is still the blocking location. The ordinary grounded replay reached:

- `resolved-89` at approximately `(-106.79, 46.80, 28.22)` feet position;
- `summit-supported` immediately afterwards at approximately `(-107.72, 47.20, 28.48)`;
- then stalled while grounded attempting current `resolved-91` at target `(-112.0, 26.0)` m X/Z.

The stable stalled feet position was approximately `(-108.50, 47.10, 27.50)`, with horizontal distance `3.808 m` remaining and one-second movement repeatedly `0.000 m`. The replay timed out at waypoint `92/97` after 100 seconds. This is a product traversal failure, not an infrastructure retry candidate.

The run exported `AcceptedShowcaseBake/ShowcaseWorld.bytes` plus matching manifest and captured all named screenshots through `06-summit-dragon-supported.png`, proving the fresh current-source payload and terminal route were actually exercised.

## Policy consequence

The previous `resolved-89` symptom survived two materially different fixes, so route controls, motor/tolerance, grade/cut-fill and summit placement remain frozen. The new stop is still inside the already-declared current points 88-91 isolation window and must be reduced to realized terrain/corridor evidence before another product fix.

## Minimal discriminator

`MountainDragonResolvedRouteDiagnosticTests.CurrentProductionTerminalCorridorSerializesForCollisionIsolation` is diagnostic-only. It:

1. rebuilds the exact production mountain surface and resolved ascent;
2. records authoritative resolved points 88-91 and their analytic mountain heights;
3. samples the current point-90 -> point-91 segment at eight deterministic intervals, recording road Y, pre-corridor terrain Y and cut/fill delta; and
4. builds the exact production road catalogue through `WorldBuilderRoadVoxelCatalogue` and serializes every shared `EmitTerrainCorridor` instruction without changing lowering policy.

The discriminator changes no route geometry, movement behavior, arrival tolerance, road profile, summit placement, shared road API or raster implementation. Its next exact-source CI result must identify whether the terminal obstruction comes from the pre-carve landform/cut-fill shape or from the generic terrain-corridor realization itself before any fix is allowed.

## Required validation

Run only the new focused diagnostic on exact feature head through `ci-test/fixes/agent-4`. Do not request another expensive bake/replay until this minimal repro has produced the terminal surface/corridor evidence and the root cause can be stated narrowly.
