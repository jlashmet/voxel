using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Runtime;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Vegetation;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// End-to-end semantic destruction proof without Showcase. The healthy singleton begins as a
    /// data-only batch entry; first branch damage must lazily materialize one dynamic tree, detach
    /// the branch, and a lower-trunk cut must leave an upright stump plus a falling crown.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter TreeDestructionVisualTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class TreeDestructionVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;

        [UnityTest]
        public IEnumerator SemanticTree_BranchDetachesAndTrunkLeavesFallingCrown()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "TreeDestruction");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            GameObject groundObject = null;
            Material groundMaterial = null;
            RenderTexture target = null;
            Texture2D capture = null;
            Mesh baselineMesh = null;

            try
            {
                ProceduralTreeRenderer renderer = null;
                for (int frame = 0; frame < 60; frame++)
                {
                    renderer = FindRuntimeRenderer();
                    if (renderer != null) break;
                    yield return null;
                }
                Assert.That(renderer, Is.Not.Null);

                var instance = new TreeInstance
                {
                    PositionMetres = float3.zero,
                    Species = TreeSpecies.Oak,
                    Seed = 0x00C0FFEEu,
                    Scale = 1f,
                };
                TreeWorldState.Replace(new[] { instance });

                for (int frame = 0;
                     frame < 60 && (renderer.PresentationCount != 1
                                    || renderer.BatchCount != 1
                                    || renderer.BatchedTreeCount != 1);
                     frame++)
                    yield return null;
                Assert.That(renderer.PresentationCount, Is.EqualTo(1));
                Assert.That(renderer.BatchCount, Is.EqualTo(1));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(1));
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(0),
                            "Healthy singleton should begin batch-only.");
                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out _), Is.False);

                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                baselineMesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
                int barkBefore = (int)baselineMesh.GetIndexCount(0) / 3;
                int leavesBefore = (int)baselineMesh.GetIndexCount(1) / 3;
                Assert.That(barkBefore, Is.GreaterThan(0));
                Assert.That(leavesBefore, Is.GreaterThan(0));

                Assert.That(renderer.TryGetTreeBounds(0, out Bounds bounds), Is.True);
                groundObject = GameObject.CreatePrimitive(PrimitiveType.Plane);
                groundObject.name = "CI Tree Destruction Ground";
                groundObject.transform.position = new Vector3(0f, -0.025f, 0f);
                groundObject.transform.localScale = Vector3.one * 5f;
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
                Capture(camera, target, ref capture, Path.Combine(outputDirectory, "01-before.png"));

                int branchIndex = SelectLeafBearingUpperBranch(skeleton);
                Assert.That(branchIndex, Is.GreaterThanOrEqualTo(0));
                TreeBranchSegment branch = skeleton.Branches[branchIndex];
                float3 branchMid = (branch.Start + branch.End) * 0.5f;
                float3 branchSweep = PerpendicularSweepDirection(branch.End - branch.Start);
                float half = math.max(0.7f, math.max(branch.RadiusStart, branch.RadiusEnd) * 4f + 0.35f);
                float3 branchFrom = branchMid - branchSweep * half;
                float3 branchTo = branchMid + branchSweep * half;

                int detachedBeforeBranch = CountDetachedBodies();
                bool branchCollision = ProceduralTreeDamageService.TrySweepImpact(
                    branchFrom, branchTo, 0.12f, out float3 branchHit, out int branchTreeIndex);
                Assert.That(branchCollision, Is.True);
                Assert.That(branchTreeIndex, Is.EqualTo(0));
                ProceduralTreeDamageService.ApplyBlast(branchHit, 0.20f, branchSweep);

                float branchDeadline = Time.realtimeSinceStartup + 2f;
                while ((!renderer.TryGetDynamicPresentationRoot(0, out _)
                        || TreeWorldState.RemovedBranches(0).Count == 0)
                       && Time.realtimeSinceStartup < branchDeadline)
                    yield return null;

                Assert.That(renderer.TryGetDynamicPresentationRoot(0, out Transform treeRoot), Is.True,
                            "First damage did not lazily materialize the singleton tree.");
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(1));
                Assert.That(renderer.BatchedTreeCount, Is.EqualTo(0));
                Transform lod0 = treeRoot.Find("LOD0");
                Assert.That(lod0, Is.Not.Null);
                Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(liveMesh, Is.Not.Null);

                for (int frame = 0; frame < 8; frame++) yield return null;
                int barkAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
                int leavesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
                int detachedAfterBranch = CountDetachedBodies();
                Assert.That(TreeWorldState.RemovedBranches(0).Count, Is.GreaterThan(0));
                Assert.That(TreeWorldState.Damage[0].Severed, Is.False);
                Assert.That(barkAfterBranch, Is.LessThan(barkBefore));
                Assert.That(leavesAfterBranch, Is.LessThan(leavesBefore));
                Assert.That(detachedAfterBranch, Is.GreaterThan(detachedBeforeBranch),
                            "Branch cut changed the tree but spawned no detached limb presentation.");
                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "02-after-branch-hit.png"));

                int trunkIndex = SelectLowerTrunkBranch(skeleton);
                Assert.That(trunkIndex, Is.GreaterThanOrEqualTo(0));
                TreeBranchSegment trunk = skeleton.Branches[trunkIndex];
                float3 trunkMid = (trunk.Start + trunk.End) * 0.5f;
                float3 trunkSweep = PerpendicularSweepDirection(trunk.End - trunk.Start);
                float trunkHalf = math.max(0.9f,
                    math.max(trunk.RadiusStart, trunk.RadiusEnd) * 5f + 0.45f);
                float3 trunkFrom = trunkMid - trunkSweep * trunkHalf;
                float3 trunkTo = trunkMid + trunkSweep * trunkHalf;

                int detachedBeforeTrunk = CountDetachedBodies();
                bool trunkCollision = ProceduralTreeDamageService.TrySweepImpact(
                    trunkFrom, trunkTo, 0.12f, out float3 trunkHit, out int trunkTreeIndex);
                Assert.That(trunkCollision, Is.True);
                Assert.That(trunkTreeIndex, Is.EqualTo(0));
                ProceduralTreeDamageService.ApplyBlast(trunkHit, 0.20f, trunkSweep);

                float deadline = Time.realtimeSinceStartup + 2f;
                while ((!TreeWorldState.Damage[0].Severed
                        || CountDetachedBodies() <= detachedBeforeTrunk)
                       && Time.realtimeSinceStartup < deadline)
                    yield return null;

                Assert.That(TreeWorldState.Damage[0].Severed, Is.True);
                Assert.That(CountDetachedBodies(), Is.GreaterThan(detachedBeforeTrunk),
                            "Trunk sever spawned no detached crown presentation.");
                Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity),
                            Is.LessThan(0.1f), "Standing semantic root should remain the rooted stump.");

                for (int frame = 0;
                     frame < 8 && (int)liveMesh.GetIndexCount(0) / 3 >= barkAfterBranch;
                     frame++)
                    yield return null;

                int barkAfterTrunk = (int)liveMesh.GetIndexCount(0) / 3;
                Assert.That(barkAfterTrunk, Is.LessThan(barkAfterBranch),
                            "Trunk sever did not remove the connected upper tree from the standing mesh.");
                Assert.That(CountBreakCaps(), Is.GreaterThan(0),
                            "Trunk sever emitted no visual splinter/break cap.");

                Rigidbody crown = FindLargestDetachedBody();
                Assert.That(crown, Is.Not.Null);
                Vector3 crownStart = crown.position;
                yield return new WaitForSeconds(0.55f);
                float crownTravel = Vector3.Distance(crownStart, crown.position);
                Assert.That(crownTravel + crown.linearVelocity.magnitude * 0.1f, Is.GreaterThan(0.10f),
                            "Detached crown did not visibly move after the trunk impact.");

                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "03-after-trunk-hit.png"));

                string metadata =
                    $"registryInstances={TreeWorldState.Instances.Count}\n" +
                    $"semanticPresentations={renderer.PresentationCount}\n" +
                    $"beganBatched=True\n" +
                    $"dynamicAfterDamage={renderer.DynamicPresentationCount}\n" +
                    $"branchTarget={branchIndex}\n" +
                    $"branchCollision={branchCollision}\n" +
                    $"barkTrianglesBefore={barkBefore}\n" +
                    $"barkTrianglesAfterBranch={barkAfterBranch}\n" +
                    $"leafTrianglesBefore={leavesBefore}\n" +
                    $"leafTrianglesAfterBranch={leavesAfterBranch}\n" +
                    $"detachedAfterBranch={detachedAfterBranch}\n" +
                    $"trunkTarget={trunkIndex}\n" +
                    $"trunkCollision={trunkCollision}\n" +
                    $"severedAfterTrunk={TreeWorldState.Damage[0].Severed}\n" +
                    $"barkTrianglesAfterTrunk={barkAfterTrunk}\n" +
                    $"breakCaps={CountBreakCaps()}\n" +
                    $"detachedAfterTrunk={CountDetachedBodies()}\n" +
                    $"crownTravelMetres={crownTravel:F3}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "tree-destruction.txt"), metadata);
                Debug.Log($"CI tree destruction capture written to {outputDirectory}\n{metadata}");
            }
            finally
            {
                TreeWorldState.Replace(System.Array.Empty<TreeInstance>());
                if (baselineMesh != null) Object.Destroy(baselineMesh);
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

        private static int SelectLeafBearingUpperBranch(TreeSkeletonSnapshot skeleton)
        {
            int bestBranch = -1;
            int bestLeaves = -1;
            var resolved = new HashSet<int>();
            for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
            {
                if (skeleton.Branches[branchIndex].Level <= 0) continue;
                TreeSkeletonTopology.ResolveRemovedBranches(
                    skeleton, new[] { branchIndex }, resolved);
                int leaves = 0;
                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    int parent = skeleton.LeafParents[leafIndex];
                    if (parent >= 0 && resolved.Contains(parent)) leaves++;
                }
                if (leaves <= bestLeaves) continue;
                bestLeaves = leaves;
                bestBranch = branchIndex;
            }
            return bestLeaves > 0 ? bestBranch : -1;
        }

        private static int SelectLowerTrunkBranch(TreeSkeletonSnapshot skeleton)
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

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer renderer in all)
                if (renderer != null && renderer.gameObject.scene.IsValid()) return renderer;
            return null;
        }

        private static int CountDetachedBodies() => FindDetachedBodies().Count;

        private static Rigidbody FindLargestDetachedBody()
        {
            Rigidbody best = null;
            float bestSize = -1f;
            foreach (Rigidbody body in FindDetachedBodies())
            {
                Renderer renderer = body.GetComponent<Renderer>();
                float size = renderer != null ? renderer.bounds.size.sqrMagnitude : 0f;
                if (size <= bestSize) continue;
                bestSize = size;
                best = body;
            }
            return best;
        }

        private static List<Rigidbody> FindDetachedBodies()
        {
            Rigidbody[] all = Resources.FindObjectsOfTypeAll<Rigidbody>();
            var result = new List<Rigidbody>();
            foreach (Rigidbody body in all)
            {
                if (body == null || body.gameObject == null || !body.gameObject.scene.IsValid()) continue;
                if (!body.name.StartsWith("Detached tree limb")) continue;
                result.Add(body);
            }
            return result;
        }

        private static int CountBreakCaps()
        {
            int count = 0;
            MeshRenderer[] all = Resources.FindObjectsOfTypeAll<MeshRenderer>();
            foreach (MeshRenderer renderer in all)
            {
                if (renderer == null || !renderer.gameObject.scene.IsValid()) continue;
                if (renderer.name.StartsWith("Tree break")) count++;
            }
            return count;
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
    }
}
