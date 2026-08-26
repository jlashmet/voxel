using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private const int CaptureWidth = 682;
        private const int CaptureHeight = 418;
        private static readonly Vector3 CapturedPosition = new(
            35.10783767700195f, 25.95001983642578f, 68.67037200927735f);
        private static readonly Quaternion CapturedRotation = new(
            0.025435077026486398f, 0.7764801979064941f,
            0.03140655532479286f, -0.6288443207740784f);
        private const float CapturedFov = 70f;
        private const float CapturedAspect = 1364f / 836f;
        private const float MarkedX = 0.5381355881690979f;
        private const float MarkedY = 0.48502078652381899f;
        private const float MarkedRadius = 0.03643718734383583f;
        private const float PlayerBlastRadiusMetres = 1.2f;
        private const float TreeSweepRadiusMetres = 0.28f;

        [UnityTest]
        public IEnumerator CapturedPlayerShot_ClearsStandingTreeFromMarkedRegion()
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

            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D before = null;
            Texture2D beforeWithoutStandingTrees = null;
            Texture2D after = null;
            Texture2D afterWithoutStandingTrees = null;
            Mesh baseline = null;
            try
            {
                cameraObject = new GameObject("CI SceneIssue captured tree camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = CapturedFov;
                camera.aspect = CapturedAspect;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 16000f;
                cameraObject.transform.SetPositionAndRotation(CapturedPosition, CapturedRotation);

                target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI SceneIssue 033015 capture",
                    antiAliasing = 1,
                };
                target.Create();
                camera.targetTexture = target;

                Ray ray = camera.ViewportPointToRay(new Vector3(MarkedX, MarkedY, 0f));
                float3 from = ray.origin;
                float3 to = ray.origin + ray.direction * 250f;
                Assert.That(ProceduralTreeDamageService.TrySweepImpact(
                                from, to, TreeSweepRadiusMetres,
                                out float3 hitMetres, out int treeIndex),
                            Is.True,
                            "The saved SceneIssue camera/marked region no longer intersects a procedural tree.");

                TreeInstance instance = TreeWorldRuntime.Instances[treeIndex];
                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                var cutsBefore = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
                bool beganBatched = !renderer.TryGetDynamicPresentationRoot(treeIndex, out _);
                int releaseBefore = renderer.LastDamageBatchReleaseCount;
                baseline = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
                int barkBefore = (int)baseline.GetIndexCount(0) / 3;
                int leavesBefore = (int)baseline.GetIndexCount(1) / 3;

                before = Capture(camera, target);
                beforeWithoutStandingTrees = CaptureWithoutStandingTrees(camera, target, renderer);
                int standingPixelsBefore = CountChangedPixelsInMarkedCircle(before, beforeWithoutStandingTrees);
                Assert.That(standingPixelsBefore, Is.GreaterThan(32),
                            "The saved marked region does not visibly contain the target tree before damage.");

                ProceduralTreeDamageService.ApplyBlast(
                    hitMetres, PlayerBlastRadiusMetres, (float3)ray.direction);
                float damageDeadline = Time.realtimeSinceStartup + DamageTimeoutSeconds;
                while (TreeWorldRuntime.RemovedBranches(treeIndex).Count <= cutsBefore.Count
                       && Time.realtimeSinceStartup < damageDeadline)
                    yield return null;
                for (int frame = 0; frame < 4; frame++) yield return null;

                var cutsAfter = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
                Assert.That(cutsAfter.Count, Is.GreaterThan(cutsBefore.Count));
                Assert.That(renderer.LastDamageBatchReleaseCount, Is.GreaterThan(releaseBefore));

                var newCuts = new List<int>();
                int trunkCuts = 0;
                foreach (int cut in cutsAfter)
                {
                    if (cutsBefore.Contains(cut)) continue;
                    newCuts.Add(cut);
                    if ((uint)cut < (uint)skeleton.Branches.Count && skeleton.Branches[cut].Level == 0)
                        trunkCuts++;
                }
                Assert.That(trunkCuts, Is.GreaterThan(0),
                            "The saved shot no longer performs the structural trunk sever exercised by this capture.");

                bool targetStandingAfter = renderer.TryGetDynamicPresentationRoot(treeIndex, out Transform treeRoot);
                int barkAfter = 0;
                int leavesAfter = 0;
                if (targetStandingAfter)
                {
                    Transform lod0 = treeRoot.Find("LOD0");
                    Assert.That(lod0, Is.Not.Null);
                    Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(liveMesh, Is.Not.Null);
                    barkAfter = (int)liveMesh.GetIndexCount(0) / 3;
                    leavesAfter = (int)liveMesh.GetIndexCount(1) / 3;
                }
                Assert.That(barkAfter, Is.LessThan(barkBefore));
                Assert.That(leavesAfter, Is.LessThan(leavesBefore));

                after = Capture(camera, target);
                afterWithoutStandingTrees = CaptureWithoutStandingTrees(camera, target, renderer);
                int standingPixelsAfter = CountChangedPixelsInMarkedCircle(after, afterWithoutStandingTrees);

                string outputDirectory = Path.Combine(
                    Directory.GetParent(Application.dataPath)!.FullName,
                    "Artifacts", "SingleTest", "SceneIssue20260825-033015-205");
                Directory.CreateDirectory(outputDirectory);
                File.WriteAllBytes(Path.Combine(outputDirectory, "verification-after.png"), after.EncodeToPNG());
                File.WriteAllText(Path.Combine(outputDirectory, "verification-metrics.txt"),
                    $"standingPixelsBefore={standingPixelsBefore}\n" +
                    $"standingPixelsAfterAllProceduralTrees={standingPixelsAfter}\n" +
                    $"targetStandingPresentationAfter={targetStandingAfter}\n" +
                    $"markedRadius={MarkedRadius:F6}\n" +
                    $"barkBefore={barkBefore}\n" +
                    $"barkAfter={barkAfter}\n" +
                    $"leavesBefore={leavesBefore}\n" +
                    $"leavesAfter={leavesAfter}\n");

                Assert.That(targetStandingAfter, Is.False,
                            "The captured level-zero sever left the target tree's rooted dynamic presentation active.");
                Assert.That(standingPixelsAfter, Is.LessThan(standingPixelsBefore),
                            "The saved marked region did not lose any standing-tree contribution after the target sever.");

                TreeDamageState damage = TreeWorldRuntime.Damage[treeIndex];
                float localImpactY = hitMetres.y - instance.PositionMetres.y;
                Debug.Log(
                    "SCENEISSUE 20260825-033015-205 " +
                    $"treeIndex={treeIndex} species={instance.Species} beganBatched={beganBatched} " +
                    $"impact={hitMetres} localImpactY={localImpactY:F3} height={skeleton.Height:F3} " +
                    $"newCuts=[{string.Join(",", newCuts)}] trunkCuts={trunkCuts} severed={damage.Severed} " +
                    $"targetStandingAfter={targetStandingAfter} barkBefore={barkBefore} barkAfter={barkAfter} " +
                    $"leavesBefore={leavesBefore} leavesAfter={leavesAfter} " +
                    $"standingPixelsBefore={standingPixelsBefore} standingPixelsAfterAllTrees={standingPixelsAfter} " +
                    $"batchReleaseDelta={renderer.LastDamageBatchReleaseCount - releaseBefore}");
            }
            finally
            {
                if (cameraObject != null)
                {
                    Camera camera = cameraObject.GetComponent<Camera>();
                    if (camera != null) camera.targetTexture = null;
                }
                if (baseline != null) Object.Destroy(baseline);
                if (before != null) Object.Destroy(before);
                if (beforeWithoutStandingTrees != null) Object.Destroy(beforeWithoutStandingTrees);
                if (after != null) Object.Destroy(after);
                if (afterWithoutStandingTrees != null) Object.Destroy(afterWithoutStandingTrees);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                if (cameraObject != null) Object.Destroy(cameraObject);
            }
        }

        private static Texture2D Capture(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                var capture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false, false);
                capture.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0, false);
                capture.Apply(false, false);
                return capture;
            }
            finally { RenderTexture.active = previous; }
        }

        private static Texture2D CaptureWithoutStandingTrees(
            Camera camera, RenderTexture target, ProceduralTreeRenderer renderer)
        {
            MeshRenderer[] standingRenderers = renderer.GetComponentsInChildren<MeshRenderer>(true);
            var enabled = new bool[standingRenderers.Length];
            try
            {
                for (int i = 0; i < standingRenderers.Length; i++)
                {
                    enabled[i] = standingRenderers[i].enabled;
                    standingRenderers[i].enabled = false;
                }
                return Capture(camera, target);
            }
            finally
            {
                for (int i = 0; i < standingRenderers.Length; i++)
                    if (standingRenderers[i] != null) standingRenderers[i].enabled = enabled[i];
            }
        }

        private static int CountChangedPixelsInMarkedCircle(Texture2D withTrees, Texture2D withoutTrees)
        {
            Color32[] a = withTrees.GetPixels32();
            Color32[] b = withoutTrees.GetPixels32();
            Assert.That(a.Length, Is.EqualTo(b.Length));
            float centerX = MarkedX * CaptureWidth;
            float centerY = MarkedY * CaptureHeight;
            float radius = MarkedRadius * Mathf.Min(CaptureWidth, CaptureHeight);
            float radiusSq = radius * radius;
            int changed = 0;
            for (int y = 0; y < CaptureHeight; y++)
            for (int x = 0; x < CaptureWidth; x++)
            {
                float dx = x + 0.5f - centerX;
                float dy = y + 0.5f - centerY;
                if (dx * dx + dy * dy > radiusSq) continue;
                int i = y * CaptureWidth + x;
                int delta = Mathf.Max(Mathf.Abs(a[i].r - b[i].r),
                    Mathf.Max(Mathf.Abs(a[i].g - b[i].g), Mathf.Abs(a[i].b - b[i].b)));
                if (delta >= 8) changed++;
            }
            return changed;
        }

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            foreach (ProceduralTreeRenderer renderer in Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>())
                if (renderer != null && renderer.gameObject.scene.IsValid()) return renderer;
            return null;
        }
    }
}
