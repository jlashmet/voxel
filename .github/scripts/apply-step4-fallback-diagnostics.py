from pathlib import Path


def replace_once(text: str, old: str, new: str, label: str) -> str:
    count = text.count(old)
    if count != 1:
        raise SystemExit(f"{label}: expected exactly one match, found {count}")
    return text.replace(old, new, 1)


def replace_exact_count(text: str, old: str, new: str, count: int, label: str) -> str:
    actual = text.count(old)
    if actual != count:
        raise SystemExit(f"{label}: expected {count} matches, found {actual}")
    return text.replace(old, new)


cache_path = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
cache = cache_path.read_text()
cache = replace_once(
    cache,
    "        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);\n",
    "        public int DirtyCount => _dirty.Count + (_build.Active ? 1 : 0);\n"
    "        public int KnownEmptyCount => _emptyVersions.Count;\n",
    "cache known-empty diagnostic")
cache = replace_once(
    cache,
    "        public int MissingVisibleCount { get; private set; }\n",
    "        public int MissingVisibleCount { get; private set; }\n"
    "        public int VisibleEmptyCount { get; private set; }\n",
    "cache visible-empty diagnostic")
cache = replace_once(
    cache,
    "        public ulong ExactMetadataPinRejectCount { get; private set; }\n",
    "        public ulong ExactMetadataPinRejectCount { get; private set; }\n"
    "        public ulong FeaturePreservingFallbackScheduleCount { get; private set; }\n"
    "        public ulong FeaturePreservingFallbackCompleteCount { get; private set; }\n"
    "        public ulong FeaturePreservingFallbackNonEmptyCount { get; private set; }\n",
    "cache fallback counters")
cache = replace_once(
    cache,
    "        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n        }\n",
    "        public void BeginVisibilityCollection()\n        {\n            _visible.Clear();\n            MissingVisibleCount = 0;\n            VisibleEmptyCount = 0;\n        }\n",
    "reset visible-empty diagnostic")
cache = replace_once(
    cache,
    "            if (currentEmpty) return;\n\n            MissingVisibleCount++;\n",
    "            if (currentEmpty)\n            {\n                VisibleEmptyCount++;\n                return;\n            }\n\n            MissingVisibleCount++;\n",
    "count visible authoritative empties")
cache = replace_exact_count(
    cache,
    "                        ScheduleFeaturePreservingHlod(voxelSize);\n"
    "                        _build.UsedFeaturePreservingFallback = true;\n",
    "                        _build.UsedFeaturePreservingFallback = true;\n"
    "                        FeaturePreservingFallbackScheduleCount++;\n"
    "                        ScheduleFeaturePreservingHlod(voxelSize);\n",
    2,
    "count fallback schedules")
cache = replace_once(
    cache,
    "                        _hlodJobScheduled = false;\n"
    "                        _build.HasOwnedSolid = _indices.Length > 0;\n",
    "                        _hlodJobScheduled = false;\n"
    "                        _build.HasOwnedSolid = _indices.Length > 0;\n"
    "                        if (_build.UsedFeaturePreservingFallback)\n"
    "                        {\n"
    "                            FeaturePreservingFallbackCompleteCount++;\n"
    "                            if (_build.HasOwnedSolid) FeaturePreservingFallbackNonEmptyCount++;\n"
    "                        }\n",
    "count fallback completions")
cache_path.write_text(cache)

scheduler_path = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs")
scheduler = scheduler_path.read_text()
scheduler = replace_once(
    scheduler,
    "        public readonly ulong Step4ExactMetadataPinRejects;\n",
    "        public readonly ulong Step4ExactMetadataPinRejects;\n"
    "        public readonly int Step4KnownEmptyChunks;\n"
    "        public readonly int Step4VisibleEmptyChunks;\n"
    "        public readonly ulong Step4FeatureFallbackScheduled;\n"
    "        public readonly ulong Step4FeatureFallbackCompleted;\n"
    "        public readonly ulong Step4FeatureFallbackNonEmpty;\n",
    "metrics step4 diagnostic fields")
scheduler = replace_once(
    scheduler,
    "            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;\n",
    "            Step4ExactMetadataPinRejects = isStep4 ? solids.ExactMetadataPinRejectCount : 0UL;\n"
    "            Step4KnownEmptyChunks = isStep4 ? solids.KnownEmptyCount : 0;\n"
    "            Step4VisibleEmptyChunks = isStep4 ? solids.VisibleEmptyCount : 0;\n"
    "            Step4FeatureFallbackScheduled = isStep4 ? solids.FeaturePreservingFallbackScheduleCount : 0UL;\n"
    "            Step4FeatureFallbackCompleted = isStep4 ? solids.FeaturePreservingFallbackCompleteCount : 0UL;\n"
    "            Step4FeatureFallbackNonEmpty = isStep4 ? solids.FeaturePreservingFallbackNonEmptyCount : 0UL;\n",
    "single-cache step4 diagnostics")
scheduler = replace_once(
    scheduler,
    "            int step4Known = 0, step4Resident = 0, step4Dirty = 0, step4Missing = 0, step4Running = 0;\n",
    "            int step4Known = 0, step4Resident = 0, step4Dirty = 0, step4Missing = 0, step4Running = 0;\n"
    "            int step4KnownEmpty = 0, step4VisibleEmpty = 0;\n",
    "aggregate step4 empty diagnostics")
