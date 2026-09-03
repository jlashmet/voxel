# T026 validation note

T026 now wires the canonical Showcase feature-presentation source through the shared far-feature selection/renderer path. The runtime queries the scene camera, does not call region generation/residency, owns renderer cache lifetime with the Showcase component, and reports semantic source/visible counts through existing far diagnostics.

Focused regression: `VoxelEngine.Tests.EditMode.ShowcaseFarFeatureRuntimeTests` proves runtime update queries only the injected presentation source and keeps zero persistent per-instance objects for an empty source.

Exact-SHA CI remains expected to encounter the separately recorded Rendering/GPU baseline blocker after the focused/preceding module phases. Do not treat that unrelated failure as T026 validation success, and do not modify the GPU restoration assignment from agent-7.
