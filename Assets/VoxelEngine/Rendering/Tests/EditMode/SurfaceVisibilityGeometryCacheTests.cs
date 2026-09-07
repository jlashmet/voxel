using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceVisibilityGeometryCacheTests
    {
        [TestCase(false, false)] [TestCase(false, true)]
        [TestCase(true, false)] [TestCase(true, true)]
        public void EvictionAcknowledgmentPreservesNewPublicationAndActiveReplacement(bool active, bool newer)
        {
            using var worker = new GpuSolidChunkCache();
            var coordinate = new int3(-2, 1, 3);
            var entry = new GpuSolidChunkCache.Entry(coordinate, worker.VoxelsPerAxis, worker.SourceStep);
            entry.PublishGpuPaged(4, newer ? 20UL : 10UL);
            var entries = Field<Dictionary<int3, GpuSolidChunkCache.Entry>>(worker, "_entries");
            entries.Add(coordinate, entry);
            if (active)
            {
                var field = typeof(GpuSolidChunkCache).GetField("_build", BindingFlags.Instance | BindingFlags.NonPublic);
                object build = field.GetValue(worker);
                build.GetType().GetField("Active").SetValue(build, true);
                build.GetType().GetField("Coordinate").SetValue(build, coordinate);
                field.SetValue(worker, build);
            }
            bool accepted = worker.AcknowledgeGpuEviction(coordinate * worker.VoxelsPerAxis,
                worker.SourceStep, 4, 10);
            Assert.That(accepted, Is.EqualTo(!newer));
            Assert.That(entries.ContainsKey(coordinate), Is.EqualTo(active || newer));
            if (active || newer)
            {
                Assert.That(entry.GpuHandle, Is.EqualTo(4));
                Assert.That(entry.Ready, Is.EqualTo(newer));
                Assert.That(entry.PublishedGpuGeneration, Is.EqualTo(newer ? 20UL : 10UL));
            }
            Assert.That(worker.DirtyCount, Is.EqualTo((newer ? 0 : 1) + (active ? 1 : 0)));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void ChangedQueryCannotReusePreviousClassification(int change)
        {
            var cache = new SurfaceVisibilityGeometryCache();
            var planes = GeometryUtility.CalculateFrustumPlanes(Matrix4x4.identity);
            var position = Vector3.zero;
            float scale = 0.1f, inner = 0, outer = 96;
            bool suspended = false;
            void Prepare() => cache.Prepare(planes, position, scale, inner, outer, suspended);
            Prepare(); Prepare(); cache.Store(new int3(-1, 0, 2), 2);
            Assert.True(cache.TryGet(new int3(-1, 0, 2), out byte classification));
            Assert.That(classification, Is.EqualTo(2));
            switch (change)
            {
                case 0: position.x = 0.000001f; break; // Unity's approximate equality is insufficient.
                case 1: planes[0] = new Plane(Vector3.up, 1); break;
                case 2: planes[5] = new Plane(planes[5].normal, planes[5].distance + 0.01f); break;
                case 3: scale = 0.2f; break;
                case 4: inner = 1; break;
                case 5: outer = 100; break;
                case 6: suspended = true; break;
            }
            Prepare();
            Assert.False(cache.TryGet(new int3(-1, 0, 2), out _));
            cache.Store(int3.zero, 1);
            Assert.False(cache.TryGet(int3.zero, out _), "Moving queries bypass insertion.");
            Prepare(); cache.Store(int3.zero, 1);
            Assert.True(cache.TryGet(int3.zero, out classification));
            Assert.That(classification, Is.EqualTo(1));
        }

        [Test]
        public void StationaryGeometryDoesNotCachePublicationEditsOrNewCoordinates()
        {
            using var worker = new GpuSolidChunkCache();
            var planes = new[] {
                new Plane(Vector3.right, 100), new Plane(Vector3.left, 100),
                new Plane(Vector3.up, 100), new Plane(Vector3.down, 100),
                new Plane(Vector3.forward, 100), new Plane(Vector3.back, 100) };
            var known = Field<HashSet<int3>>(worker, "_known");
            var desired = Field<Dictionary<int3, ulong>>(worker, "_desiredVersions");
            var empty = Field<Dictionary<int3, ulong>>(worker, "_emptyVersions");
            int frame = 0;
            void Collect(int3 coordinate)
            {
                worker.BeginVisibilityCollection(planes, Vector3.zero, 0.1f);
                worker.CollectVisibleCoordinate(coordinate, planes, Vector3.zero, 0.1f, ++frame);
            }
            known.Add(int3.zero);
            Collect(int3.zero); Collect(int3.zero); Collect(int3.zero);
            Assert.That(worker.MissingVisibleCount, Is.EqualTo(1));
            empty[int3.zero] = desired[int3.zero]; // Observe a newly published empty generation.
            Collect(int3.zero);
            Assert.That(worker.MissingVisibleCount, Is.Zero);
            Assert.That(worker.LastVisibilityEmptyCount, Is.EqualTo(1));
            desired[int3.zero]++;
            Collect(int3.zero);
            Assert.That(worker.MissingVisibleCount, Is.EqualTo(1));
            Assert.That(worker.LastVisibilityEmptyCount, Is.Zero);
            var streamed = new int3(1, 0, 0);
            known.Add(streamed);
            Collect(streamed);
            Assert.That(worker.MissingVisibleCount, Is.EqualTo(1));
            Assert.True(desired.ContainsKey(streamed));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)]
        [TestCase(4)] [TestCase(5)] [TestCase(6)]
        public void DirectResultPreservesDrawableAndCoverageSemantics(int state)
        {
            using var worker = new GpuSolidChunkCache();
            var planes = new[] {
                new Plane(Vector3.right, 100), new Plane(Vector3.left, 100),
                new Plane(Vector3.up, 100), new Plane(Vector3.down, 100),
                new Plane(Vector3.forward, 100), new Plane(Vector3.back, 100) };
            var entries = Field<Dictionary<int3, GpuSolidChunkCache.Entry>>(worker, "_entries");
            GpuSolidChunkCache.Entry entry = null;
            try
            {
                if (state != 0) Field<HashSet<int3>>(worker, "_known").Add(int3.zero);
                if (state == 2) Field<Dictionary<int3, ulong>>(worker, "_emptyVersions")[int3.zero] = 1;
                if (state == 3 || state == 4)
                {
                    // Presentation metadata fixture, no synthetic geometry/rendering.
                    entry = new GpuSolidChunkCache.Entry(int3.zero, 32, 1);
                    entry.PublishGpuPaged(4); entry.SourceVersion = 1;
                    entries.Add(int3.zero, entry);
                    if (state == 4) Field<Dictionary<int3, ulong>>(worker, "_desiredVersions")[int3.zero] = 2;
                }
                if (state == 5) worker.RingSuspended = true;
                if (state == 6) planes[0] = new Plane(Vector3.right, -100);
                worker.BeginVisibilityCollection();
                var result = worker.CollectVisibleCoordinate(int3.zero, planes, Vector3.zero, 0.1f, 42);
                Assert.That(result.Drawable, Is.EqualTo(state == 3 || state == 4));
                Assert.That(result.CurrentViewComplete, Is.EqualTo(state == 2 || state == 3 || state == 6));
                Assert.That(result.Drawable, Is.EqualTo(worker.Visible.Count == 1));
                if (entry != null) Assert.That(entry.LastUsedFrame, Is.EqualTo(42));
                if (state == 1) Assert.That(worker.MissingVisibleCount, Is.EqualTo(1));
                if (state == 6) Assert.That(worker.DirtyCount, Is.GreaterThan(0),
                    "Off-frustum completeness must still activate surrounding build demand.");
            }
            finally
            {
                entries.Clear();
                entry?.Dispose();
            }
        }

        [Test]
        public void GpuDemandFeedbackControlsUrgencyAndResidentAgeWithoutCpuBandTests()
        {
            using var worker = new GpuSolidChunkCache();
            worker.MaxViewDistanceMetres = 10;
            var planes = new[] { new Plane(Vector3.right, 1000), new Plane(Vector3.left, 1000),
                new Plane(Vector3.up, 1000), new Plane(Vector3.down, 1000),
                new Plane(Vector3.forward, 1000), new Plane(Vector3.back, 1000) };
            int3 pending = new int3(10, 0, 0);
            var known = Field<HashSet<int3>>(worker, "_known"); known.Add(int3.zero); known.Add(pending);
            var desired = Field<Dictionary<int3, ulong>>(worker, "_desiredVersions"); desired[pending] = 1;
            var entries = Field<Dictionary<int3, GpuSolidChunkCache.Entry>>(worker, "_entries");
            var entry = new GpuSolidChunkCache.Entry(int3.zero, 32, 1);
            entry.PublishGpuPaged(4); entry.SourceVersion = 1; entries.Add(int3.zero, entry);
            try
            {
                worker.BeginVisibilityCollection(planes, Vector3.zero, 0.1f, true);
                Assert.That(worker.CollectVisibleCoordinate(int3.zero, planes, Vector3.zero, 0.1f, 1).GpuCandidate, Is.True);
                Assert.That(worker.CollectVisibleCoordinate(pending, planes, Vector3.zero, 0.1f, 1).GpuCandidate, Is.True,
                    "Out-of-band metadata remains available for GPU camera reclassification.");
                Assert.That(worker.DirtyCount, Is.Zero);
                Assert.That(entry.LastUsedFrame, Is.Zero, "Candidate transport must not make a CPU camera decision.");
                worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 3, 1); worker.ApplyGpuDemand(pending, 0, 1);
                worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 0, 2); worker.ApplyGpuDemand(pending, 3, 2);
                Assert.That(worker.MissingVisibleCount, Is.EqualTo(1));
                Assert.That(worker.DirtyCount, Is.EqualTo(1));
                Assert.That(entry.LastUsedFrame, Is.EqualTo(1), "Out-of-band cache entries must not become artificially hot.");
                worker.BeginVisibilityCollection(planes, Vector3.zero, 0.1f, true);
                worker.CollectVisibleCoordinate(int3.zero, planes, Vector3.zero, 0.1f, 2);
                worker.CollectVisibleCoordinate(pending, planes, Vector3.zero, 0.1f, 2);
                Assert.That(worker.MissingVisibleCount, Is.EqualTo(1), "Metadata refresh must retain the latest GPU observation.");
                worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 1, 3); worker.ApplyGpuDemand(pending, 0, 3);
                Assert.That(worker.MissingVisibleCount, Is.Zero);
                Assert.That(worker.DirtyCount, Is.Zero);
                Assert.That(desired[pending], Is.EqualTo(1));
                Assert.That(entry.LastUsedFrame, Is.EqualTo(3));
                desired[int3.zero] = 2;
                worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 3, 5);
                Assert.That(entry.LastUsedFrame, Is.EqualTo(5));
                Assert.That(worker.DirtyCount, Is.EqualTo(1), "Delayed classification must queue the current edit generation.");
                Assert.That(worker.MissingVisibleCount, Is.Zero, "A stale drawable remains available during an edit.");
                worker.RingSuspended = true;
                worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 3, 6);
                Assert.That(entry.LastUsedFrame, Is.EqualTo(5), "Suspension must reject older in-band demand.");
                Assert.That(worker.DirtyCount, Is.Zero);
                worker.RingSuspended = false;
                known.Remove(pending);
                worker.ApplyGpuDemand(pending, 3, 7);
                Assert.That(worker.DirtyCount, Is.Zero, "Delayed feedback must not resurrect removed world coordinates.");
                Assert.That(worker.Visible.Count, Is.EqualTo(1), "Demand refresh must not duplicate cached candidates.");
            }
            finally { entries.Clear(); entry.Dispose(); }
        }

        [Test]
        public void IncrementalGpuAccountingMatchesFullRefreshAcrossCameraAndPublicationChanges()
        {
            using var incremental = new GpuSolidChunkCache { GpuBuildDemandEnabled = true };
            using var full = new GpuSolidChunkCache { GpuBuildDemandEnabled = true };
            var random = new System.Random(391);
            const int count = 24;
            var previous = new uint[count];
            foreach (var worker in new[] { incremental, full })
                for (int i = 0; i < count; i++)
                {
                    var coordinate = new int3(i, 0, 0);
                    Field<HashSet<int3>>(worker, "_known").Add(coordinate);
                    Field<Dictionary<int3, ulong>>(worker, "_desiredVersions")[coordinate] = 1;
                }
            for (int frame = 0; frame < 80; frame++)
            {
                bool reset = frame % 7 == 0;
                if (reset)
                    foreach (var worker in new[] { incremental, full })
                    {
                        var empty = Field<Dictionary<int3, ulong>>(worker, "_emptyVersions");
                        empty.Clear();
                        if (frame % 2 == 0)
                            for (int i = 0; i < count; i += 3) empty[new int3(i, 0, 0)] = 1;
                    }
                // Metadata collection may reset public diagnostics; demand retains its own
                // accounting and restores it at the next feedback boundary.
                incremental.BeginVisibilityCollection();
                incremental.BeginGpuDemandFeedback(reset); full.BeginGpuDemandFeedback();
                for (int i = 0; i < count; i++)
                {
                    var coordinate = new int3(i, 0, 0);
                    uint classification = previous[i] & 3;
                    if (random.Next(4) == 0 || frame == 0)
                        classification = new uint[] { 0, 1, 3 }[random.Next(3)];
                    uint geometry = classification | ((uint)random.Next(1, 1000) << 3);
                    if (reset || (previous[i] & 3) != classification)
                        incremental.ApplyGpuDemand(coordinate, geometry, frame);
                    else incremental.UpdateGpuDemandRank(coordinate, geometry);
                    full.ApplyGpuDemand(coordinate, geometry, frame);
                    previous[i] = geometry;
                }
                Assert.AreEqual(full.MissingVisibleCount, incremental.MissingVisibleCount, $"missing frame {frame}");
                Assert.AreEqual(full.LastVisibilityInBandCount, incremental.LastVisibilityInBandCount);
                Assert.AreEqual(full.LastVisibilityFrustumCount, incremental.LastVisibilityFrustumCount);
                CollectionAssert.AreEquivalent(Field<HashSet<int3>>(full, "_dirty"),
                    Field<HashSet<int3>>(incremental, "_dirty"), $"demand frame {frame}");
            }
        }

        [Test]
        public void RankOnlyFeedbackCannotCreateDemandOrChangeClassification()
        {
            using var worker = new GpuSolidChunkCache { GpuBuildDemandEnabled = true };
            var coordinate = int3.zero;
            worker.UpdateGpuDemandRank(coordinate, 3);
            Assert.AreEqual(0, worker.DirtyCount);
            Field<HashSet<int3>>(worker, "_known").Add(coordinate);
            worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(coordinate, 0, 1);
            worker.UpdateGpuDemandRank(coordinate, 3);
            Assert.AreEqual(0, worker.MissingVisibleCount);
            Assert.AreEqual(0, worker.DirtyCount);
            worker.ApplyGpuDemand(coordinate, 3, 2);
            Assert.AreEqual(1, worker.MissingVisibleCount);
            var remove = typeof(GpuSolidChunkCache).GetMethod("TryRemoveChunk", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.True((bool)remove.Invoke(worker, new object[] { coordinate }));
            Assert.AreEqual(0, worker.MissingVisibleCount);
            worker.UpdateGpuDemandRank(coordinate, 3u | (10u << 3));
            Assert.AreEqual(0, worker.DirtyCount);
            Assert.AreEqual(0, worker.LastVisibilityInBandCount);
        }

        [Test]
        public void DemandDiagnosticDistinguishesQueuedCurrentEmptyAndStrandedWork()
        {
            using var worker = new GpuSolidChunkCache { GpuBuildDemandEnabled = true };
            Field<HashSet<int3>>(worker, "_known").Add(int3.zero);
            worker.BeginGpuDemandFeedback(); worker.ApplyGpuDemand(int3.zero, 3, 1);
            Assert.AreEqual(0, worker.CountUnqueuedGpuDemand());
            Field<HashSet<int3>>(worker, "_queuedDirty").Clear();
            Field<HashSet<int3>>(worker, "_queuedVisibleDirty").Clear();
            Assert.AreEqual(1, worker.CountUnqueuedGpuDemand());
            Field<Dictionary<int3, ulong>>(worker, "_emptyVersions")[int3.zero] = ulong.MaxValue;
            Assert.AreEqual(0, worker.CountUnqueuedGpuDemand());
            Field<Dictionary<int3, ulong>>(worker, "_emptyVersions").Clear();
            worker.ApplyGpuDemand(int3.zero, 0, 2);
            Assert.AreEqual(0, worker.CountUnqueuedGpuDemand());
        }

        private static T Field<T>(object owner, string name) =>
            (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    }
}
