using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Rendering.Vegetation;
using VoxelEngine.Showcase;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// End-to-end semantic tree destruction proof. The test publishes one deterministic tree,
    /// lets the production ProceduralTreeRenderer build it, collides against the same procedural
    /// skeleton used by tornadoes, and verifies that the live render mesh actually loses bark and
    /// foliage indices. A second semantic hit severs the lower trunk and must visibly rotate the
    /// rendered tree into its falling state.
    /// </summary>
    public sealed class TreeDestructionVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float VoxelSize = 0.1f;

        [UnityTest]
        public IEnumerator SemanticTree_BranchBreaksAndTrunkFallsVisibly()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "TreeDestruction");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            GameObject groundObject = null;
            Material groundMaterial = null;
            RenderTexture target = null;
            Texture2D capture = null;

            try
            {
                List<ProceduralTreeRenderer> renderers = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    renderers = FindRuntimeRenderers();
                    if (renderers.Count == 1) break;
                    yield return null;
                }

                Assert.That(renderers, Is.Not.Null);
                Assert.That(renderers.Count, Is.EqualTo(1),
                            "Production bootstrap must create exactly one ProceduralTreeRenderer.");
                ProceduralTreeRenderer renderer = renderers[0];

                var instance = new TreeInstance
                {
                    PositionMetres = float3.zero,
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };
                TreeWorldState.Replace(new[] { instance });

                for (int frame = 0; frame < 60 && renderer.transform.childCount == 0; frame++)
                    yield return null;
                yield return null;
                yield return null;

                renderers = FindRuntimeRenderers();
                Assert.That(renderers.Count, Is.EqualTo(1));
                renderer = renderers[0];
                Assert.That(renderer.transform.childCount, Is.EqualTo(1),
                            "One semantic tree must have exactly one presentation root.");

                Transform treeRoot = renderer.transform.GetChild(0);
                Assert.That(treeRoot.gameObject.activeSelf, Is.True);
                Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity),
                            Is.LessThan(0.01f));

                Transform lod0 = treeRoot.Find("LOD0");
                Assert.That(lod0, Is.Not.Null, "LOD0 presentation was not created.");
                MeshFilter lod0Filter = lod0.GetComponent<MeshFilter>();
                Assert.That(lod0Filter, Is.Not.Null);
                Assert.That(lod0Filter.sharedMesh, Is.Not.Null);

                Mesh liveMesh = lod0Filter.sharedMesh;
                int barkTrianglesBefore = (int)liveMesh.GetIndexCount(0) / 3;
                int leafTrianglesBefore = (int)liveMesh.GetIndexCount(1) / 3;
                Assert.That(barkTrianglesBefore, Is.GreaterThan(0));
                Assert.That(leafTrianglesBefore, Is.GreaterThan(0));

                Bounds bounds = CalculateBounds(treeRoot);
                Assert.That(bounds.size.sqrMagnitude, Is.GreaterThan(0f));

                groundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                groundObject.name = "CI Tree Destruction Ground";
                groundObject.transform.position = new Vector3(0f, -0.025f, 0f);
                groundObject.transform.localScale = Vector3.one * 4f;
                Shader groundShader = Shader.Find("Universal Render Pipeline/Unlit");
                if (groundShader != null)
                {
                    groundMaterial = new Material(groundShader) { name = "CI Tree Destruction Ground" };
                    groundMaterial.SetColor("_BaseColor", new Color(0.16f, 0.18f, 0.20f, 1f));
                    groundObject.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
                }

                cameraObject = new GameObject("CI Tree Destruction Camera");
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.52f, 0.60f, 0.70f, 1f);
                camera.fieldOfView = 34f;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 220f;
                camera.allowHDR = false;
                camera.allowMSAA = true;

                Vector3 focus = bounds.center;
                float radius = Mathf.Max(bounds.extents.magnitude, 2f);
                Vector3 viewDirection = new Vector3(0.82f, 0.18f, -1f).normalized;
                cameraObject.transform.position = focus + viewDirection * (radius * 3.20f);
                cameraObject.transform.LookAt(focus + Vector3.up * (bounds.extents.y * 0.04f));

                target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32)
                {
                    name = "CI Tree Destruction Capture",
                    antiAliasing = 4,
                };
                target.Create();
                camera.targetTexture = target;

                yield return null;
                yield return null;
                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "01-before.png"));

                ProceduralTreeSkeleton skeleton =
                    ProceduralTreeSkeletonBuilder.Generate(in instance);
                int branchIndex = SelectLeafBearingUpperBranch(skeleton);
                Assert.That(branchIndex, Is.GreaterThanOrEqualTo(0),
                            "Deterministic Oak did not contain a removable leaf-bearing upper branch.");

                TreeBranchSegment branch = skeleton.Branches[branchIndex];
                float3 branchMidpoint = instance.PositionMetres + (branch.Start + branch.End) * 0.5f;
                float3 branchSweep = PerpendicularSweepDirection(branch.End - branch.Start);
                float branchSweepHalfLength = math.max(0.7f,
                    math.max(branch.RadiusStart, branch.RadiusEnd) * 4f + 0.35f);
                float3 branchFrom = branchMidpoint - branchSweep * branchSweepHalfLength;
                float3 branchTo = branchMidpoint + branchSweep * branchSweepHalfLength;

                bool branchCollision = ProceduralTreeDamageService.TrySweepImpact(
                    branchFrom, branchTo, 0.12f, out float3 branchHit, out int branchTreeIndex);
                Assert.That(branchCollision, Is.True,
                            "Semantic sweep failed to collide with the rendered upper branch.");
                Assert.That(branchTreeIndex, Is.EqualTo(0));

                ProceduralTreeDamageBridge.ApplyExplosion(
                    (int3)math.round(branchHit / VoxelSize), 2);

                for (int frame = 0; frame < 5; frame++) yield return null;

                int directCutsAfterBranch = TreeWorldState.RemovedBranches(0).Count;
                int barkTrianglesAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
                int leafTrianglesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
                bool severedAfterBranch = TreeWorldState.Damage[0].Severed;

                Assert.That(directCutsAfterBranch, Is.GreaterThan(0),
                            "Upper-branch impact recorded no semantic branch cuts.");
                Assert.That(severedAfterBranch, Is.False,
                            "An upper-branch impact must not sever the lower trunk.");
                Assert.That(barkTrianglesAfterBranch, Is.LessThan(barkTrianglesBefore),
                            "Semantic branch cut did not remove visible bark triangles.");
                Assert.That(leafTrianglesAfterBranch, Is.LessThan(leafTrianglesBefore),
                            "Semantic branch cut did not remove attached visible foliage triangles.");

                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "02-after-branch-hit.png"));

                int trunkIndex = SelectLowerTrunkBranch(skeleton);
                Assert.That(trunkIndex, Is.GreaterThanOrEqualTo(0));
                TreeBranchSegment trunk = skeleton.Branches[trunkIndex];
                float3 trunkMidpoint = instance.PositionMetres + (trunk.Start + trunk.End) * 0.5f;
                float3 trunkSweep = PerpendicularSweepDirection(trunk.End - trunk.Start);
                float trunkSweepHalfLength = math.max(0.9f,
                    math.max(trunk.RadiusStart, trunk.RadiusEnd) * 5f + 0.45f);
                float3 trunkFrom = trunkMidpoint - trunkSweep * trunkSweepHalfLength;
                float3 trunkTo = trunkMidpoint + trunkSweep * trunkSweepHalfLength;

                bool trunkCollision = ProceduralTreeDamageService.TrySweepImpact(
                    trunkFrom, trunkTo, 0.12f, out float3 trunkHit, out int trunkTreeIndex);
                Assert.That(trunkCollision, Is.True,
                            "Semantic sweep failed to collide with the rendered lower trunk.");
                Assert.That(trunkTreeIndex, Is.EqualTo(0));

                ProceduralTreeDamageBridge.ApplyExplosion(
                    (int3)math.round(trunkHit / VoxelSize), 2);
                yield return null;
                yield return null;

                Assert.That(TreeWorldState.Damage[0].Severed, Is.True,
                            "Lower-trunk semantic impact did not mark the tree severed.");

                yield return new WaitForSeconds(0.55f);

                float fallAngle = Quaternion.Angle(treeRoot.localRotation, Quaternion.identity);
                Assert.That(fallAngle, Is.GreaterThan(10f),
                            "Severed tree did not visibly rotate into its falling animation.");
                Assert.That(treeRoot.gameObject.activeSelf, Is.True,
                            "Tree retired before the falling visual could be observed.");

                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "03-after-trunk-hit.png"));

                string metadata =
                    $"registryInstances={TreeWorldState.Instances.Count}\n" +
                    $"presentationRoots={renderer.transform.childCount}\n" +
                    $"branchTarget={branchIndex}\n" +
                    $"branchCollision={branchCollision}\n" +
                    $"branchHit={branchHit}\n" +
                    $"directCutsAfterBranch={directCutsAfterBranch}\n" +
                    $"barkTrianglesBefore={barkTrianglesBefore}\n" +
                    $"barkTrianglesAfterBranch={barkTrianglesAfterBranch}\n" +
                    $"leafTrianglesBefore={leafTrianglesBefore}\n" +
                    $"leafTrianglesAfterBranch={leafTrianglesAfterBranch}\n" +
                    $"severedAfterBranch={severedAfterBranch}\n" +
                    $"trunkTarget={trunkIndex}\n" +
                    $"trunkCollision={trunkCollision}\n" +
                    $"trunkHit={trunkHit}\n" +
                    $"severedAfterTrunk={TreeWorldState.Damage[0].Severed}\n" +
                    $"fallAngleDegrees={fallAngle:F2}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "tree-destruction.txt"), metadata);
                Debug.Log($"CI tree destruction capture written to {outputDirectory}\n{metadata}");

                Assert.That(File.Exists(Path.Combine(outputDirectory, "01-before.png")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "02-after-branch-hit.png")), Is.True);
                Assert.That(File.Exists(Path.Combine(outputDirectory, "03-after-trunk-hit.png")), Is.True);
            }
            finally
            {
                TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
                if (capture != null) Object.Destroy(capture);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                if (cameraObject != null) Object.Destroy(cameraObject);
                if (groundObject != null) Object.Destroy(groundObject);
                if (groundMaterial != null) Object.Destroy(groundMaterial);
            }
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
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (MeshRenderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
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
