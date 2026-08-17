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
        public void VisibleCurrentGenerationBuildDoesNotQueueDuplicateAdmission()
        {
            using var cache = new CpuTransvoxelChunkCache(1)
            {
                MinViewDistanceMetres = 0f,
                MaxViewDistanceMetres = 96f,
                ShardCount = 1,
                ShardIndex = 0,
            };
            cache.SetClipmapWindow(int3.zero, 8);

            MethodInfo discover = typeof(CpuTransvoxelChunkCache).GetMethod(
                "DiscoverSurfaceBricks", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo select = typeof(CpuTransvoxelChunkCache).GetMethod(
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
        public void OutOfBandDiscoveryParksUntilChunkBecomesVisibleInRing()
        {
            using var cache = new CpuTransvoxelChunkCache(4)
            {
                MinViewDistanceMetres = 192f,
                MaxViewDistanceMetres = 288f,
                ShardCount = 1,
                ShardIndex = 0,
            };
            cache.SetClipmapWindow(int3.zero, 16);

            MethodInfo discover = typeof(CpuTransvoxelChunkCache).GetMethod(
                "DiscoverSurfaceBricks", BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo select = typeof(CpuTransvoxelChunkCache).GetMethod(
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
