# Plan — VoxelShowcase Dragon Mesh Voxelization

## Observed requirement / acceptance
Add a reusable authoring-time triangle-mesh → canonical sparse voxel pipeline, prove it on a **downloaded third-party** detailed curved dragon, and present matched source/voxel views in built `VoxelShowcase`. Runtime gameplay truth must remain only discrete voxel storage; the source mesh may exist solely as authoring input/reference and comparison presentation. Required proof includes conservative triangle coverage, solid fill, transforms/mirroring, materials, thin features, deterministic sparse bake/replay, one-shot showcase placement, source licensing/provenance, dragon-specific anatomy regression, surface-distance/silhouette metrics, destruction/world-truth evidence, cost metrics, required built-player captures, and direct human review.

## Hypotheses / discriminator
A. Existing structure authoring/storage is already the canonical runtime path; missing work is reusable triangle rasterization/fill + sparse bake codec + showcase adapter.
B. An existing arbitrary mesh/SDF importer already owns this conversion.

Result: **A supported; B rejected.** No arbitrary production mesh voxelizer exists on this revision. Canonical replay is `IStructureAuthoringSession.Set` via `StructuresComposition.CreateAuthoringSession`; rendering/collision/editing already derive from that storage.

## Selected implementation
Keep deterministic mesh conversion additive under `Structures/Runtime/MeshImport`: transformed vertices → conservative triangle/AABB coverage → optional bounded exterior flood fill → deterministic material ownership → lexicographically ordered sparse cells → stable codec → replay through `IStructureAuthoringSession`. Preflight invalid indices/non-finite transforms, X/Z<=127, Y<=511, and dense working-set budget before allocation. No runtime voxelization or `MeshCollider` gameplay fallback.

Behavior-first regression contract landed at `9164857ad304dc95a6e182e8e982251d5a918567`; production core and one-shot selection state now exist on the branch and remain unvalidated until exact-SHA CI.

## Source dragon / licensing
The issue requires a downloaded third-party mesh; an original replacement is invalid. Verified candidates: Sketchfab Meleagor `Black Ink Dragon` (CC-BY, ~21.6k tris) but download is authentication-gated here; OpenGameArt artist_71 `A three headed lizard or dragon creature` is CC-BY 4.0 and explicitly ~24k tris with textures, public revised archive; McGuire’s Chinese Dragon is listed CC-BY 4.0 but ~412.7k tris/34 MB. Preferred source is artist_71 if its actual mesh bytes can be transferred into the repository; otherwise select another redistribution-safe third-party source without weakening detail/fidelity acceptance. Commit exact URL, author, license, format, triangle/vertex count, downloaded-file SHA-256, derived-source checksum if converted, and required attribution.

## Blast radius / cost
Changes stay in mesh-import, showcase integration, this issue evidence/tests/assets. Do not change terrain/building semantics, storage layout, collision/edit rules, global voxel scale, device budgets, another SceneIssue, or `.github/test-request.json` on the feature branch. Expensive conversion is offline/editor-side; runtime only decodes/replays the baked sparse artifact and optionally renders the labeled source comparison.

## Remaining gates
Commit transfer-safe licensed source + authoring adapter/bake, wire dedicated comparison/placement, dragon/anatomy/metric regressions, metrics/evidence hooks, and destruction truth proof. Merge latest master if needed, then issue exactly one final PlayMode targeted request on `ci-test/fixes/agent-1` for focused regressions + real-player `VoxelShowcase` capture. Inspect all required views directly. Only after green exact-SHA CI/player validation and human visual acceptance: open→pending metadata/bookkeeping, then pending→closed with `fixed`/`resolvedUtc`, merge latest master, and non-force promote exact feature head.