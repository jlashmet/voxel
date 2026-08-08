using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Storage;
using VoxelEngine.Showcase;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class FallingVoxelPhysicsTests
    {
        [Test]
        public void SeveredComponentLeavesGridAsBoundedDetachedChunks()
        {
            var world = new ShowcaseWorld(123u, 4096, 1, 2);
            try
            {
                // Indestructible ground, a two-voxel stump, one removed joint, and a three-voxel
                // detached top. The upper component has no path to the bedrock anchor.
                Set(world, new int3(20, 0, 20), ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 6; y++)
                    Set(world, new int3(20, y, 20), ShowcaseWorld.MatWood);

                int changed = world.RemoveAndResolveCollapse(new int3(20, 3, 20));

                Assert.GreaterOrEqual(changed, 4, "joint plus detached top should leave the grid");
                Assert.Greater(world.PendingDetachedChunks, 0);
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, new int3(20, 6, 20)));

                int detached = 0;
                while (world.TryDequeueDetachedChunk(out var chunk))
                {
                    Assert.LessOrEqual(chunk.Voxels.Length, GpuDebrisSystem.MaxVoxelsPerChunk);
                    detached += chunk.Voxels.Length;
                    world.RestoreDetachedChunk(chunk);
                }

                Assert.AreEqual(3, detached);
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 4, 20)));
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 5, 20)));
                Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, new int3(20, 6, 20)));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void DetachedBeamBeyondFormerScanBoundaryCollapsesCompletely()
        {
            var world = new ShowcaseWorld(321u, 8192, 1, 2);
            try
            {
                Set(world, new int3(20, 0, 20), ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 10; y++)
                    Set(world, new int3(20, y, 20), ShowcaseWorld.MatWood);
                for (int x = 20; x <= 140; x++)
                    Set(world, new int3(x, 11, 20), ShowcaseWorld.MatWood);

                world.RemoveAndResolveCollapse(new int3(20, 10, 20));

                int detached = 0;
                while (world.TryDequeueDetachedChunk(out var chunk))
                    detached += chunk.Voxels.Length;
                Assert.AreEqual(121, detached,
                    "a long unsupported top must not be treated as supported at a scan boundary");
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, new int3(140, 11, 20)));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void TowerOverloadsSingleRemainingSupportThread()
        {
            var world = new ShowcaseWorld(654u, 16384, 1, 2);
            try
            {
                for (int x = 20; x <= 21; x++)
                {
                    Set(world, new int3(x, 0, 20), ShowcaseWorld.MatBedrock);
                    for (int y = 1; y <= 10; y++)
                        Set(world, new int3(x, y, 20), ShowcaseWorld.MatWood);
                }
                for (int x = 15; x <= 25; x++)
                for (int y = 11; y <= 30; y++)
                for (int z = 15; z <= 25; z++)
                    Set(world, new int3(x, y, z), 6);

                // A bridge to a separately grounded structure makes the tower globally
                // connected. Local load paths must still fail at the destroyed base.
                Set(world, new int3(60, 0, 20), ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 20; y++)
                    Set(world, new int3(60, y, 20), 6);
                for (int x = 25; x <= 60; x++)
                    Set(world, new int3(x, 20, 20), 6);

                world.RemoveAndResolveCollapse(new int3(21, 10, 20));

                int detached = 0;
                while (world.TryDequeueDetachedChunk(out var chunk))
                    detached += chunk.Voxels.Length;
                Assert.GreaterOrEqual(detached, 2400,
                    "a full tower remained suspended by one wood voxel of contact area");
                Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, new int3(20, 30, 20)));
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void NaturalTerrainIsNeverClassifiedAsOverloadedArchitecture()
        {
            var world = new ShowcaseWorld(987u, 16384, 1, 2);
            try
            {
                for (int x = 0; x <= 24; x++)
                for (int z = 0; z <= 24; z++)
                {
                    Set(world, new int3(x, 0, z), ShowcaseWorld.MatBedrock);
                    for (int y = 1; y <= 12; y++)
                        Set(world, new int3(x, y, z), y == 12 ? (byte)10 : ShowcaseWorld.MatStone);
                }

                int changed = world.RemoveAndResolveCollapse(new int3(12, 8, 12));

                Assert.AreEqual(1, changed, "terrain around a small cavity was detached as debris");
                Assert.Zero(world.PendingDetachedChunks,
                    "natural ground entered the animated structural debris queue");
            }
            finally
            {
                world.Dispose();
            }
        }

        [Test]
        public void CastleScaleHollowTowerFailsOnOneRemoteColumn()
        {
            var world = new ShowcaseWorld(1357u, 262144, 1, 2);
            try
            {
                const int cx = 100;
                const int cz = 100;
                const int radius = 30;
                const int innerRadius = 18;
                int outerSq = radius * radius;
                int innerSq = innerRadius * innerRadius;

                // Two narrow base columns initially carry a hollow tower. Removing the near one
                // leaves a globally connected tower on a remote thread, matching the castle case.
                foreach (int x in new[] { cx - radius, cx + radius })
                {
                    Set(world, new int3(x, 0, cz), ShowcaseWorld.MatBedrock);
                    for (int y = 1; y <= 10; y++) Set(world, new int3(x, y, cz), 6);
                }

                int towerVoxels = 0;
                for (int x = cx - radius; x <= cx + radius; x++)
                for (int z = cz - radius; z <= cz + radius; z++)
                {
                    int dx = x - cx;
                    int dz = z - cz;
                    int distanceSq = dx * dx + dz * dz;
                    if (distanceSq > outerSq || distanceSq < innerSq) continue;
                    for (int y = 11; y <= 160; y++)
                    {
                        Set(world, new int3(x, y, z), 6);
                        towerVoxels++;
                    }
                }
                Assert.Greater(towerVoxels, 262144,
                    "fixture no longer exceeds the former conservative traversal cap");

                world.RemoveAndResolveCollapse(new int3(cx + radius, 10, cz));

                int detached = 0;
                int chunks = 0;
                while (world.TryDequeueDetachedChunk(out var chunk))
                {
                    detached += chunk.Voxels.Length;
                    chunks++;
                }
                Assert.AreEqual(towerVoxels, detached,
                    "castle-scale upper tower remained on the remote support thread");
                Assert.LessOrEqual(chunks, GpuDebrisSystem.MaxChunks,
                    "tower collapse exceeds the bounded authoritative GPU chunk capacity");
                Assert.AreEqual(VoxelDimensions.MaterialEmpty,
                    Get(world, new int3(cx, 160, cz + radius)));
            }
            finally
            {
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator EightRapidStructuralFailuresStayBounded()
        {
            var world = new ShowcaseWorld(777u, 65536, 1, 2);
            var debris = new GpuDebrisSystem();
            try
            {
                Assert.True(debris.Available);
                for (int structure = 0; structure < 8; structure++)
                {
                    int cx = 20 + structure * 12;
                    for (int x = cx; x <= cx + 1; x++)
                    {
                        Set(world, new int3(x, 0, 20), ShowcaseWorld.MatBedrock);
                        for (int y = 1; y <= 10; y++)
                            Set(world, new int3(x, y, 20), ShowcaseWorld.MatWood);
                    }
                    for (int x = cx - 3; x <= cx + 3; x++)
                    for (int y = 11; y <= 22; y++)
                    for (int z = 17; z <= 23; z++)
                        Set(world, new int3(x, y, z), 6);
                }

                var collapseTimer = Stopwatch.StartNew();
                for (int structure = 0; structure < 8; structure++)
                    world.RemoveAndResolveCollapse(new int3(21 + structure * 12, 10, 20));
                collapseTimer.Stop();
                Assert.Greater(world.PendingDetachedChunks, 8);
                Assert.Less(collapseTimer.Elapsed.TotalMilliseconds, 1000,
                    "bounded structural classification regressed into a multi-second stall");

                double worstSubmitMs = 0;
                for (int frame = 0; frame < 8; frame++)
                {
                    var frameTimer = Stopwatch.StartNew();
                    debris.Step(world, 0.016f);
                    frameTimer.Stop();
                    worstSubmitMs = System.Math.Max(worstSubmitMs, frameTimer.Elapsed.TotalMilliseconds);
                    yield return null;
                }

                Assert.Greater(debris.ActiveChunks, 0);
                Assert.LessOrEqual(debris.ActiveChunks, GpuDebrisSystem.MaxChunks);
                Assert.Less(worstSubmitMs, 100,
                    "GPU submission exceeded its per-frame budget under eight rapid failures");
                UnityEngine.Debug.Log($"### GPU_DEBRIS_STRESS collapse={collapseTimer.Elapsed.TotalMilliseconds:0.0}ms " +
                                      $"worstSubmit={worstSubmitMs:0.0}ms active={debris.ActiveChunks} " +
                                      $"queued={world.PendingDetachedChunks}");
            }
            finally
            {
                debris.Dispose();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator GpuDebrisSimulatesAndSettlesDetachedChunk()
        {
            var world = new ShowcaseWorld(456u, 4096, 1, 2);
            var debris = new GpuDebrisSystem();
            try
            {
                Assert.True(debris.Available, "compute-shader debris is unavailable on this test device");
                int3 column = new int3(20, 0, 20);
                Set(world, column, ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 6; y++)
                    Set(world, column + new int3(0, y, 0), ShowcaseWorld.MatWood);
                world.RemoveAndResolveCollapse(column + new int3(0, 3, 0));

                debris.Step(world, 0.016f);
                Assert.Greater(debris.ActiveChunks, 0,
                    "detached voxels were never submitted to the GPU");
                world.RegionsNeedingUpload.Clear();

                // Moving debris is presentation data. It must not rewrite/upload the voxel grid
                // every frame—the exact hot path that made repeated tornadoes progressively lag.
                for (int i = 0; i < 6; i++)
                {
                    debris.Step(world, 0.016f);
                    yield return null;
                }
                Assert.Zero(world.RegionsNeedingUpload.Count,
                    "GPU flight rewrote authoritative voxel regions before settlement");

                float deadline = Time.realtimeSinceStartup + 2.5f;
                while (debris.ActiveChunks > 0 && Time.realtimeSinceStartup < deadline)
                {
                    debris.Step(world, 0.033f);
                    yield return null;
                }

                Assert.Zero(debris.ActiveChunks,
                    "wood debris kept animating instead of settling within its short lifetime");
                int rebaked = 0;
                for (int x = -20; x <= 60; x++)
                for (int y = 1; y <= 30; y++)
                for (int z = -20; z <= 60; z++)
                    if (Get(world, new int3(x, y, z)) == ShowcaseWorld.MatWood) rebaked++;
                Assert.GreaterOrEqual(rebaked, 5,
                    "settled chunk did not rejoin the authoritative grid near its landing area");
            }
            finally
            {
                debris.Dispose();
                world.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator CaptureGpuDebrisInFlight()
        {
            const string output = "/tmp/physics_shots/gpu_debris_in_flight.png";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var world = new ShowcaseWorld(789u, 8192, 1, 2);
            var debris = new GpuDebrisSystem();
            var cameraObject = new GameObject("GPU debris capture camera");
            var lightObject = new GameObject("GPU debris capture light");
            try
            {
                Assert.True(debris.Available);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.045f, 0.065f, 0.10f);
                camera.fieldOfView = 48f;
                camera.transform.position = new Vector3(2f, 3.1f, -2.4f);
                camera.transform.LookAt(new Vector3(2f, 1.8f, 2f));
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(42f, -28f, 0f);

                Set(world, new int3(20, 0, 20), ShowcaseWorld.MatBedrock);
                for (int y = 1; y <= 8; y++)
                    Set(world, new int3(20, y, 20), ShowcaseWorld.MatStone);
                for (int x = 18; x <= 22; x++)
                for (int y = 9; y <= 11; y++)
                for (int z = 18; z <= 22; z++)
                    Set(world, new int3(x, y, z), (x + y + z) % 3 == 0
                        ? ShowcaseWorld.MatWood : ShowcaseWorld.MatStone);
                world.RemoveAndResolveCollapse(new int3(20, 8, 20));

                for (int i = 0; i < 9; i++)
                {
                    debris.Step(world, 0.033f);
                    yield return null;
                }

                var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = rt;
                debris.Step(world, 0.016f);
                camera.Render();
                RenderTexture.active = rt;
                var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
                texture.Apply();
                File.WriteAllBytes(output, texture.EncodeToPNG());
                RenderTexture.active = null;
                camera.targetTexture = null;
                Object.DestroyImmediate(texture);
                rt.Release();
                Object.DestroyImmediate(rt);
                Assert.True(File.Exists(output));
                Assert.Greater(debris.ActiveChunks, 0);
            }
            finally
            {
                debris.Dispose();
                world.Dispose();
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(lightObject);
            }
        }

        [UnityTest]
        public IEnumerator TornadoTravelsBeforeImpactingKnownVoxelTarget()
        {
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var world = (ShowcaseWorld)typeof(VoxelShowcase)
                .GetField("_world", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(showcase);

            var origin = new Vector3(10f, 40f, 10f);
            int3 target = new int3(160, 400, 100); // six metres along +X, in otherwise open sky.
            Set(world, target, ShowcaseWorld.MatWood);

            showcase.LaunchTornado(origin, Vector3.right, 2);
            Assert.AreEqual(1, showcase.ActiveTornadoCount);
            Assert.AreEqual(ShowcaseWorld.MatWood, Get(world, target),
                "launch itself must not apply an instant hitscan blast");

            var step = typeof(VoxelShowcase).GetMethod(
                "StepTornadoes", BindingFlags.NonPublic | BindingFlags.Instance);
            for (int i = 0; i < 10 && showcase.ActiveTornadoCount > 0; i++)
                step.Invoke(showcase, new object[] { 0.025f });

            Assert.Zero(showcase.ActiveTornadoCount, "projectile never reached its target");
            Assert.AreEqual(VoxelDimensions.MaterialEmpty, Get(world, target),
                "impact did not destroy the target voxel");
        }

        [UnityTest]
        public IEnumerator CaptureTornadoInFlight()
        {
            const string output = "/tmp/physics_shots/tornado_in_flight.png";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;

            var showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            var cam = Camera.main;
            typeof(VoxelShowcase).GetField("m_FlyMode", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, true);
            typeof(VoxelShowcase).GetField("_mouseLook", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(showcase, false);
            var origin = new Vector3(10f, 90f, 10f);
            showcase.LaunchTornado(origin, Vector3.right, 2);

            var step = typeof(VoxelShowcase).GetMethod(
                "StepTornadoes", BindingFlags.NonPublic | BindingFlags.Instance);
            step.Invoke(showcase, new object[] { 0.12f });

            cam.transform.position = new Vector3(10.5f, 92.2f, 4.8f);
            cam.transform.LookAt(new Vector3(13.4f, 90f, 10f));
            cam.fieldOfView = 42f;
            yield return null;

            var rt = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            var oldTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var texture = new Texture2D(960, 540, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            texture.Apply();
            File.WriteAllBytes(output, texture.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = oldTarget;
            Object.DestroyImmediate(texture);
            rt.Release();
            Object.DestroyImmediate(rt);

            Assert.True(File.Exists(output));
        }

        private static void Set(ShowcaseWorld world, int3 voxel, byte material) =>
            VoxelAccess.SetVoxel(ref world.Table, ref world.Pool, voxel, material);

        private static byte Get(ShowcaseWorld world, int3 voxel) =>
            VoxelAccess.GetVoxel(ref world.Table, in world.Pool, voxel);
    }
}
