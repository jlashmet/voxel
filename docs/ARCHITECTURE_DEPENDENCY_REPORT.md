# Final Architecture Dependency Report

This is the repository-persisted copy of the final static dependency report accepted for source `2c451f2759f4c04649eeab3dba88edc56efd2c46` by GitHub Actions run `31910012496` (`Final architecture static acceptance`). The permanent generator/acceptance gate is `.github/workflows/final-architecture-static.yml`.

## VoxelEngine assembly edges

- `VoxelEngine.CI.Editor` (`Assets/VoxelEngine/CI/Editor/VoxelEngine.CI.Editor.asmdef`): `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Showcase`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Terrain.Runtime`, `VoxelEngine.Vegetation.Api`, `VoxelEngine.Vegetation.Runtime`
- `VoxelEngine.CI.PlayMode` (`Assets/VoxelEngine/CI/PlayMode/VoxelEngine.CI.PlayMode.asmdef`): `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Showcase`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Vegetation.Api`, `VoxelEngine.Vegetation.Runtime`
- `VoxelEngine.Collision.Api` (`Assets/VoxelEngine/Collision/Api/VoxelEngine.Collision.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Collision.Runtime` (`Assets/VoxelEngine/Collision/Runtime/VoxelEngine.Collision.Runtime.asmdef`): `VoxelEngine.Collision.Api`, `VoxelEngine.Storage.Api`
- `VoxelEngine.Composition` (`Assets/VoxelEngine/Composition/VoxelEngine.Composition.asmdef`): `VoxelEngine.Collision.Api`, `VoxelEngine.Edits.Api`, `VoxelEngine.Edits.Runtime`, `VoxelEngine.Foundation`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Rendering.Api`, `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api`, `VoxelEngine.Vegetation.Runtime`
- `VoxelEngine.Edits.Api` (`Assets/VoxelEngine/Edits/Api/VoxelEngine.Edits.Api.asmdef`): `VoxelEngine.Storage.Api`
- `VoxelEngine.Edits.Runtime` (`Assets/VoxelEngine/Edits/Runtime/VoxelEngine.Edits.Runtime.asmdef`): `VoxelEngine.Edits.Api`, `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`
- `VoxelEngine.Foundation` (`Assets/VoxelEngine/Foundation/VoxelEngine.Foundation.asmdef`): no VoxelEngine references
- `VoxelEngine.Net.Api` (`Assets/VoxelEngine/Net/Api/VoxelEngine.Net.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Net.Runtime` (`Assets/VoxelEngine/Net/Runtime/VoxelEngine.Net.Runtime.asmdef`): `VoxelEngine.Edits.Api`, `VoxelEngine.Foundation`, `VoxelEngine.Net.Api`, `VoxelEngine.Storage.Api`, `VoxelEngine.Streaming.Api`
- `VoxelEngine.Rendering.Api` (`Assets/VoxelEngine/Rendering/Api/VoxelEngine.Rendering.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Rendering.Runtime` (`Assets/VoxelEngine/Rendering/Runtime/VoxelEngine.Rendering.Runtime.asmdef`): `VoxelEngine.Storage.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api`
- `VoxelEngine.Showcase` (`Assets/Scenes/Showcase/VoxelEngine.Showcase.asmdef`): `VoxelEngine.Collision.Api`, `VoxelEngine.Composition`, `VoxelEngine.Edits.Api`, `VoxelEngine.Rendering.Api`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api`
- `VoxelEngine.Storage.Api` (`Assets/VoxelEngine/Storage/Api/VoxelEngine.Storage.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Storage.Runtime` (`Assets/VoxelEngine/Storage/Runtime/VoxelEngine.Storage.Runtime.asmdef`): `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`
- `VoxelEngine.Streaming.Api` (`Assets/VoxelEngine/Streaming/Api/VoxelEngine.Streaming.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Streaming.Runtime` (`Assets/VoxelEngine/Streaming/Runtime/VoxelEngine.Streaming.Runtime.asmdef`): `VoxelEngine.Storage.Api`, `VoxelEngine.Streaming.Api`, `VoxelEngine.Tiering.Api`
- `VoxelEngine.StructuralIntegrity.Api` (`Assets/VoxelEngine/StructuralIntegrity/Api/VoxelEngine.StructuralIntegrity.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.StructuralIntegrity.Runtime` (`Assets/VoxelEngine/StructuralIntegrity/Runtime/VoxelEngine.StructuralIntegrity.Runtime.asmdef`): `VoxelEngine.Storage.Api`
- `VoxelEngine.Structures.Api` (`Assets/VoxelEngine/Structures/Api/VoxelEngine.Structures.Api.asmdef`): `VoxelEngine.Storage.Api`
- `VoxelEngine.Structures.Runtime` (`Assets/VoxelEngine/Structures/Runtime/VoxelEngine.Structures.Runtime.asmdef`): `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `VoxelEngine.Terrain.Api`
- `VoxelEngine.Terrain.Api` (`Assets/VoxelEngine/Terrain/Api/VoxelEngine.Terrain.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Terrain.Runtime` (`Assets/VoxelEngine/Terrain/Runtime/VoxelEngine.Terrain.Runtime.asmdef`): `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`, `VoxelEngine.Terrain.Api`
- `VoxelEngine.Tests.EditMode` (`Assets/Tests/EditMode/VoxelEngine.Tests.EditMode.asmdef`): `VoxelEngine.Collision.Runtime`, `VoxelEngine.Composition`, `VoxelEngine.Edits.Api`, `VoxelEngine.Edits.Runtime`, `VoxelEngine.Net.Api`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Streaming.Api`, `VoxelEngine.Streaming.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Terrain.Runtime`, `VoxelEngine.Tiering.Api`
- `VoxelEngine.Tests.Features` (`Assets/Tests/Features/VoxelEngine.Tests.Features.asmdef`): `VoxelEngine.Edits.Api`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`
- `VoxelEngine.Tests.Parity` (`Assets/Tests/Parity/VoxelEngine.Tests.Parity.asmdef`): `VoxelEngine.Edits.Api`, `VoxelEngine.Edits.Runtime`, `VoxelEngine.Net.Api`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.StructuralIntegrity.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Terrain.Runtime`, `VoxelEngine.Tiering.Api`
- `VoxelEngine.Tests.PlayMode` (`Assets/Tests/PlayMode/VoxelEngine.Tests.PlayMode.asmdef`): `VoxelEngine.Collision.Api`, `VoxelEngine.Collision.Runtime`, `VoxelEngine.Edits.Api`, `VoxelEngine.Net.Api`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Showcase`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Streaming.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Tiering.Api`
- `VoxelEngine.Tiering.Api` (`Assets/VoxelEngine/Tiering/Api/VoxelEngine.Tiering.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Vegetation.Api` (`Assets/VoxelEngine/Vegetation/Api/VoxelEngine.Vegetation.Api.asmdef`): no VoxelEngine references
- `VoxelEngine.Vegetation.Runtime` (`Assets/VoxelEngine/Vegetation/Runtime/VoxelEngine.Vegetation.Runtime.asmdef`): `VoxelEngine.Vegetation.Api`

## WorldGen assembly edges

- `MountingForce.WorldGen.Architecture` (`Packages/com.mountingforce.worldgen/Runtime/Architecture/MountingForce.WorldGen.Architecture.asmdef`): `MountingForce.WorldGen.Core`
- `MountingForce.WorldGen.Core` (`Packages/com.mountingforce.worldgen/Runtime/MountingForce.WorldGen.Core.asmdef`): no engine/worldgen references
- `MountingForce.WorldGen.Voxel` (`Packages/com.mountingforce.worldgen/Runtime/Voxel/MountingForce.WorldGen.Voxel.asmdef`): `MountingForce.WorldGen.Architecture`, `MountingForce.WorldGen.Core`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Vegetation.Api`

## Final boundary assertions

- Api assemblies reference no Runtime assemblies.
- Runtime assemblies reference no foreign Runtime assemblies.
- Composition is the only production assembly that wires concrete Runtime assemblies.
- Production `Assets/` and `Packages/` contain no `VoxelEngine.Core` references.
- Production architecture contains no `Legacy*` or `*Compatibility*` adapter source files.
- WorldGen Core/Architecture are engine-independent; WorldGen.Voxel references engine Api assemblies only.

The CI and test assemblies intentionally appear above with direct Runtime references because they are non-production verification/tooling assemblies; the permanent production guard excludes tests/editor/CI while still enforcing subsystem and Api direction rules.