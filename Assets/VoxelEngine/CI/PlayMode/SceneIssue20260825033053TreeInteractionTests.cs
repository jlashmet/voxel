using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    [NUnit.Framework.Explicit("Regression fixture for SceneIssue 20260825-033053-588-VoxelShowcase.")]
    public sealed class SceneIssue20260825033053TreeInteractionTests
    {
        private const float StartupTimeoutSeconds = 30f;
        private const float ShotSweepRadiusMetres = 0.28f;
        private const float ShotBlastRadiusMetres = 1.2f;
        private const int CapturedWidth = 1364;
        private const int CapturedHeight = 836;
        private const float CapturedAspect = CapturedWidth / (float)CapturedHeight;
        private static readonly Vector3 CapturedPosition = new(
            30.713180541992189f, 23.950254440307618f, 154.71759033203126f);
        private static readonly Quaternion CapturedRotation = new(
            0.0947750136256218f, -0.2117554098367691f,
            -0.020636681467294694f, -0.9724975824356079f);
        private const float CapturedFov = 70f;

        [UnityTest]
        public IEnumerator CapturedViewTreeBlocksPlayerAndRespondsToShot()
        {
            TreeWorldRuntime.Clear();
#if UNITY_EDITOR
            UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                "Assets/Scenes/VoxelShowcase.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
#else
            AsyncOperation load = SceneManager.LoadSceneAsync("VoxelShowcase", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;
#endif

            GameObject sceneCameraObject = GameObject.Find("Showcase Camera");
            Assert.That(sceneCameraObject, Is.Not.Null,
                "The saved capture camera 'Showcase Camera' was not present after scene load.");
            Camera camera = sceneCameraObject.GetComponent<Camera>();
            Assert.That(camera, Is.Not.Null,
                "The saved capture object no longer owns a Camera component.");
            camera.fieldOfView = CapturedFov;
            camera.aspect = CapturedAspect;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 16000f;

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while ((!ShowcaseTreePopulation.Completed || TreeWorldRuntime.Instances.Count == 0)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                sceneCameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);
                yield return null;
            }

            Assert.That(ShowcaseTreePopulation.Completed, Is.True,
                "The captured VoxelShowcase tree population did not complete.");
            Assert.That(TreeWorldRuntime.Instances.Count, Is.GreaterThan(0),
                "The captured VoxelShowcase scene published no semantic trees.");

            // Pin the real scene camera at the saved fixture for additional frames so streaming,
            // surface scheduling and procedural renderers settle around the exact current view.
            for (int frame = 0; frame < 60; frame++)
            {
                sceneCameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);
                yield return null;
            }
            sceneCameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);

            string outputDirectory = GetOutputDirectory();
            Directory.CreateDirectory(outputDirectory);
            CaptureVerificationFrame(camera, outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "verification-current-replay.txt"),
                $"capturedPosition={CapturedPosition}\n" +
                $"capturedRotation={CapturedRotation}\n" +
                $"capturedFov={CapturedFov:F1}\n" +
                $"semanticTreeCount={TreeWorldRuntime.Instances.Count}\n");

            var treeDamage = new TreeDamageService();
            Assert.That(TryFindCapturedViewTree(
                            camera, treeDamage,
                            out int hitTreeIndex, out float3 hitMetres,
                            out float3 rayDirection, out float viewportX, out float viewportY),
                        Is.True,
                        "No authored semantic branch geometry is visible and shootable from the saved camera view.");

            TreeInstance instance = TreeWorldRuntime.Instances[hitTreeIndex];
            TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            TreeBranchSegment lowerTrunk = skeleton.Branches
                .Where(branch => branch.Level == 0)
                .OrderBy(branch => (branch.Start.y + branch.End.y) * 0.5f)
                .First();
            float3 collisionMidpoint = instance.PositionMetres
                + (lowerTrunk.Start + lowerTrunk.End) * 0.5f;
            float3 playerHalfExtents = new(0.30f, 0.90f, 0.30f);
            bool blocksPlayer = treeDamage.OverlapsWoodAabb(
                collisionMidpoint - playerHalfExtents,
                collisionMidpoint + playerHalfExtents);
            Assert.That(blocksPlayer, Is.True,
                "The authored tree shot from the saved view must block a player-sized volume at its lower trunk.");

            int removedBefore = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
            treeDamage.ApplyBlast(hitMetres, ShotBlastRadiusMetres, rayDirection);
            int removedAfter = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
            Assert.That(removedAfter, Is.GreaterThan(removedBefore),
                "A shot from the saved view hit semantic tree geometry but did not remove any branch geometry.");

            TreeDamageState damage = TreeWorldRuntime.Damage[hitTreeIndex];
            File.WriteAllText(Path.Combine(outputDirectory, "verification-metrics.txt"),
                $"capturedPosition={CapturedPosition}\n" +
                $"capturedFov={CapturedFov:F1}\n" +
                $"viewportHit=({viewportX:F3},{viewportY:F3})\n" +
                $"treeIndex={hitTreeIndex}\n" +
                $"hitMetres={hitMetres}\n" +
                $"collisionMidpoint={collisionMidpoint}\n" +
                $"blocksPlayer={blocksPlayer}\n" +
                $"removedBranchesBefore={removedBefore}\n" +
                $"removedBranchesAfter={removedAfter}\n" +
                $"severed={damage.Severed}\n");

            Debug.Log(
                "SCENEISSUE 20260825-033053-588 " +
                $"viewport=({viewportX:F3},{viewportY:F3}) tree={hitTreeIndex} " +
                $"hit={hitMetres} blocksPlayer={blocksPlayer} " +
                $"removed={removedBefore}->{removedAfter} severed={damage.Severed}");
            TreeWorldRuntime.Clear();
        }

        private static string GetOutputDirectory()
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Artifacts", "SingleTest", "SceneIssue20260825-033053-588");
        }

        private static void CaptureVerificationFrame(Camera camera, string outputDirectory)
        {
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var target = new RenderTexture(CapturedWidth, CapturedHeight, 24, RenderTextureFormat.ARGB32);
            var texture = new Texture2D(CapturedWidth, CapturedHeight, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, CapturedWidth, CapturedHeight), 0, 0, false);
                texture.Apply(false, false);
                File.WriteAllBytes(
                    Path.Combine(outputDirectory, "verification-current-replay.png"),
                    texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(texture);
                target.Release();
                Object.Destroy(target);
            }
        }

        private static bool TryFindCapturedViewTree(
            Camera camera,
            TreeDamageService treeDamage,
            out int treeIndex,
            out float3 hitMetres,
            out float3 rayDirection,
            out float viewportX,
            out float viewportY)
        {
            treeIndex = -1;
            hitMetres = default;
            rayDirection = default;
            viewportX = 0f;
            viewportY = 0f;
            float bestDistanceSquared = float.MaxValue;
            float3 rayOrigin = (float3)camera.transform.position;
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(camera);

            for (int candidateTreeIndex = 0;
                 candidateTreeIndex < TreeWorldRuntime.Instances.Count;
                 candidateTreeIndex++)
            {
                TreeInstance instance = TreeWorldRuntime.Instances[candidateTreeIndex];
                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                float3 root = instance.PositionMetres;

                for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
                {
                    TreeBranchSegment branch = skeleton.Branches[branchIndex];
                    float3 start = root + branch.Start;
                    float3 end = root + branch.End;
                    float radius = math.max(0.01f, math.max(branch.RadiusStart, branch.RadiusEnd));
                    float3 centre = (start + end) * 0.5f;
                    float3 size = math.abs(end - start) + radius * 2f;
                    var branchBounds = new Bounds((Vector3)centre, (Vector3)size);
                    if (!GeometryUtility.TestPlanesAABB(frustumPlanes, branchBounds))
                        continue;

                    const int samples = 5;
                    for (int sampleIndex = 0; sampleIndex < samples; sampleIndex++)
                    {
                        float t = sampleIndex / (float)(samples - 1);
                        float3 targetMetres = math.lerp(start, end, t);
                        Vector3 viewport = camera.WorldToViewportPoint((Vector3)targetMetres);
                        if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f
                            || viewport.y < 0f || viewport.y > 1f)
                            continue;

                        float3 direction = math.normalizesafe(
                            targetMetres - rayOrigin, new float3(0f, 0f, 1f));
                        float3 rayEnd = targetMetres + direction * 2f;
                        if (!treeDamage.TrySweepImpact(
                                rayOrigin, rayEnd, ShotSweepRadiusMetres,
                                out float3 candidateHit, out int sweptTreeIndex))
                            continue;

                        float distanceSquared = math.lengthsq(candidateHit - rayOrigin);
                        if (distanceSquared >= bestDistanceSquared) continue;
                        bestDistanceSquared = distanceSquared;
                        treeIndex = sweptTreeIndex;
                        hitMetres = candidateHit;
                        rayDirection = direction;
                        viewportX = viewport.x;
                        viewportY = viewport.y;
                    }
                }
            }

            return treeIndex >= 0;
        }
    }
}
