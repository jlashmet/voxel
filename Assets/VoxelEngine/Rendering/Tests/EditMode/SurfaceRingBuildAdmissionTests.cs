using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class SurfaceRingBuildAdmissionTests
    {
        [Test]
        public void GpuBackgroundAdmissionUsesGpuDistanceRankAndRetainsUnselectedDemand()
        {
            using var cache = new GpuSolidChunkCache { GpuBuildDemandEnabled = true, AllowBackgroundBuilds = true };
            cache.SetClipmapWindow(int3.zero, 8);
            var known = (HashSet<int3>)typeof(GpuSolidChunkCache).GetField("_known", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            var select = typeof(GpuSolidChunkCache).GetMethod("BeginNearestBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            int3 first = int3.zero, second = new(1, 0, 0);
            known.Add(first); known.Add(second);
            cache.BeginGpuDemandFeedback();
            cache.ApplyGpuDemand(first, 1u | (1000u << 3), 1);
            cache.ApplyGpuDemand(second, 1u | (2000u << 3), 1);
            cache.BeginGpuDemandFeedback(false);
            cache.UpdateGpuDemandRank(second, 1u | (10u << 3));
            Assert.True((bool)select.Invoke(cache, new object[] { null, 0.1f, double.MaxValue }));
            object build = typeof(GpuSolidChunkCache).GetField("_build", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            Assert.That((int3)build.GetType().GetField("Coordinate").GetValue(build), Is.EqualTo(second));
            var dirty = (HashSet<int3>)typeof(GpuSolidChunkCache).GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            Assert.True(dirty.Contains(first));
        }

        [Test]
        public void GpuAdmissionPrioritizesVisibleDemandAndDoesNotRequeueCurrentBuild()
        {
            using var cache = new GpuSolidChunkCache { GpuBuildDemandEnabled = true };
            cache.SetClipmapWindow(int3.zero, 8);
            var known = (HashSet<int3>)typeof(GpuSolidChunkCache).GetField("_known", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            var select = typeof(GpuSolidChunkCache).GetMethod("BeginNearestBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            int3 background = new(1, 0, 0), target = int3.zero;
            known.Add(background); known.Add(target);
            cache.BeginGpuDemandFeedback();
            cache.ApplyGpuDemand(background, 1, 1);
            cache.ApplyGpuDemand(target, 3, 1);
            // A null camera proves this production admission route consumes GPU decisions.
            Assert.True((bool)select.Invoke(cache, new object[] { null, 0.1f, double.MaxValue }));
            object build = typeof(GpuSolidChunkCache).GetField("_build", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            Assert.That((int3)build.GetType().GetField("Coordinate").GetValue(build), Is.EqualTo(target));
            var dirty = (HashSet<int3>)typeof(GpuSolidChunkCache).GetField("_dirty", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(cache);
            Assert.False(dirty.Contains(target));
            cache.BeginGpuDemandFeedback(); cache.ApplyGpuDemand(target, 3, 2);
            Assert.False(dirty.Contains(target), "The active current generation must not acquire duplicate dirty demand.");
            typeof(GpuSolidChunkCache).GetMethod("RejectPendingOrCompletedBuild", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(cache, new object[] { true });
            Assert.True(dirty.Contains(target), "Rejection must retry without waiting for a changed camera query.");
            Assert.True((bool)select.Invoke(cache, new object[] { null, 0.1f, double.MaxValue }));
        }

        [Test]
        public void VisibleCurrentGenerationBuildDoesNotQueueDuplicateAdmission()
        {
            using var cache = new GpuSolidChunkCache(1)
            {
                MinViewDistanceMetres = 0f,
                MaxViewDistanceMetres = 96f,
                ShardCount = 1,
                ShardIndex = 0,
            };
            cache.SetClipmapWindow(int3.zero, 8);

            MethodInfo discover = typeof(GpuSolidChunkCache).GetMethod(
                "DiscoverSurfaceBricks", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo select = typeof(GpuSolidChunkCache).GetMethod(
                "BeginNearestBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(discover);
            Assert.NotNull(select);

            // Interior brick coordinates admit exactly chunk (0,0,0), avoiding border-neighbour
            // discovery so the active-build assertion is unambiguous.
            int admitted = (int)discover.Invoke(cache, new object[]
            {
                new List<int3> { new int3(1, 1, 1) }
            });
            Assert.AreEqual(1, admitted);
            Assert.AreEqual(0, cache.DirtyCount,
                "Authoritative discovery should not consume build admission before ring demand is known.");

            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.ActiveCamera");
            var camera = cameraObject.AddComponent<Camera>();
            try
            {
                camera.transform.position = new Vector3(0f, 0f, -10f);
                camera.transform.LookAt(Vector3.zero);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 200f;

                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                cache.BeginVisibilityCollection();
                cache.CollectVisibleCoordinate(int3.zero, planes,
                    camera.transform.position, 0.1f, 1);
                Assert.AreEqual(1, cache.DirtyCount,
                    "Current-ring visibility did not activate the discovered authoritative generation.");

                bool selected = (bool)select.Invoke(cache, new object[]
                {
                    camera,
                    0.1f,
                    Time.realtimeSinceStartupAsDouble + 1.0,
                });
                Assert.True(selected);
                Assert.AreEqual(1, cache.DirtyCount,
                    "The selected generation should be represented only by the active build.");

                cache.BeginVisibilityCollection();
                cache.CollectVisibleCoordinate(int3.zero, planes,
                    camera.transform.position, 0.1f, 2);

                Assert.AreEqual(1, cache.MissingVisibleCount);
                Assert.AreEqual(1, cache.DirtyCount,
                    "Visibility requeued the same source generation while it was already in flight.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void FrustumVisibleDemandBypassesBackgroundPrefetchBacklog()
        {
            using var cache = new GpuSolidChunkCache(1)
            {
                MinViewDistanceMetres = 0f,
                MaxViewDistanceMetres = 96f,
                ShardCount = 1,
                ShardIndex = 0,
            };
            cache.SetClipmapWindow(new int3(0, 0, -3), 20);

            MethodInfo track = typeof(GpuSolidChunkCache).GetMethod(
                "TrackKnown", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo invalidate = typeof(GpuSolidChunkCache).GetMethod(
                "Invalidate", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo select = typeof(GpuSolidChunkCache).GetMethod(
                "BeginNearestBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo buildField = typeof(GpuSolidChunkCache).GetField(
                "_build", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(track);
            Assert.NotNull(invalidate);
            Assert.NotNull(select);
            Assert.NotNull(buildField);

            // Fill more than one bounded 64-candidate selection slice with valid in-band
            // background prefetch work, all queued before the target. This is the production
            // shape that produced multi-second visible queue latency in PR run 32014802229.
            int background = 0;
            for (int z = -10; z <= -5; z++)
            for (int x = -8; x < 8; x++)
            {
                int3 coordinate = new(x, 0, z);
                Assert.True((bool)track.Invoke(cache, new object[] { coordinate }));
                invalidate.Invoke(cache, new object[] { coordinate });
                background++;
            }
            Assert.Greater(background, 64);

            int3 target = int3.zero;
            int3 closerTarget = new(0, 0, -2);
            Assert.True((bool)track.Invoke(cache, new object[] { target }));
            invalidate.Invoke(cache, new object[] { target });
            Assert.True((bool)track.Invoke(cache, new object[] { closerTarget }));
            invalidate.Invoke(cache, new object[] { closerTarget });
            Assert.AreEqual(background + 2, cache.DirtyCount);

            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.PriorityCamera");
            var camera = cameraObject.AddComponent<Camera>();
            try
            {
                camera.transform.position = new Vector3(3.2f, 3.2f, -20f);
                camera.transform.LookAt(new Vector3(3.2f, 3.2f, 3.2f));
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 200f;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

                cache.BeginVisibilityCollection();
                cache.CollectVisibleCoordinate(target, planes,
                    camera.transform.position, 0.1f, 1);
                cache.CollectVisibleCoordinate(closerTarget, planes,
                    camera.transform.position, 0.1f, 1);
                Assert.AreEqual(2, cache.MissingVisibleCount,
                    "Both fixture targets must be real frustum-visible missing chunks.");

                bool selected = (bool)select.Invoke(cache, new object[]
                {
                    camera,
                    0.1f,
                    Time.realtimeSinceStartupAsDouble + 1.0,
                });
                Assert.True(selected);

                object build = buildField.GetValue(cache);
                FieldInfo coordinateField = build.GetType().GetField(
                    "Coordinate", BindingFlags.Instance | BindingFlags.Public);
                Assert.NotNull(coordinateField);
                int3 selectedCoordinate = (int3)coordinateField.GetValue(build);
                Assert.AreEqual(target, selectedCoordinate,
                    "Visible demand was rescanned/re-ranked instead of taking the first current "
                  + "priority record ahead of the saturated background prefetch FIFO.");
                Assert.Greater(cache.DirtyCount, 64,
                    "Prioritizing the visible hole must not discard background prefetch work.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void OutOfBandDiscoveryParksUntilChunkBecomesVisibleInRing()
        {
            using var cache = new GpuSolidChunkCache(4)
            {
                MinViewDistanceMetres = 192f,
                MaxViewDistanceMetres = 288f,
                ShardCount = 1,
                ShardIndex = 0,
            };
            cache.SetClipmapWindow(int3.zero, 16);

            MethodInfo discover = typeof(GpuSolidChunkCache).GetMethod(
                "DiscoverSurfaceBricks", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo select = typeof(GpuSolidChunkCache).GetMethod(
                "BeginNearestBuild", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(discover);
            Assert.NotNull(select);

            int admitted = (int)discover.Invoke(cache, new object[]
            {
                new List<int3> { int3.zero }
            });
            Assert.Greater(admitted, 0);
            Assert.AreEqual(0, cache.DirtyCount,
                "Discovery should retain authoritative versions without dirtying an LOD before ring ownership is known.");

            var cameraObject = new GameObject("SurfaceRingBuildAdmissionTests.Camera");
            var camera = cameraObject.AddComponent<Camera>();
            try
            {
                camera.transform.position = Vector3.zero;
                bool selected = (bool)select.Invoke(cache, new object[]
                {
                    camera,
                    0.1f,
                    Time.realtimeSinceStartupAsDouble + 1.0,
                });

                Assert.False(selected,
                    "Step-4 must not build chunks that are wholly inside the finer ring.");
                Assert.AreEqual(0, cache.DirtyCount,
                    "Out-of-band discovery remained in the active dirty FIFO and will be rescanned forever.");

                camera.transform.position = new Vector3(0f, 0f, -220f);
                camera.transform.LookAt(Vector3.zero);
                camera.nearClipPlane = 0.3f;
                camera.farClipPlane = 500f;
                Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);

                cache.BeginVisibilityCollection();
                cache.CollectVisibleCoordinate(int3.zero, planes,
                    camera.transform.position, 0.1f, 1);

                Assert.AreEqual(1, cache.MissingVisibleCount,
                    "The in-band discovered chunk should be reported as a real visible hole until built.");
                Assert.Greater(cache.DirtyCount, 0,
                    "Visibility did not reactivate parked authoritative work when the chunk entered its ring.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
