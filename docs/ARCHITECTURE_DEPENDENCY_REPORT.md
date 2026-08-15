# Final Architecture Dependency Report

**Verified branch:** `refactor/system-boundaries-finalization-stable`  
**Verified code SHA:** `c09dc595211d492f8bb934292367dfd5b4215b4e`  
**Static acceptance:** `Stable final architecture acceptance` run `31909666042`, static job `95073061962` — passed  
**Generated report precursor:** `Final architecture static acceptance` run `31909432576` at `02a6bff15f8d034ac62e91a84219d7b5d42b07e7` — passed

This report records the final production assembly dependency graph for the architecture cutover. The generated precursor report was rechecked against the current code head after Composition restored its required Vegetation wiring. Test, CI, and Editor assemblies are intentionally outside the production Runtime-wiring rule because they must be able to exercise concrete implementations.

## Production assembly edges

| Assembly | VoxelEngine references |
|---|---|
| `VoxelEngine.Foundation` | none |
| `VoxelEngine.Storage.Api` | none |
| `VoxelEngine.Storage.Runtime` | `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api` |
| `VoxelEngine.Terrain.Api` | none |
| `VoxelEngine.Terrain.Runtime` | `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`, `VoxelEngine.Terrain.Api` |
| `VoxelEngine.Structures.Api` | `VoxelEngine.Storage.Api` |
| `VoxelEngine.Structures.Runtime` | `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `VoxelEngine.Terrain.Api` |
| `VoxelEngine.Edits.Api` | `VoxelEngine.Storage.Api` |
| `VoxelEngine.Edits.Runtime` | `VoxelEngine.Edits.Api`, `VoxelEngine.Foundation`, `VoxelEngine.Storage.Api` |
| `VoxelEngine.StructuralIntegrity.Api` | none |
| `VoxelEngine.StructuralIntegrity.Runtime` | `VoxelEngine.Storage.Api` |
| `VoxelEngine.Tiering.Api` | none |
| `VoxelEngine.Streaming.Api` | none |
| `VoxelEngine.Streaming.Runtime` | `VoxelEngine.Storage.Api`, `VoxelEngine.Streaming.Api`, `VoxelEngine.Tiering.Api` |
| `VoxelEngine.Collision.Api` | none |
| `VoxelEngine.Collision.Runtime` | `VoxelEngine.Collision.Api`, `VoxelEngine.Storage.Api` |
| `VoxelEngine.Vegetation.Api` | none |
| `VoxelEngine.Vegetation.Runtime` | `VoxelEngine.Vegetation.Api` |
| `VoxelEngine.Net.Api` | none |
| `VoxelEngine.Net.Runtime` | `VoxelEngine.Edits.Api`, `VoxelEngine.Foundation`, `VoxelEngine.Net.Api`, `VoxelEngine.Storage.Api`, `VoxelEngine.Streaming.Api` |
| `VoxelEngine.Rendering.Api` | none |
| `VoxelEngine.Rendering.Runtime` | `VoxelEngine.Storage.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api` |
| `VoxelEngine.Composition` | `VoxelEngine.Collision.Api`, `VoxelEngine.Edits.Api`, `VoxelEngine.Edits.Runtime`, `VoxelEngine.Foundation`, `VoxelEngine.Net.Runtime`, `VoxelEngine.Rendering.Api`, `VoxelEngine.Rendering.Runtime`, `VoxelEngine.Storage.Api`, `VoxelEngine.Storage.Runtime`, `VoxelEngine.Structures.Api`, `VoxelEngine.Structures.Runtime`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api`, `VoxelEngine.Vegetation.Runtime` |
| `VoxelEngine.Showcase` | `VoxelEngine.Collision.Api`, `VoxelEngine.Composition`, `VoxelEngine.Edits.Api`, `VoxelEngine.Rendering.Api`, `VoxelEngine.Storage.Api`, `VoxelEngine.Structures.Api`, `VoxelEngine.Terrain.Api`, `VoxelEngine.Tiering.Api`, `VoxelEngine.Vegetation.Api` |

## Enforced assertions

The current-head static acceptance passed all of these rules:

- Engine `*.Api` assemblies reference no engine `*.Runtime` assemblies.
- Engine `*.Runtime` assemblies reference no foreign subsystem `*.Runtime` assemblies.
- `VoxelEngine.Composition` is the sole production assembly allowed to wire concrete Runtime assemblies.
- `VoxelEngine.Showcase` is Runtime-free and consumes APIs plus Composition only.
- Production source and assembly metadata contain no `VoxelEngine.Core` references.
- `VoxelEngine.Streaming.Runtime` has no Net dependency.
- `VoxelEngine.Net.Runtime` consumes `VoxelEngine.Streaming.Api`, not `VoxelEngine.Streaming.Runtime`.
- `VoxelEngine.Rendering.Runtime` consumes only `Storage.Api`, `Tiering.Api`, and `Vegetation.Api` from engine subsystems.
- `MountingForce.WorldGen.Core` and `MountingForce.WorldGen.Architecture` have no `VoxelEngine.*` dependency.

## Tooling/test exception policy

`Assets/Tests/**`, `Assets/VoxelEngine/CI/**`, and Editor-only assemblies may reference concrete Runtime assemblies so they can test and inspect implementations. These are explicit tooling exceptions; they are not production dependency edges and do not weaken the production guard.

The permanent EditMode guard resolves named and `GUID:` engine assembly references so a production dependency cannot evade the rule by changing asmdef reference syntax.

## Legacy-token classification

The final repository inventory deliberately distinguishes active architecture from historical text. Retired migration/publisher workflows and archived Unity logs were removed, and `tools/check-compile.sh` now targets the final Api/Runtime assembly layout. Remaining `VoxelEngine.Core` literals are intentional: permanent guards that forbid its return plus architecture/spec history describing the migration. Production source and assembly metadata remain zero-match.
