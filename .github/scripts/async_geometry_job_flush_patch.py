from pathlib import Path

scheduler = Path('Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs')
tests = Path('Assets/Tests/EditMode/GeometryPipelineArchitectureTests.cs')

s = scheduler.read_text()
old = '''            _workerPrepareTiming.Add(workerPrepareMs);\n            CollectVisibility(camera, voxelSize, frame);\n'''
new = '''            _workerPrepareTiming.Add(workerPrepareMs);\n\n            // Geometry jobs are intentionally never completed while unfinished. Explicitly flush\n            // the once-per-world-frame batch after all solid/discovery/water scheduling so jobs\n            // cannot remain buffered waiting for an unrelated Unity subsystem to force dispatch.\n            // ScheduleBatchedJobs is non-blocking; readiness is still polled on later frames.\n            JobHandle.ScheduleBatchedJobs();\n\n            CollectVisibility(camera, voxelSize, frame);\n'''
if old not in s:
    raise SystemExit('scheduler flush anchor not found')
if 'JobHandle.ScheduleBatchedJobs();' in s:
    raise SystemExit('scheduler already contains explicit job flush')
s = s.replace(old, new, 1)
scheduler.write_text(s)

t = tests.read_text()
anchor = '''        [Test]\n        public void MultipleCamerasCannotMultiplyGeometryFrameBudgets()\n'''
test = '''        [Test]\n        public void GeometryJobsAreFlushedOnceWithoutWaiting()\n        {\n            string scheduler = ReadRenderingSource(\n                Path.Combine("SurfaceExtraction", "VoxelSurfaceScheduler.cs"));\n            const string flush = "JobHandle.ScheduleBatchedJobs();";\n            int first = scheduler.IndexOf(flush, StringComparison.Ordinal);\n            Assert.GreaterOrEqual(first, 0,\n                "Async geometry jobs need an explicit non-blocking dispatch boundary.");\n            Assert.AreEqual(first, scheduler.LastIndexOf(flush, StringComparison.Ordinal),\n                "Job batches should flush once per world frame, not once per worker/job.");\n            int water = scheduler.IndexOf("_water.Prepare(storage, camera, voxelSize, WaterBuildBudgetMs);",\n                                          StringComparison.Ordinal);\n            int visibility = scheduler.IndexOf("CollectVisibility(camera, voxelSize, frame);", first,\n                                               StringComparison.Ordinal);\n            Assert.Greater(first, water, "Flush must include water and solid jobs scheduled this frame.");\n            Assert.Greater(visibility, first, "Flush must happen before the frame returns to draw traversal.");\n        }\n\n'''
if anchor not in t:
    raise SystemExit('test insertion anchor not found')
if 'GeometryJobsAreFlushedOnceWithoutWaiting' in t:
    raise SystemExit('flush regression test already exists')
t = t.replace(anchor, test + anchor, 1)
tests.write_text(t)
