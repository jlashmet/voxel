using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Composition;
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
        private const float ShotSweepRadiusMetres = 0.12f;
        private const float ShotBlastRadiusMetres = 1.2f;
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

                Assert.That(TryFindVisibleLowerTrunk(camera, out int collisionTreeIndex,
                                                     out float3 collisionMidpoint), Is.True,
                    "No lower trunk from the authored tree population is visible from the saved camera pose.");

                float3 playerHalfExtents = new(0.30f, 0.90f, 0.30f);
                bool blocksPlayer = VegetationComposition.TreeDamage.OverlapsWoodAabb(
                    collisionMidpoint - playerHalfExtents,
                    collisionMidpoint + playerHalfExtents);
                Assert.That(blocksPlayer, Is.True,
                    "A visible authored tree trunk at the saved pose must block a player-sized volume.");

                float3 rayOrigin = (float3)cameraObject.transform.position;
                float3 rayDirection = math.normalizesafe(collisionMidpoint - rayOrigin, new float3(0f, 0f, 1f));
                float3 rayEnd = collisionMidpoint + rayDirection * 2f;
                Assert.That(VegetationComposition.TreeDamage.TrySweepImpact(
                                rayOrigin, rayEnd, ShotSweepRadiusMetres,
                                out float3 hitMetres, out int hitTreeIndex),
                            Is.True,
                            "A shot through the saved camera toward a visible lower trunk did not hit semantic tree geometry.");

                int removedBefore = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
                VegetationComposition.TreeDamage.ApplyBlast(
                    hitMetres, ShotBlastRadiusMetres, rayDirection);
                int removedAfter = TreeWorldRuntime.RemovedBranches(hitTreeIndex).Count;
                Assert.That(removedAfter, Is.GreaterThan(removedBefore),
                    "A shot from the saved view hit a tree but did not remove any semantic branch geometry.");

                TreeDamageState damage = TreeWorldRuntime.Damage[hitTreeIndex];
                string outputDirectory = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    "Artifacts", "SingleTest", "SceneIssue20260825-033053-588");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllText(Path.Combine(outputDirectory, "verification-metrics.txt"),
                    $"capturedPosition={CapturedPosition}\n" +
                    $"capturedFov={CapturedFov:F1}\n" +
                    $"visibleCollisionTreeIndex={collisionTreeIndex}\n" +
                    $"collisionMidpoint={collisionMidpoint}\n" +
                    $"blocksPlayer={blocksPlayer}\n" +
                    $"shotTreeIndex={hitTreeIndex}\n" +
                    $"hitMetres={hitMetres}\n" +
                    $"removedBranchesBefore={removedBefore}\n" +
                    $"removedBranchesAfter={removedAfter}\n" +
                    $"severed={damage.Severed}\n");

                Debug.Log(
                    "SCENEISSUE 20260825-033053-588 " +
                    $"collisionTree={collisionTreeIndex} blocksPlayer={blocksPlayer} " +
                    $"shotTree={hitTreeIndex} hit={hitMetres} " +
                    $"removed={removedBefore}->{removedAfter} severed={damage.Severed}");
            }
            finally
            {
                Object.Destroy(cameraObject);
                TreeWorldRuntime.Clear();
            }
        }

        private static bool TryFindVisibleLowerTrunk(
            Camera camera, out int treeIndex, out float3 midpoint)
        {
            treeIndex = -1;
            midpoint = default;
            float bestDistanceSquared = float.MaxValue;
            IReadOnlyList<TreeInstance> instances = TreeWorldRuntime.Instances;

            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                TreeBranchSegment lowerTrunk = skeleton.Branches
                    .Where(branch => branch.Level == 0)
                    .OrderBy(branch => (branch.Start.y + branch.End.y) * 0.5f)
                    .First();

                float3 candidate = instance.PositionMetres + (lowerTrunk.Start + lowerTrunk.End) * 0.5f;
                Vector3 viewport = camera.WorldToViewportPoint((Vector3)candidate);
                if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f
                    || viewport.y < 0f || viewport.y > 1f)
                    continue;

                float distanceSquared = math.lengthsq(candidate - (float3)camera.transform.position);
                if (distanceSquared >= bestDistanceSquared) continue;
                bestDistanceSquared = distanceSquared;
                treeIndex = i;
                midpoint = candidate;
            }

            return treeIndex >= 0;
        }
    }
}
