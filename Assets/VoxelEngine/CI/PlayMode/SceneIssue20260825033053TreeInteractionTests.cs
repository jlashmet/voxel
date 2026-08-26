using System.Collections;
using System.Collections.Generic;
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
        private const float ShotRangeMetres = 250f;
        private const float CapturedAspect = 1364f / 836f;
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

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while ((!ShowcaseTreePopulation.Completed || TreeWorldRuntime.Instances.Count == 0)
                   && Time.realtimeSinceStartup < startupDeadline)
                yield return null;

            Assert.That(ShowcaseTreePopulation.Completed, Is.True,
                "The captured VoxelShowcase tree population did not complete.");
            Assert.That(TreeWorldRuntime.Instances.Count, Is.GreaterThan(0),
                "The captured VoxelShowcase scene published no semantic trees.");

            var treeDamage = new TreeDamageService();
            GameObject cameraObject = new("SceneIssue 033053 captured camera");
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = CapturedFov;
                camera.aspect = CapturedAspect;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 16000f;
                cameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);

                Assert.That(TryFindCapturedViewTree(
                                camera, treeDamage,
                                out int hitTreeIndex, out float3 hitMetres,
                                out float3 rayDirection, out float viewportX, out float viewportY),
                            Is.True,
                            "No semantic tree geometry is intersected anywhere in the saved whole-frame camera view.");

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
                    "The authored tree intersected from the saved view must block a player-sized volume at its lower trunk.");

                int removedBefore = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
                treeDamage.ApplyBlast(hitMetres, ShotBlastRadiusMetres, rayDirection);
                int removedAfter = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
                Assert.That(removedAfter, Is.GreaterThan(removedBefore),
                    "A shot from the saved view hit semantic tree geometry but did not remove any branch geometry.");

                TreeDamageState damage = TreeWorldRuntime.Damage[hitTreeIndex];
                string outputDirectory = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    "Artifacts", "SingleTest", "SceneIssue20260825-033053-588");
                Directory.CreateDirectory(outputDirectory);
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
            }
            finally
            {
                Object.Destroy(cameraObject);
                TreeWorldRuntime.Clear();
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

            const int columns = 19;
            const int rows = 13;
            for (int row = 0; row < rows; row++)
            {
                float y = math.lerp(0.05f, 0.95f, row / (float)(rows - 1));
                for (int column = 0; column < columns; column++)
                {
                    float x = math.lerp(0.05f, 0.95f, column / (float)(columns - 1));
                    Ray ray = camera.ViewportPointToRay(new Vector3(x, y, 0f));
                    float3 from = (float3)ray.origin;
                    float3 direction = (float3)ray.direction;
                    float3 to = from + direction * ShotRangeMetres;
                    if (!treeDamage.TrySweepImpact(
                            from, to, ShotSweepRadiusMetres,
                            out float3 candidateHit, out int candidateTreeIndex))
                        continue;

                    float distanceSquared = math.lengthsq(candidateHit - from);
                    if (distanceSquared >= bestDistanceSquared) continue;
                    bestDistanceSquared = distanceSquared;
                    treeIndex = candidateTreeIndex;
                    hitMetres = candidateHit;
                    rayDirection = direction;
                    viewportX = x;
                    viewportY = y;
                }
            }

            return treeIndex >= 0;
        }
    }
}
