# Experiment 041 — operation bounds rendered as fallback geometry

## Exact-run discriminator
Exact request `6df028095878dc272f0718f56a5435d843782d8f`, run `34010802098`, source `3c46a02f9c8a5471ba0e30c15281df3128ee35ed` completed with failure before player execution. Both module validation and the SceneIssue player build aborted on compile errors in `FarFeatureEmptyProjectionTests`: its test asmdef referenced `Unity.Mathematics` while retaining `noEngineReferences: true`, so Unity 6 could not resolve the forwarded `float3`/`int3` engine module. No visual discriminator was produced by this run.

## Demonstrated production defect
`FarFeaturePresentationAdapter.GeometryFor` projects only positive `Fill` / `FillIfEmpty` primitives. Road terrain corridors, carve operations, paint operations and surface-detail operations therefore legitimately produce no far-feature geometry. `Query` nevertheless published those bakes with `Geometry == null`. `ProceduralFarFeatureRenderer` treats missing geometry as its generic fallback box and scales that box to the bake bounds. The material selection for an operation-only bake falls back to row 0, whose canonical Empty presentation color is magenta. A bounded road/corridor operation can therefore appear as a giant magenta/gray slab even though it contains no positive geometry.

## Narrow correction
Composition now drops bakes that project no positive geometry and forgets any selection history for them. Geometry, material and style all select from the same positive `Fill` / `FillIfEmpty` predicate. The renderer fallback remains unchanged for callers that intentionally provide null geometry; no renderer-wide suppression or scene-specific exception was added.

The focused regression covers five non-geometric modes, two positive controls, and four mixed style/material cases. Its asmdef now follows the repository's existing WorldBuilder test pattern with `noEngineReferences: false` so Unity Mathematics types compile.

## Required proof
This source-level root cause is strong enough to justify the correction but does not substitute for production visual proof. The next exact-SHA run must compile and pass the focused Composition regression, preserve module/player evidence, execute the Mountain Dragon production replay, and show the ordinary full-rendering route without substantial magenta/slab artifacts. The temporary renderer-isolation observer may remain only until the corrected production capture proves whether any additional draw owner remains.
