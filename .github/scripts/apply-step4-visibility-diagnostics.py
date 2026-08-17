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
plan = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")

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

replace_once(plan,
"""- [ ] Fix the remaining coarse-coverage defect: exact-head PR run 32022085431 still has 50 visible coarse silhouette holes and step 4 again ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0` after 20 s; step 4 uses the exact COW snapshot path (`LevelForStride(4) == -1`), so the disappearance is downstream of exact snapshot admission rather than lossy mip sampling.\n""",
"""- [ ] Fix the remaining coarse-coverage defect: exact-head PR run 32028393584 still has 48 visible coarse silhouette holes and step 4 again ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0` after 20 s. The validated false-empty fallback does not materially change production, so the next evidence must distinguish ring visibility ownership/current-empty state from another geometry hypothesis before further rendering changes.\n""")

replace_once(plan,
"""- [ ] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage.\n""",
"""- [x] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage. PR run 32028393584 (`63577998`) passes the affected EditMode gate, including `OwnedThinFeatureMissedByFourVoxelLatticeMustUseFeaturePreservingFallback`, with no HLOD overflow; production remains materially unchanged at 48 silhouette holes and step 4 `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`, proving the fallback fixes a real mechanism but is not the dominant coarse-coverage defect.\n- [x] Add allocation-free step-4 visibility/fallback lifecycle diagnostics: last-pass known/in-band/frustum/ready/current-empty counts plus fallback scheduled/completed/non-empty/published counters, surfaced through `VoxelSurfaceMetrics` and the production LOD failure message.\n- [ ] Use the new step-4 visibility diagnostics in Unity to identify the first stage where the 240 m castle leaves production ownership before making another geometry change.\n""")

replace_once(plan,
"""- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n""",
"""- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n- [x] Record the step-4-fallback measurement from PR run 32028393584: at 10.01 s the showcase has 123/5,672 resident, 4,221 dirty, 13 running, 103 visible and 1,539 missing visible chunks; prepare p95 2.58 ms, select p95 0.02 ms, queue p95 4,335.45 ms and build p95 1,484.54 ms. Coarse lookdev still reports 48 holes and the dedicated LOD test still ends step 4 at `110/7/0/0/0/0`, so the fallback does not close the production coverage gap.\n""")

replace_once(plan,
"""- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n""",
"""- `2bca9841` — visible-demand selection made constant-time in the normal case; no-stutter and LOD-fidelity batchmode render targets repaired without changing acceptance thresholds.\n- `e53f14ce` — focused step-4 false-empty regression compiled and proved the missing production guard.\n- `63577998` — clean validation head for the step-4 feature-preserving fallback; EditMode passes but production coarse coverage remains open.\n""")

text = plan.read_text()
old_tail = """PR #88 run 32022085431 (`f0e0b689`) validates the constant-time selector and both repaired validation harnesses on the clean exact head, but production PlayMode remains red. Selection p95 falls from 0.52 ms to 0.02 ms without changing the 0.50 ms budget, while the 10-second showcase still has 1,543 missing visible chunks and coarse lookdev reports 50 holes. Step 4 again converges to `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`, confirming the remaining coarse disappearance is not queue backlog. Static tracing shows exact classification preserves owned solids, while step-4 faceted/continuous generation samples the four-voxel lattice and zero-geometry completion is recorded as authoritative empty; the next gate is a focused regression for that false-empty path before any production geometry fallback is enabled. The no-stutter and fidelity fixtures now reach production rendering and fail on convergence/coverage rather than their former batchmode setup blockers.\n"""
new_tail = """PR #88 run 32028393584 (`63577998`) validates the focused step-4 false-empty policy/implementation on the clean exact head, but production PlayMode remains red. The fallback does not materially change the renderer: coarse lookdev reports 48 holes, the 10-second showcase still has 1,539 missing visible chunks, and the dedicated step-4 band still ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`. Because both dirty and missing-visible demand are zero when the castle disappears, the next gate is visibility/ownership lifecycle instrumentation—not another speculative geometry fallback. The no-stutter and fidelity fixtures continue to reach production rendering and fail on convergence/coverage rather than harness setup.\n"""
if old_tail not in text:
    raise SystemExit("plan: current summary paragraph did not match")
plan.write_text(text.replace(old_tail, new_tail, 1))
