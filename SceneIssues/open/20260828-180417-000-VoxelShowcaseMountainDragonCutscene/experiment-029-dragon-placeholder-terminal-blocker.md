# Experiment 029 — dragon placeholder terminal blocker

## Question

After Experiments 025-028 ruled out terrain-corridor height, corridor winner/order, realized top-solid road columns, and vegetation, what authoritative production voxel blocks the grounded `CharacterMotor` sweep near the summit?

## Exact evidence

Targeted CI request `fe1bac7d2543857ccfc7f9fb5887184b31c6a762` validated exact feature source `7fc8dd0886dc4717443f67b20b02742f016c72ff` in workflow run `33835068338`.

`MountainDragonCharacterMotorBlockerDiagnosticTests.CurrentProductionTerminalCapsuleSerializesBlockingVoxelForCollisionIsolation` passed and reproduced the production half-voxel negative-Z sweep. The current position and raised current position were occupiable, while the forward sweep and the same sweep after the normal 0.3 m step-up were blocked. Every occupied blocker cell serialized by the diagnostic used material `9`.

In the current Showcase material composition, material `9` is the bright-red `WorldgenCloth` role supplied to `WorldBuilderMountainSummitPlaceholderCatalogue`. The placeholder is a solid 60 dm cube centred on the authoritative summit mass. The terminal authored road leg continued to that same summit centre, so normal traversal eventually drove the player capsule into the dragon cube itself.

The same workflow's standalone SceneIssue replay step completed successfully; the overall workflow result was failure because the persistent Unity Test Runner later hit its known temporary init-scene/PostbuildCleanup lifecycle failure. That cleanup failure does not change the blocker evidence above.

## Root cause

The shared road resolver, realized terrain corridor, `CharacterMotor`, and vegetation are not the terminal product defect. Showcase composition placed two valid solid concepts on the same terminal space: the road arrival and the dragon placeholder footprint.

## Chosen correction

Keep the reusable mountain, road, placeholder, and collision systems unchanged. Keep the dragon centred on the broad summit. Change only Showcase-owned ascent intent so the final semantic control remains on the summit but stops beside the placeholder: one path width beyond the placeholder half-size along the existing summit approach direction.

This preserves a supported summit arrival, leaves ordinary player-scale clearance from the solid cube, keeps the proximity trigger derived from the resolved final route point, and avoids scene-specific collision exceptions or weakened motor policy.

A module-local regression now requires the resolved terminal road point to clear the placeholder AABB by at least half the road width while keeping the road centreline plus half-width on the broad summit crest.
