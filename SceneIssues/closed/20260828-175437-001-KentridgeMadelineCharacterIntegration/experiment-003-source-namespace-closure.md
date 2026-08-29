# Experiment 003 — source namespace / assembly closure

- Source SHA: `6345c1722f0e47b98442a910431b4c227d871e27`
- CI request SHA: `0a77888d5df94436f7c1fc93eafe4008e41c2336`
- Workflow run: `33216799378`
- Result: **product failure before behavioral execution**. Both the focused PlayMode test and configured standalone Kentridge player build stopped on the same compiler errors in `KentridgeCharacterHost.cs`: unresolved `NpcRef` and `CharacterMotor`.
- Discriminator: `ShowcaseWorld` and `CharacterMotor` use the same `VoxelEngine.Showcase` namespace but are compiled by different assemblies (`Game.Composition.Showcase` vs `VoxelEngine.Showcase`). The prior dependency fix retained the former but dropped the latter. `NpcRef` is exposed from the already-referenced `Game.WorldBuilder.Api` namespace, which the extracted host had not imported.
- Corrective action: restore the exact `VoxelEngine.Showcase` assembly reference and import `Game.WorldBuilder.Api`; do not change the test filter, player profile, scene capture transport, or behavior.
