from pathlib import Path


def replace_once(path: Path, old: str, new: str) -> None:
    text = path.read_text()
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{path}: expected exactly one match, found {count}: {old[:100]!r}")
    path.write_text(text.replace(old, new, 1))


cache = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
metrics = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs")
lod = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")

replace_once(cache,
"""        public ulong FeaturePreservingFallbackPublishCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""",
"""        public ulong FeaturePreservingFallbackPublishCount { get; private set; }\n        // Last visibility pass diagnostics. These counters are reset by BeginVisibilityCollection\n        // and never participate in scheduling; they distinguish ring ownership, frustum routing,\n        // current-ready and current-empty states when a production LOD disappears.\n        public int LastVisibilityKnownCount { get; private set; }\n        public int LastVisibilityInBandCount { get; private set; }\n        public int LastVisibilityFrustumCount { get; private set; }\n        public int LastVisibilityReadyCount { get; private set; }\n        public int LastVisibilityEmptyCount { get; private set; }\n        public ulong MaterialPaletteInvalidationCount { get; private set; }\n""")

replace_once(cache,
"""        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n        }\n""",
"""        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n            LastVisibilityKnownCount = 0;\n            LastVisibilityInBandCount = 0;\n            LastVisibilityFrustumCount = 0;\n            LastVisibilityReadyCount = 0;\n            LastVisibilityEmptyCount = 0;\n        }\n""")

replace_once(cache,
"""            if (!_known.Contains(coordinate)) return;\n\n            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition))\n""",
"""            if (!_known.Contains(coordinate)) return;\n            LastVisibilityKnownCount++;\n\n            Bounds bounds = ChunkWorldBounds(coordinate, voxelSize);\n            if (!WithinRingBand(bounds, cameraPosition))\n""")

replace_once(cache,
"""                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);\n                return;\n            }\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n""",
"""                if (_dirty.Contains(coordinate)) ParkDirty(coordinate);\n                return;\n            }\n            LastVisibilityInBandCount++;\n\n            bool hasDesired = _desiredVersions.TryGetValue(coordinate, out ulong desired);\n""")

replace_once(cache,
"""            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n\n            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in\n""",
"""            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds)) return;\n            LastVisibilityFrustumCount++;\n            if (currentReady) LastVisibilityReadyCount++;\n            if (currentEmpty) LastVisibilityEmptyCount++;\n\n            // Background prefetch above remains intentionally 360 degrees. Once a chunk is in\n""")

replace_once(metrics,
"""        public readonly ulong Step4FeatureFallbackPublished;\n        public readonly ulong MaterialPaletteInvalidations;\n""",
"""        public readonly ulong Step4FeatureFallbackPublished;\n        public readonly int Step4VisibilityKnown;\n        public readonly int Step4VisibilityInBand;\n        public readonly int Step4VisibilityFrustum;\n        public readonly int Step4VisibilityReady;\n        public readonly int Step4VisibilityEmpty;\n        public readonly ulong MaterialPaletteInvalidations;\n""")

replace_once(metrics,
"""            Step4FeatureFallbackPublished = isStep4\n                ? solids.FeaturePreservingFallbackPublishCount : 0UL;\n            MaterialPaletteInvalidations = solids.MaterialPaletteInvalidationCount;\n""",
"""            Step4FeatureFallbackPublished = isStep4\n                ? solids.FeaturePreservingFallbackPublishCount : 0UL;\n            Step4VisibilityKnown = isStep4 ? solids.LastVisibilityKnownCount : 0;\n            Step4VisibilityInBand = isStep4 ? solids.LastVisibilityInBandCount : 0;\n            Step4VisibilityFrustum = isStep4 ? solids.LastVisibilityFrustumCount : 0;\n            Step4VisibilityReady = isStep4 ? solids.LastVisibilityReadyCount : 0;\n            Step4VisibilityEmpty = isStep4 ? solids.LastVisibilityEmptyCount : 0;\n            MaterialPaletteInvalidations = solids.MaterialPaletteInvalidationCount;\n""")

replace_once(metrics,
"""            ulong step4FallbackScheduled = 0, step4FallbackCompleted = 0;\n            ulong step4FallbackNonEmpty = 0, step4FallbackPublished = 0;\n            ulong materialInvalidations = 0, surfaceInvalidations = 0;\n""",
"""            ulong step4FallbackScheduled = 0, step4FallbackCompleted = 0;\n            ulong step4FallbackNonEmpty = 0, step4FallbackPublished = 0;\n            int step4VisibilityKnown = 0, step4VisibilityInBand = 0;\n            int step4VisibilityFrustum = 0, step4VisibilityReady = 0, step4VisibilityEmpty = 0;\n            ulong materialInvalidations = 0, surfaceInvalidations = 0;\n""")

replace_once(metrics,
"""                    step4FallbackNonEmpty += worker.FeaturePreservingFallbackNonEmptyCount;\n                    step4FallbackPublished += worker.FeaturePreservingFallbackPublishCount;\n                }\n""",
"""                    step4FallbackNonEmpty += worker.FeaturePreservingFallbackNonEmptyCount;\n                    step4FallbackPublished += worker.FeaturePreservingFallbackPublishCount;\n                    step4VisibilityKnown += worker.LastVisibilityKnownCount;\n                    step4VisibilityInBand += worker.LastVisibilityInBandCount;\n                    step4VisibilityFrustum += worker.LastVisibilityFrustumCount;\n                    step4VisibilityReady += worker.LastVisibilityReadyCount;\n                    step4VisibilityEmpty += worker.LastVisibilityEmptyCount;\n                }\n""")

replace_once(metrics,
"""            Step4FeatureFallbackNonEmpty = step4FallbackNonEmpty;\n            Step4FeatureFallbackPublished = step4FallbackPublished;\n            MaterialPaletteInvalidations = materialInvalidations;\n""",
"""            Step4FeatureFallbackNonEmpty = step4FallbackNonEmpty;\n            Step4FeatureFallbackPublished = step4FallbackPublished;\n            Step4VisibilityKnown = step4VisibilityKnown;\n            Step4VisibilityInBand = step4VisibilityInBand;\n            Step4VisibilityFrustum = step4VisibilityFrustum;\n            Step4VisibilityReady = step4VisibilityReady;\n            Step4VisibilityEmpty = step4VisibilityEmpty;\n            MaterialPaletteInvalidations = materialInvalidations;\n""")

replace_once(lod,
"""                      + $\"meta:{metrics.Step4ExactMetadataScheduled}/{metrics.Step4ExactMetadataCompleted}/\"\n                      + $\"revReject:{metrics.Step4ExactMetadataRevisionRejects}/\"\n                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n""",
"""                      + $\"meta:{metrics.Step4ExactMetadataScheduled}/{metrics.Step4ExactMetadataCompleted}/\"\n                      + $\"revReject:{metrics.Step4ExactMetadataRevisionRejects}/\"\n                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n                      + $\"visibility:known:{metrics.Step4VisibilityKnown}/inBand:{metrics.Step4VisibilityInBand}/\"\n                      + $\"frustum:{metrics.Step4VisibilityFrustum}/ready:{metrics.Step4VisibilityReady}/\"\n                      + $\"empty:{metrics.Step4VisibilityEmpty} \"\n                      + $\"fallback:{metrics.Step4FeatureFallbackScheduled}/{metrics.Step4FeatureFallbackCompleted}/\"\n                      + $\"nonEmpty:{metrics.Step4FeatureFallbackNonEmpty}/published:{metrics.Step4FeatureFallbackPublished} \"\n""")
