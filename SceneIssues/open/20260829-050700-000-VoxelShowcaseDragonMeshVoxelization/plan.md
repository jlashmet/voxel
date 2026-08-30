# Plan — VoxelShowcase Dragon Mesh Voxelization

## Observed requirement / acceptance
Add a reusable authoring-time triangle-mesh → canonical sparse voxel pipeline, prove it on a **downloaded third-party** detailed curved **winged** dragon, and present matched source/voxel views in built `VoxelShowcase`. Runtime gameplay truth remains discrete voxel storage; the source mesh is authoring/reference/comparison only. Required proof includes conservative triangle coverage, solid fill, transforms/mirroring, material regions, thin features, deterministic sparse bake/replay, one-shot showcase placement, provenance, dragon anatomy regression, similarity metrics, destruction/world-truth evidence, cost metrics, required built-player captures, and direct human review.

## Hypotheses / discriminator
A. Existing structure authoring/storage is already the canonical runtime path; missing work is reusable triangle rasterization/fill + sparse bake codec + showcase adapter.
B. An existing arbitrary mesh/SDF importer already owns this conversion.

Result: **A supported; B rejected.** Canonical replay is `IStructureAuthoringSession.Set` via structure composition; rendering/collision/editing derive from that storage.

## Selected implementation
Keep conversion additive under `Structures/Runtime/MeshImport`: transformed vertices → conservative triangle/AABB coverage → bounded exterior flood fill when valid → deterministic material ownership → ordered sparse cells → stable codec → `IStructureAuthoringSession` replay. Preflight invalid indices/non-finite transforms, X/Z<=127, Y<=511, and dense working-set budget before allocation. No runtime mesh voxelization or `MeshCollider` gameplay fallback.

Behavior-first importer/topology contracts and the Unity hierarchy adapter are on the branch; exact-SHA CI remains pending.

## Source dragon / licensing
The source must visibly include wings, head/snout, torso, limbs/feet/claws, tail and secondary silhouette detail. Sketchfab Meleagor `Black Ink Dragon` (CC-BY, ~21.6k tris) remains auth-gated. OpenGameArt artist_71 `A three headed lizard or dragon creature` is CC-BY 4.0/~24k tris but **rejected after visual inspection because it has no wings**, so it cannot satisfy mandatory wing comparison/close-up acceptance. Search continues for a redistribution-safe, transfer-capable winged source; do not weaken anatomy/detail requirements to fit tooling. Commit exact URL, author, license, format, triangle/vertex count, downloaded-file SHA-256, committed-source checksum, and required attribution/license text.

## Blast radius / cost
Changes stay in mesh-import, showcase integration, this issue evidence/tests/assets. Do not change terrain/building semantics, storage layout, collision/edit rules, global voxel scale, device budgets, another SceneIssue, or `.github/test-request.json` on the feature branch. Expensive conversion is offline/editor-side; runtime only decodes/replays the baked sparse artifact and renders the explicit source comparison.

## Remaining gates
Commit licensed winged source + bake, finish showcase comparison/placement, dragon/anatomy/metric regressions, evidence hooks, and destruction truth proof. Merge latest master before the single final PlayMode request on `ci-test/fixes/agent-1`; inspect every required built-player view. Only after green exact-SHA regression/player validation and human visual acceptance: complete pending metadata/bookkeeping, then close with `fixed`/`resolvedUtc`, merge latest master, and non-force promote the exact feature head.