scheduler = replace_once(
    scheduler,
    "            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;\n",
    "            ulong step4MetadataRevisionRejects = 0, step4MetadataPinRejects = 0;\n"
    "            ulong step4FallbackScheduled = 0, step4FallbackCompleted = 0, step4FallbackNonEmpty = 0;\n",
    "aggregate step4 fallback diagnostics")
scheduler = replace_once(
    scheduler,
    "                    step4MetadataRevisionRejects += worker.ExactMetadataRevisionRejectCount;\n"
    "                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;\n",
    "                    step4MetadataRevisionRejects += worker.ExactMetadataRevisionRejectCount;\n"
    "                    step4MetadataPinRejects += worker.ExactMetadataPinRejectCount;\n"
    "                    step4KnownEmpty += worker.KnownEmptyCount;\n"
    "                    step4VisibleEmpty += worker.VisibleEmptyCount;\n"
    "                    step4FallbackScheduled += worker.FeaturePreservingFallbackScheduleCount;\n"
    "                    step4FallbackCompleted += worker.FeaturePreservingFallbackCompleteCount;\n"
    "                    step4FallbackNonEmpty += worker.FeaturePreservingFallbackNonEmptyCount;\n",
    "aggregate worker diagnostics")
scheduler = replace_once(
    scheduler,
    "            Step4ExactMetadataPinRejects = step4MetadataPinRejects;\n",
    "            Step4ExactMetadataPinRejects = step4MetadataPinRejects;\n"
    "            Step4KnownEmptyChunks = step4KnownEmpty;\n"
    "            Step4VisibleEmptyChunks = step4VisibleEmpty;\n"
    "            Step4FeatureFallbackScheduled = step4FallbackScheduled;\n"
    "            Step4FeatureFallbackCompleted = step4FallbackCompleted;\n"
    "            Step4FeatureFallbackNonEmpty = step4FallbackNonEmpty;\n",
    "publish aggregate diagnostics")
scheduler_path.write_text(scheduler)

lod_path = Path("Assets/Tests/PlayMode/LodRenderingTests.cs")
lod = lod_path.read_text()
lod = replace_once(
    lod,
    "                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n",
    "                      + $\"pinReject:{metrics.Step4ExactMetadataPinRejects} \"\n"
    "                      + $\"empty:{metrics.Step4KnownEmptyChunks}/visibleEmpty:{metrics.Step4VisibleEmptyChunks} \"\n"
    "                      + $\"fallback:{metrics.Step4FeatureFallbackScheduled}/{metrics.Step4FeatureFallbackCompleted}/\"\n"
    "                      + $\"{metrics.Step4FeatureFallbackNonEmpty} \"\n",
    "LOD failure diagnostic")
lod_path.write_text(lod)

plan_path = Path(".claude/plans/voxel-showcase-rendering-repair-v2.md")
plan = plan_path.read_text()
plan = replace_once(
    plan,
    "- [ ] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage.\n",
    "- [x] Validate the step-4 false-empty fallback in EditMode and remeasure production step-4/coarse visible coverage. PR run 32028393584 (`63577998`) passes EditMode and bake, but production is materially unchanged: silhouette still has 48 not-ready coarse holes; step 4 still ends `known=110/resident=7/dirty=0/missing=0/jobs=0/visible=0`; and the 10-second showcase is 123/5,672 resident with 4,221 dirty, 103 visible, 1,539 missing visible, queue p95 4,335.45 ms and build p95 1,484.54 ms. The fallback policy is valid in isolation but did not affect the production castle capture.\n"
    "- [x] Add allocation-free step-4 production diagnostics for known-empty/visible-empty chunks plus fallback scheduled/completed/non-empty counts, and include them in the existing LOD failure output so the next run distinguishes trigger failure from fallback-output failure.\n"
    "- [ ] Use the step-4 activation diagnostics to identify why production castle chunks never become visible before broadening the fallback or changing any LOD/fidelity behavior.\n",
    "plan run216 validation and diagnostic tasks")
plan = replace_once(
    plan,
    "- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n",
    "- [x] Record the constant-time-selector measurement from PR run 32022085431: at 10.00 s the renderer has 141/5,672 resident, 4,264 dirty, 16 running jobs, 104 visible and 1,543 missing visible chunks; prepare p95 2.58 ms, worker/select p95 0.51/0.02 ms, queue p95 4,073.81 ms and build p95 1,036.84 ms. Selection no longer consumes the frame budget, but worker/build throughput and false-empty coarse publication still prevent convergence.\n"
    "- [x] Record the step-4 fallback remeasurement from PR run 32028393584: at 10.01 s the renderer has 123/5,672 resident, 4,221 dirty, 13 running jobs, 103 visible and 1,539 missing visible chunks; select p95 remains 0.02 ms, queue p95 4,335.45 ms and build p95 1,484.54 ms. LOD step 4 remains `110 known / 7 resident / 0 dirty / 0 missing / 0 jobs / 0 visible`, so the isolated false-empty fallback has not yet changed production output.\n",
    "plan run216 measurement")
plan_path.write_text(plan)

workflow = Path(".github/workflows/step4-fallback-diagnostics-once.yml")
script = Path(".github/scripts/apply-step4-fallback-diagnostics.py")
workflow.unlink(missing_ok=True)
script.unlink(missing_ok=True)
