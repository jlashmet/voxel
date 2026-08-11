using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Showcase;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Full Showcase integration proof for semantic vegetation destruction. Unlike the isolated
    /// destruction test, this launches the production tornado projectile through VoxelShowcase,
    /// so collision arbitration, semantic-tree dispatch, branch damage and the live renderer all
    /// participate exactly as they do during gameplay.
    /// </summary>
    public sealed class ShowcaseTreeDestructionVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float StartupTimeoutSeconds = 30f;
        private const float ImpactTimeoutSeconds = 4f;

        [UnityTest]
        public IEnumerator ShowcaseTornado_BreaksBranchAndSeversTreeVisibly()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "ShowcaseTreeDestruction");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D capture = null;

            AsyncOperation load = SceneManager.LoadSceneAsync("VoxelShowcase", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while ((!ShowcaseTreePopulation.Completed || TreeWorldState.Instances.Count == 0)
                   && Time.realtimeSinceStartup < startupDeadline)
                yield return null;

            Assert.That(ShowcaseTreePopulation.Completed, Is.True,
                        "Semantic Showcase tree population never completed.");
            Assert.That(TreeWorldState.Instances.Count, Is.GreaterThan(0));

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.That(showcase, Is.Not.Null, "VoxelShowcase driver was not present in the scene.");

            List<ProceduralTreeRenderer> renderers = FindRuntimeRenderers();
            while ((renderers.Count != 1
                    || renderers[0].transform.childCount < TreeWorldState.Instances.Count)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                yield return null;
                renderers = FindRuntimeRenderers();
            }

            Assert.That(renderers.Count, Is.EqualTo(1));
            ProceduralTreeRenderer renderer = renderers[0];
            Assert.That(renderer.transform.childCount,
                        Is.EqualTo(TreeWorldState.Instances.Count));

            for (int frame = 0; frame < 30; frame++) yield return null;

            int treeIndex = SelectActiveTree(renderer);
            Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0),
                        "No active semantic Showcase tree was available for the tornado test.");

            TreeInstance instance = TreeWorldState.Instances[treeIndex];
            Transform treeRoot = renderer.transform.GetChild(treeIndex);
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f));
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);

            Transform lod0 = treeRoot.Find("LOD0");
            Assert.That(lod0, Is.Not.Null);
            MeshFilter lod0Filter = lod0.GetComponent<MeshFilter>();
            Assert.That(lod0Filter, Is.Not.Null);
            Mesh liveMesh = lod0Filter.sharedMesh;
            Assert.That(liveMesh, Is.Not.Null);

            int barkTrianglesBefore = (int)liveMesh.GetIndexCount(0) / 3;
            int leafTrianglesBefore = (int)liveMesh.GetIndexCount(1) / 3;
            int cutsBefore = TreeWorldState.RemovedBranches(treeIndex).Count;

            Bounds bounds = CalculateBounds(treeRoot);
            cameraObject = new GameObject("CI Showcase Tornado Tree Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 500f;
            camera.allowHDR = false;
            camera.allowMSAA = true;

            Vector3 focus = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 2f);
            Vector3 viewDirection = new Vector3(0.82f, 0.17f, -1f).normalized;
            cameraObject.transform.position = focus + viewDirection * (radius * 3.1f);
            cameraObject.transform.LookAt(focus + Vector3.up * (bounds.extents.y * 0.04f));

            target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
            {
                name = "CI Showcase Tornado Tree Capture",
                antiAliasing = 4,
            };
            target.Create();
            camera.targetTexture = target;

            yield return null;
            yield return null;
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "01-showcase-before.png"));

            ProceduralTreeSkeleton skeleton =
                ProceduralTreeSkeletonBuilder.Generate(in instance);
            int branchIndex = SelectLeafBearingUpperBranch(skeleton);
            Assert.That(branchIndex, Is.GreaterThanOrEqualTo(0));

            TreeBranchSegment branch = skeleton.Branches[branchIndex];
            float3 branchTarget = instance.PositionMetres + (branch.Start + branch.End) * 0.5f;
            float3 branchDirection = PerpendicularSweepDirection(branch.End - branch.Start);
            float branchOffset = math.max(1.1f,
                math.max(branch.RadiusStart, branch.RadiusEnd) * 5f + 0.55f);
            float3 branchOrigin = branchTarget - branchDirection * branchOffset;

            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(0));
            showcase.LaunchTornado((Vector3)branchOrigin, (Vector3)branchDirection, 2);
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(1));

            float branchDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while (TreeWorldState.RemovedBranches(treeIndex).Count <= cutsBefore
                   && Time.realtimeSinceStartup < branchDeadline)
                yield return null;

            int cutsAfterBranch = TreeWorldState.RemovedBranches(treeIndex).Count;
            Assert.That(cutsAfterBranch, Is.GreaterThan(cutsBefore),
                        "The real Showcase tornado did not record a semantic branch cut.");

            for (int frame = 0; frame < 5; frame++) yield return null;

            int barkTrianglesAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
            int leafTrianglesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False,
                        "Upper branch tornado incorrectly severed the trunk.");
            Assert.That(barkTrianglesAfterBranch, Is.LessThan(barkTrianglesBefore),
                        "The real Showcase tornado did not visibly remove bark triangles.");
            Assert.That(leafTrianglesAfterBranch, Is.LessThan(leafTrianglesBefore),
                        "The real Showcase tornado did not visibly remove attached foliage.");

            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "02-showcase-after-branch.png"));

            int trunkIndex = SelectLowerTrunkBranch(skeleton);
            Assert.That(trunkIndex, Is.GreaterThanOrEqualTo(0));
            TreeBranchSegment trunk = skeleton.Branches[trunkIndex];
            float3 trunkTarget = instance.PositionMetres + (trunk.Start + trunk.End) * 0.5f;
            float3 trunkDirection = PerpendicularSweepDirection(trunk.End - trunk.Start);
            float trunkOffset = math.max(1.2f,
                math.max(trunk.RadiusStart, trunk.RadiusEnd) * 6f + 0.65f);
            float3 trunkOrigin = trunkTarget - trunkDirection * trunkOffset;

            // The branch projectile should already be consumed. If its impact happened on the
            // same frame as this assertion, allow one extra frame for VoxelShowcase to remove it.
            if (showcase.ActiveTornadoCount != 0) yield return null;
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(0));

            showcase.LaunchTornado((Vector3)trunkOrigin, (Vector3)trunkDirection, 2);
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(1));

            float trunkDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while (!TreeWorldState.Damage[treeIndex].Severed
                   && Time.realtimeSinceStartup < trunkDeadline)
                yield return null;

            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.True,
                        "The real Showcase tornado did not sever the lower trunk.");

            yield return new WaitForSeconds(0.55f);
            float fallAngle = Quaternion.Angle(treeRoot.localRotation, Quaternion.identity);
            Assert.That(fallAngle, Is.GreaterThan(10f),
                        "The tree did not visibly begin falling after a real Showcase tornado hit.");
            Assert.That(treeRoot.gameObject.activeSelf, Is.True);

            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "03-showcase-after-trunk.png"));

            string metadata =
                $"treeIndex={treeIndex}\n" +
                $"species={instance.Species}\n" +
                $"presentationRoots={renderer.transform.childCount}\n" +
                $"cutsBefore={cutsBefore}\n" +
                $"cutsAfterBranch={cutsAfterBranch}\n" +
                $"barkTrianglesBefore={barkTrianglesBefore}\n" +
                $"barkTrianglesAfterBranch={barkTrianglesAfterBranch}\n" +
                $"leafTrianglesBefore={leafTrianglesBefore}\n" +
                $"leafTrianglesAfterBranch={leafTrianglesAfterBranch}\n" +
                $"severedAfterTrunk={TreeWorldState.Damage[treeIndex].Severed}\n" +
                $"fallAngleDegrees={fallAngle:F2}\n" +
                $"activeTornadoesAtEnd={showcase.ActiveTornadoCount}\n";
            File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree-destruction.txt"), metadata);
            Debug.Log($"CI Showcase tornado tree destruction written to {outputDirectory}\n{metadata}");

            Assert.That(File.Exists(Path.Combine(outputDirectory, "01-showcase-before.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputDirectory, "02-showcase-after-branch.png")), Is.True);
            Assert.That(File.Exists(Path.Combine(outputDirectory, "03-showcase-after-trunk.png")), Is.True);

            if (capture != null) Object.Destroy(capture);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
            if (cameraObject != null) Object.Destroy(cameraObject);
        }

        private static int SelectActiveTree(ProceduralTreeRenderer renderer)
        {
            int count = math.min(renderer.transform.childCount, TreeWorldState.Instances.Count);
            for (int i = 0; i < count; i++)
            {
                if (!renderer.transform.GetChild(i).gameObject.activeSelf) continue;
                if (i < TreeWorldState.Damage.Count
                    && TreeWorldState.Damage[i].Severed)
                    continue;
                return i;
            }
            return -1;
        }

        private static int SelectLeafBearingUpperBranch(
            ProceduralTreeSkeleton skeleton)
        {
            int bestBranch = -1;
            int bestLeaves = -1;
            var resolved = new HashSet<int>();
            for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
            {
                if (skeleton.Branches[branchIndex].Level <= 0) continue;
                ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                    skeleton, new[] { branchIndex }, resolved);
                int leaves = 0;
                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    int parent = skeleton.LeafParents != null
                              && leafIndex < skeleton.LeafParents.Length
                        ? skeleton.LeafParents[leafIndex] : -1;
                    if (parent >= 0 && resolved.Contains(parent)) leaves++;
                }
                if (leaves <= bestLeaves) continue;
                bestLeaves = leaves;
                bestBranch = branchIndex;
            }
            return bestLeaves > 0 ? bestBranch : -1;
        }

        private static int SelectLowerTrunkBranch(
            ProceduralTreeSkeleton skeleton)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;
            float targetY = skeleton.Height * 0.24f;
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                if (branch.Level != 0) continue;
                float midpointY = (branch.Start.y + branch.End.y) * 0.5f;
                if (midpointY >= skeleton.Height * 0.45f) continue;
                float distance = math.abs(midpointY - targetY);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = i;
            }
            return best;
        }

        private static float3 PerpendicularSweepDirection(float3 tangent)
        {
            tangent = math.normalizesafe(tangent, new float3(0f, 1f, 0f));
            float3 direction = math.cross(tangent, new float3(0f, 1f, 0f));
            if (math.lengthsq(direction) < 1e-5f)
                direction = math.cross(tangent, new float3(1f, 0f, 0f));
            return math.normalizesafe(direction, new float3(1f, 0f, 0f));
        }

        private static Bounds CalculateBounds(Transform root)
        {
            MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                if (!hasBounds)
                {
                    bounds = meshRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(meshRenderer.bounds);
                }
            }
            return bounds;
        }

        private static void Capture(Camera camera, RenderTexture target,
                                    ref Texture2D capture, string path)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                if (capture == null)
                    capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                capture.Apply(false, false);
                File.WriteAllBytes(path, capture.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static List<ProceduralTreeRenderer> FindRuntimeRenderers()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            var result = new List<ProceduralTreeRenderer>(all.Length);
            foreach (ProceduralTreeRenderer renderer in all)
            {
                if (renderer == null || renderer.gameObject == null) continue;
                if (!renderer.gameObject.scene.IsValid()) continue;
                result.Add(renderer);
            }
            return result;
        }
    }
}
