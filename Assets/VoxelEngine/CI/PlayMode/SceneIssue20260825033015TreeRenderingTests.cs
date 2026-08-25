using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Showcase;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Vegetation.Runtime;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    [NUnit.Framework.Explicit("Regression fixture for SceneIssue 20260825-033015-205-VoxelShowcase.")]
    public sealed class SceneIssue20260825033015TreeRenderingTests
    {
        private const float StartupTimeoutSeconds = 30f;
        private const float DamageTimeoutSeconds = 4f;
        private static readonly Vector3 CapturedPosition = new(
            35.10783767700195f, 25.95001983642578f, 68.67037200927735f);
        private static readonly Quaternion CapturedRotation = new(
            0.025435077026486398f, 0.7764801979064941f,
            0.03140655532479286f, -0.6288443207740784f);
        private const float CapturedFov = 70f;
        private const float CapturedAspect = 1364f / 836f;
        private const float MarkedX = 0.5381355881690979f;
        private const float MarkedY = 0.48502078652381899f;
        private const float PlayerBlastRadiusMetres = 1.2f;
        private const float TreeSweepRadiusMetres = 0.28f;

        [UnityTest]
        public IEnumerator CapturedPlayerShot_ReportsTreeCutAndStandingPresentation()
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

            ProceduralTreeRenderer renderer = null;
            while ((renderer == null || renderer.PresentationCount < TreeWorldRuntime.Instances.Count)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                renderer = FindRuntimeRenderer();
                yield return null;
            }
            Assert.That(renderer, Is.Not.Null);
            for (int frame = 0; frame < 8; frame++) yield return null;

            var cameraObject = new GameObject("CI SceneIssue captured tree ray");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = CapturedFov;
            camera.aspect = CapturedAspect;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 16000f;
            cameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);

            Ray ray = camera.ViewportPointToRay(new Vector3(MarkedX, MarkedY, 0f));
            float3 from = ray.origin;
            float3 to = ray.origin + ray.direction * 250f;
            Assert.That(ProceduralTreeDamageService.TrySweepImpact(
                            from, to, TreeSweepRadiusMetres,
                            out float3 hitMetres, out int treeIndex),
                        Is.True,
                        "The saved SceneIssue camera/marked region no longer intersects a procedural tree.");
            Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(treeIndex, Is.LessThan(TreeWorldRuntime.Instances.Count));

            TreeInstance instance = TreeWorldRuntime.Instances[treeIndex];
            TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            var cutsBefore = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
            bool beganBatched = !renderer.TryGetDynamicPresentationRoot(treeIndex, out _);
            int releaseBefore = renderer.LastDamageBatchReleaseCount;

            Mesh baseline = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
            int barkBefore = (int)baseline.GetIndexCount(0) / 3;
            int leavesBefore = (int)baseline.GetIndexCount(1) / 3;

            ProceduralTreeDamageService.ApplyBlast(
                hitMetres, PlayerBlastRadiusMetres, (float3)ray.direction);

            float damageDeadline = Time.realtimeSinceStartup + DamageTimeoutSeconds;
            while (TreeWorldRuntime.RemovedBranches(treeIndex).Count <= cutsBefore.Count
                   && Time.realtimeSinceStartup < damageDeadline)
                yield return null;
            for (int frame = 0; frame < 4; frame++) yield return null;

            var cutsAfter = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
            Assert.That(cutsAfter.Count, Is.GreaterThan(cutsBefore.Count),
                        "The saved shot intersects a tree but creates no structural cut.");
            Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out Transform treeRoot), Is.True,
                        "The damaged saved tree did not leave the healthy batch for dynamic presentation.");
            Assert.That(renderer.LastDamageBatchReleaseCount, Is.GreaterThan(releaseBefore),
                        "The saved tree remained in the intact healthy batch after structural damage.");

            Transform lod0 = treeRoot.Find("LOD0");
            Assert.That(lod0, Is.Not.Null);
            Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(liveMesh, Is.Not.Null);
            int barkAfter = (int)liveMesh.GetIndexCount(0) / 3;
            int leavesAfter = (int)liveMesh.GetIndexCount(1) / 3;
            Assert.That(barkAfter, Is.LessThan(barkBefore));
            Assert.That(leavesAfter, Is.LessThan(leavesBefore));

            var newCuts = new List<int>();
            int trunkCuts = 0;
            foreach (int cut in cutsAfter)
            {
                if (cutsBefore.Contains(cut)) continue;
                newCuts.Add(cut);
                if ((uint)cut < (uint)skeleton.Branches.Count && skeleton.Branches[cut].Level == 0)
                    trunkCuts++;
            }

            TreeDamageState damage = TreeWorldRuntime.Damage[treeIndex];
            float localImpactY = hitMetres.y - instance.PositionMetres.y;
            Debug.Log(
                "SCENEISSUE 20260825-033015-205 " +
                $"treeIndex={treeIndex} species={instance.Species} beganBatched={beganBatched} " +
                $"treePosition={instance.PositionMetres} impact={hitMetres} localImpactY={localImpactY:F3} " +
                $"height={skeleton.Height:F3} newCuts=[{string.Join(",", newCuts)}] trunkCuts={trunkCuts} " +
                $"severed={damage.Severed} barkBefore={barkBefore} barkAfter={barkAfter} " +
                $"leavesBefore={leavesBefore} leavesAfter={leavesAfter} " +
                $"batchReleaseDelta={renderer.LastDamageBatchReleaseCount - releaseBefore}");

            Object.Destroy(baseline);
            Object.Destroy(cameraObject);
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer renderer in all)
                if (renderer != null && renderer.gameObject.scene.IsValid()) return renderer;
            return null;
        }
    }
}
