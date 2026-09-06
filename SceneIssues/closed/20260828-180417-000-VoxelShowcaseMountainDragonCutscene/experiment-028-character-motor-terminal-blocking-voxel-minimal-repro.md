# Experiment 028 - terminal CharacterMotor blocking-voxel minimal repro

## Trigger
Fresh exact-source run `33831759558` baked a source-matched VoxelShowcase payload and advanced ordinary grounded replay through `resolved-93`, then repeatedly hard-stopped at feet approximately `(-112.000,49.400,21.608)` m while targeting `resolved-94` at `(-112.0,20.0)` m X/Z. The replay remained grounded with about `1.608 m` horizontal distance and `0.000 m` one-second movement until timeout.

The built-player blocker discriminator already separates the production collision sources at that stop: the half-voxel X probe is clear, the intended negative-Z probe reports `voxel:true/wood:false`, the raised step-up position is clear, and the raised negative-Z probe remains `voxel:true/wood:false`. Semantic tree wood is therefore rejected, and the normal 0.3 m grounded step-up cannot clear the obstruction.

## Prior exclusions
Experiments 025-027 already reject insufficient cut allowance, corridor winner/order discontinuity, and incorrect realized top-solid road columns at the earlier terminal approach. They do not identify a side/body/head voxel entering the exact production player AABB at the later `resolved-94` stop.

## Minimal discriminator
`MountainDragonCharacterMotorBlockerDiagnosticTests.CurrentProductionTerminalCapsuleSerializesBlockingVoxelForCollisionIsolation` is diagnostic-only. It:

1. generates the real production `ShowcaseWorld` regions intersected by the grounded and step-up capsule at the recorded built-player stop;
2. resolves `CharacterMotor` by its production assembly and invokes its private `FootMin`, `FootMax`, and `IsBlocked` methods directly, so the blocked/clear decision is the exact shipped collision path rather than a duplicated policy;
3. reads the motor's current production `Radius`, `Height`, and `StepHeight` fields and uses the production half-voxel movement probe distance;
4. verifies current and raised positions are clear while negative-Z and raised-negative-Z probes are blocked; and
5. enumerates the occupied authoritative voxel coordinates/material ids inside each exact queried AABB, including each blocker's vertical offset relative to the feet.

No route geometry, motor behavior/tolerance, grade/cut-fill, summit placement, terrain realization, vegetation policy, shared corridor policy, or runtime allocation budget changes in this experiment. The larger brick pool exists only inside this two-region blocking diagnostic because `GenerateRegionBlocking` bypasses streaming eviction.

## Decision rule
Run only this focused EditMode diagnostic on the exact feature source through `ci-test/fixes/agent-4`. If the newly entered cells belong to road/terrain realization, repair that owning realization boundary with an independent regression. If they belong to a scene-composed summit/support/other feature, keep the repair scene-specific. Do not request another full bake/replay until the blocker coordinates/material identify the owning system.
