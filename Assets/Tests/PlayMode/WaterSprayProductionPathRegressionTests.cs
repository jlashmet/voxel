using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Materials.Api;
using Game.Materials.Runtime;
using NUnit.Framework;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Api;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class WaterSprayProductionPathRegressionTests
    {
        private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnityTest]
        public IEnumerator CascadeSprayFlagSurvivesCanonicalStorageCacheAndGpuUpload()
        {
            var world = new ShowcaseWorld(
                0xA913u,
                brickPoolCapacity: 8192,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2,
                GameMaterialSimulationDefinitions.Create(),
                maxMixedBrickAllocationBytes: 64L * 1024L * 1024L,
                features: ShowcaseFeatureContent.HouseOnly,
                startup: ShowcaseStartupSource.Generate);
            var cache = new CpuWaterSurfaceChunkCache();
            var cameraObject = new GameObject("Cascade spray production-path discriminator camera");
            Camera camera = cameraObject.AddComponent<Camera>();

            try
            {
                world.GenerateRegionBlocking(int3.zero);
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());

                // Same ordinary Cascade material and representative ribbon dimensions used by the
                // showcase. The discriminator intentionally exercises Storage -> production cache ->
                // shared GPU arena rather than calling WaterBrickMeshBatchJob directly.
                int3 ribbonMin = new int3(333, 400, 199);
                int3 ribbonSize = new int3(8, 47, 2);
                Assert.That(
                    world.AuthorVoxelBox(ribbonMin, ribbonSize, GameMaterialIds.Cascade),
                    Is.EqualTo(ribbonSize.x * ribbonSize.y * ribbonSize.z));

                List<int3> ribbonBricks = BricksCovering(ribbonMin, ribbonSize);
                cache.InvalidateSurfaceBricks(world.ReadStorage, ribbonBricks);
                Assert.That(cache.DirtyCount, Is.GreaterThan(0));

                Vector3 centre = (Vector3)((float3)ribbonMin + (float3)ribbonSize * 0.5f)
                               * ShowcaseWorld.VoxelSize;
                camera.transform.position = centre + new Vector3(0f, 0f, -20f);
                camera.transform.LookAt(centre);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 350f;

                const int maxFrames = 120;
                for (int frame = 0; frame < maxFrames && cache.CompletedBuildCount == 0; frame++)
                {
                    cache.Prepare(world.ReadStorage, camera, ShowcaseWorld.VoxelSize, budgetMs: 5.0);
                    // Production frames flush scheduled jobs at the player loop boundary. The Unity
                    // test coroutine can advance its 120 yields in under half a second, leaving the
                    // worker batch undispatched long enough to make this discriminator inconclusive.
                    // Flush scheduling only; never block on JobHandle.Complete().
                    JobHandle.ScheduleBatchedJobs();
                    cache.TryPublishPending(int.MaxValue, out _);
                    if (cache.CompletedBuildCount == 0)
                        yield return null;
                }

                Assert.That(cache.CompletedBuildCount, Is.GreaterThan(0),
                    $"Production water cache failed to publish within {maxFrames} frames. " +
                    $"dirty={cache.DirtyCount} runningJobs={cache.RunningJobCount} " +
                    $"pendingUploads={cache.PendingUploadCount} pendingBytes={cache.PendingUploadBytes} " +
                    $"meshOverflow={cache.MeshOverflowCount} arenaFailures={cache.ArenaAllocationFailures} " +
                    $"blockingCompletionViolations={cache.FramePathBlockingCompletionViolations} " +
                    $"staleBuilds={cache.StaleBuildCount} residents={cache.ResidentCount} " +
                    $"uploadedBytes={cache.UploadedGeometryBytes} residentGpuBytes={cache.ResidentGpuBytes}.");
                IReadOnlyList<CpuWaterSurfaceChunkCache.Entry> visible =
                    cache.CollectVisible(camera, ShowcaseWorld.VoxelSize);
                Assert.That(visible.Count, Is.GreaterThan(0));

                bool sawCascade = false;
                bool sawSpray = false;
                foreach (CpuWaterSurfaceChunkCache.Entry entry in visible)
                {
                    SurfaceGeometryLease lease = ReadLiveLease(entry);
                    Assert.That(lease.IsValid, Is.True);
                    Assert.That(entry.IndexCount, Is.GreaterThan(0));

                    var referencedIndices = new uint[entry.IndexCount];
                    entry.Indices.GetData(
                        referencedIndices, 0, lease.IndexStart, referencedIndices.Length);
                    uint maxVertex = 0;
                    for (int i = 0; i < referencedIndices.Length; i++)
                        maxVertex = math.max(maxVertex, referencedIndices[i]);

                    var vertices = new SmoothSurfaceVertex[(int)maxVertex + 1];
                    entry.Vertices.GetData(vertices, 0, lease.VertexStart, vertices.Length);
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        uint packed = vertices[i].Material;
                        if ((packed & SmoothSurfaceVertex.BaseMaterialMask) != GameMaterialIds.Cascade)
                            continue;
                        sawCascade = true;
                        if ((packed & SmoothSurfaceVertex.WaterSprayFlag) != 0)
                            sawSpray = true;
                    }
                }

                Assert.That(sawCascade, Is.True,
                    "The published production water-cache lease must retain Cascade material identity.");
                Assert.That(sawSpray, Is.True,
                    "A true Cascade lower boundary must retain WaterSprayFlag through canonical storage, production extraction, arena upload, and publication.");
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(cameraObject);
                world.StopBackgroundWork();
                world.Dispose();
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        [UnityTest]
        public IEnumerator RepresentativeCacheJobGetsWorkerTimeWithinTwoSeconds()
        {
            var world = new ShowcaseWorld(
                0xA913u,
                brickPoolCapacity: 8192,
                loadRadiusRegions: 1,
                unloadRadiusRegions: 2,
                GameMaterialSimulationDefinitions.Create(),
                maxMixedBrickAllocationBytes: 64L * 1024L * 1024L,
                features: ShowcaseFeatureContent.HouseOnly,
                startup: ShowcaseStartupSource.Generate);
            var cache = new CpuWaterSurfaceChunkCache();
            var cameraObject = new GameObject("Cascade spray worker-time root-cause probe camera");
            Camera camera = cameraObject.AddComponent<Camera>();

            try
            {
                world.GenerateRegionBlocking(int3.zero);
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());

                int3 ribbonMin = new int3(333, 400, 199);
                int3 ribbonSize = new int3(8, 47, 2);
                Assert.That(
                    world.AuthorVoxelBox(ribbonMin, ribbonSize, GameMaterialIds.Cascade),
                    Is.EqualTo(ribbonSize.x * ribbonSize.y * ribbonSize.z));
                cache.InvalidateSurfaceBricks(world.ReadStorage, BricksCovering(ribbonMin, ribbonSize));

                Vector3 centre = (Vector3)((float3)ribbonMin + (float3)ribbonSize * 0.5f)
                               * ShowcaseWorld.VoxelSize;
                camera.transform.position = centre + new Vector3(0f, 0f, -20f);
                camera.transform.LookAt(centre);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 350f;

                double start = Time.realtimeSinceStartupAsDouble;
                int yields = 0;
                while (cache.CompletedBuildCount == 0
                       && Time.realtimeSinceStartupAsDouble - start < 2.0)
                {
                    cache.Prepare(world.ReadStorage, camera, ShowcaseWorld.VoxelSize, budgetMs: 5.0);
                    JobHandle.ScheduleBatchedJobs();
                    cache.TryPublishPending(int.MaxValue, out _);
                    if (cache.CompletedBuildCount == 0)
                    {
                        yields++;
                        yield return null;
                    }
                }

                double elapsed = Time.realtimeSinceStartupAsDouble - start;
                Assert.That(cache.CompletedBuildCount, Is.GreaterThan(0),
                    $"Representative water-cache job still did not publish after {elapsed:F3}s/{yields} yields. " +
                    $"dirty={cache.DirtyCount} runningJobs={cache.RunningJobCount} " +
                    $"pendingUploads={cache.PendingUploadCount} meshOverflow={cache.MeshOverflowCount} " +
                    $"staleBuilds={cache.StaleBuildCount}.");
                Debug.Log($"WATER_SPRAY_ROOT_CAUSE cache-published elapsed={elapsed:F3}s yields={yields}");
            }
            finally
            {
                cache.Dispose();
                Object.DestroyImmediate(cameraObject);
                world.StopBackgroundWork();
                world.Dispose();
                VoxelMaterialPresentationInstaller.Apply(GameMaterialRenderingDefinitions.Create());
            }
        }

        private static SurfaceGeometryLease ReadLiveLease(CpuWaterSurfaceChunkCache.Entry entry)
        {
            FieldInfo field = typeof(CpuWaterSurfaceChunkCache.Entry).GetField("_liveLease", InstanceNonPublic);
            Assert.That(field, Is.Not.Null);
            return (SurfaceGeometryLease)field.GetValue(entry);
        }

        private static List<int3> BricksCovering(int3 min, int3 size)
        {
            const int blockEdgeLog2 = 3;
            int3 maxInclusive = min + size - 1;
            int3 minBrick = new int3(
                min.x >> blockEdgeLog2,
                min.y >> blockEdgeLog2,
                min.z >> blockEdgeLog2);
            int3 maxBrick = new int3(
                maxInclusive.x >> blockEdgeLog2,
                maxInclusive.y >> blockEdgeLog2,
                maxInclusive.z >> blockEdgeLog2);
            var result = new List<int3>();
            for (int z = minBrick.z; z <= maxBrick.z; z++)
            for (int y = minBrick.y; y <= maxBrick.y; y++)
            for (int x = minBrick.x; x <= maxBrick.x; x++)
                result.Add(new int3(x, y, z));
            return result;
        }
    }
}
