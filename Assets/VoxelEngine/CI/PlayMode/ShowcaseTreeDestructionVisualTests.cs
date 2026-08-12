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
    /// Full Showcase integration proof. Healthy trees begin data-only inside batches. A tornado must
    /// detach a meaningful connected limb and lazily materialize only that tree; a lower-trunk hit
    /// must leave a rooted stump and a crown that physically tips away from the cut.
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
            Mesh baselineMesh = null;

            AsyncOperation load = SceneManager.LoadSceneAsync("VoxelShowcase", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            while (!load.isDone) yield return null;

            float startupDeadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            while ((!ShowcaseTreePopulation.Completed || TreeWorldState.Instances.Count == 0)
                   && Time.realtimeSinceStartup < startupDeadline)
                yield return null;

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.That(showcase, Is.Not.Null);
            ProceduralTreeRenderer renderer = null;
            while ((renderer == null || renderer.PresentationCount < TreeWorldState.Instances.Count)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                renderer = FindRuntimeRenderer();
                yield return null;
            }
            Assert.That(renderer, Is.Not.Null);
            for (int frame = 0; frame < 20; frame++) yield return null;

            int treeIndex = SelectTreeForDestruction();
            Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0));
            TreeInstance instance = TreeWorldState.Instances[treeIndex];
            ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            int branchIndex = SelectStructuralUpperBranch(skeleton);
            int trunkIndex = SelectLowerTrunkBranch(skeleton);
            Assert.That(branchIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(trunkIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);

            bool beganBatched = !renderer.TryGetDynamicPresentationRoot(treeIndex, out _);
            int dynamicBefore = renderer.DynamicPresentationCount;

            baselineMesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
            int barkBefore = (int)baselineMesh.GetIndexCount(0) / 3;
            int leavesBefore = (int)baselineMesh.GetIndexCount(1) / 3;
            int cutsBefore = TreeWorldState.RemovedBranches(treeIndex).Count;
            Assert.That(renderer.TryGetTreeBounds(treeIndex, out Bounds bounds), Is.True);

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

            TreeBranchSegment branch = skeleton.Branches[branchIndex];
            float3 branchTarget = instance.PositionMetres + (branch.Start + branch.End) * 0.5f;
            float3 branchDirection = PerpendicularSweepDirection(branch.End - branch.Start);
            float branchOffset = math.max(1.1f,
                math.max(branch.RadiusStart, branch.RadiusEnd) * 5f + 0.55f);
            float3 branchOrigin = branchTarget - branchDirection * branchOffset;

            int detachedBeforeBranch = CountDetachedBodies();
            showcase.LaunchTornado((Vector3)branchOrigin, (Vector3)branchDirection, 2);
            float branchDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while ((TreeWorldState.RemovedBranches(treeIndex).Count <= cutsBefore
                    || CountDetachedBodies() <= detachedBeforeBranch
                    || !renderer.TryGetDynamicPresentationRoot(treeIndex, out _))
                   && Time.realtimeSinceStartup < branchDeadline)
                yield return null;

            int cutsAfterBranch = TreeWorldState.RemovedBranches(treeIndex).Count;
            int detachedAfterBranch = CountDetachedBodies();
            Assert.That(cutsAfterBranch, Is.GreaterThan(cutsBefore));
            Assert.That(detachedAfterBranch, Is.GreaterThan(detachedBeforeBranch));
            Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out Transform treeRoot), Is.True,
                        "First real damage did not lazily materialize the tree presentation.");
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f));
            if (beganBatched)
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(dynamicBefore + 1));

            Transform lod0 = treeRoot.Find("LOD0");
            Assert.That(lod0, Is.Not.Null);
            Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(liveMesh, Is.Not.Null);
            for (int frame = 0; frame < 5; frame++) yield return null;

            int barkAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
            int leavesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);
            Assert.That(barkAfterBranch, Is.LessThan(barkBefore - 48),
                        "Branch hit removed only twig-scale bark; destruction should detach a visible connected limb.");
            Assert.That(leavesAfterBranch, Is.LessThan(leavesBefore));
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "02-showcase-after-branch.png"));

            TreeBranchSegment trunk = skeleton.Branches[trunkIndex];
            float3 trunkTarget = instance.PositionMetres + (trunk.Start + trunk.End) * 0.5f;
            float3 trunkDirection = PerpendicularSweepDirection(trunk.End - trunk.Start);
            float trunkOffset = math.max(1.2f,
                math.max(trunk.RadiusStart, trunk.RadiusEnd) * 6f + 0.65f);
            float3 trunkOrigin = trunkTarget - trunkDirection * trunkOffset;

            while (showcase.ActiveTornadoCount != 0
                   && Time.realtimeSinceStartup < branchDeadline)
                yield return null;
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(0));

            int detachedBeforeTrunk = CountDetachedBodies();
            showcase.LaunchTornado((Vector3)trunkOrigin, (Vector3)trunkDirection, 2);
            float trunkDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while ((!TreeWorldState.Damage[treeIndex].Severed
                    || CountDetachedBodies() <= detachedBeforeTrunk)
                   && Time.realtimeSinceStartup < trunkDeadline)
                yield return null;

            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.True);
            Assert.That(CountDetachedBodies(), Is.GreaterThan(detachedBeforeTrunk));
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f),
                        "The rooted presentation must remain the stump, not rotate as a whole tree.");

            for (int frame = 0;
                 frame < 8 && (int)liveMesh.GetIndexCount(0) / 3 >= barkAfterBranch;
                 frame++)
                yield return null;

            int barkAfterTrunk = (int)liveMesh.GetIndexCount(0) / 3;
            Assert.That(barkAfterTrunk, Is.LessThan(barkAfterBranch));
            Assert.That(CountBreakCaps(), Is.GreaterThan(0));

            Rigidbody crown = FindLargestDetachedBody();
            Assert.That(crown, Is.Not.Null);
            Assert.That(crown.GetComponent<CapsuleCollider>(), Is.Not.Null,
                        "A severed crown should collide like a trunk, not like a giant canopy box.");
            Vector3 crownStart = crown.position;
            float startTilt = Vector3.Angle(crown.transform.up, Vector3.up);
            yield return new WaitForSeconds(0.55f);
            float crownTravel = Vector3.Distance(crownStart, crown.position);
            float crownTilt = Vector3.Angle(crown.transform.up, Vector3.up);
            Assert.That(crownTilt, Is.GreaterThan(startTilt + 6f),
                        "Severed crown translated but did not visibly topple away from the cut.");
            Assert.That(crownTravel + crown.linearVelocity.magnitude * 0.1f, Is.GreaterThan(0.10f));

            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "03-showcase-after-trunk.png"));

            string metadata =
                $"treeIndex={treeIndex}\n" +
                $"species={instance.Species}\n" +
                $"beganBatched={beganBatched}\n" +
                $"dynamicPresentationsBefore={dynamicBefore}\n" +
                $"dynamicPresentationsAfterBranch={renderer.DynamicPresentationCount}\n" +
                $"cutsBefore={cutsBefore}\n" +
                $"cutsAfterBranch={cutsAfterBranch}\n" +
                $"barkTrianglesBefore={barkBefore}\n" +
                $"barkTrianglesAfterBranch={barkAfterBranch}\n" +
                $"leafTrianglesBefore={leavesBefore}\n" +
                $"leafTrianglesAfterBranch={leavesAfterBranch}\n" +
                $"detachedAfterBranch={detachedAfterBranch}\n" +
                $"severedAfterTrunk={TreeWorldState.Damage[treeIndex].Severed}\n" +
                $"barkTrianglesAfterTrunk={barkAfterTrunk}\n" +
                $"detachedAfterTrunk={CountDetachedBodies()}\n" +
                $"breakCaps={CountBreakCaps()}\n" +
                $"crownTravelMetres={crownTravel:F3}\n" +
                $"crownTiltDegrees={crownTilt:F2}\n" +
                $"activeTornadoesAtEnd={showcase.ActiveTornadoCount}\n";
            File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree-destruction.txt"), metadata);
            Debug.Log($"CI Showcase tornado tree destruction written to {outputDirectory}\n{metadata}");

            if (baselineMesh != null) Object.Destroy(baselineMesh);
            if (capture != null) Object.Destroy(capture);
            if (target != null)
            {
                target.Release();
                Object.Destroy(target);
            }
            if (cameraObject != null) Object.Destroy(cameraObject);
        }

        private static int SelectTreeForDestruction()
        {
            int count = math.min(TreeWorldState.Instances.Count, TreeWorldState.Damage.Count);
            for (int i = 0; i < count; i++)
            {
                if (TreeWorldState.Damage[i].Severed) continue;
                TreeInstance instance = TreeWorldState.Instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                if (SelectStructuralUpperBranch(skeleton) < 0) continue;
                if (SelectLowerTrunkBranch(skeleton) < 0) continue;
                return i;
            }
            return -1;
        }

        private static int SelectStructuralUpperBranch(ProceduralTreeSkeleton skeleton)
        {
            int bestBranch = -1;
            int bestScore = -1;
            var resolved = new HashSet<int>();
            for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
            {
                if (skeleton.Branches[branchIndex].Level != 1) continue;
                ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                    skeleton, new[] { branchIndex }, resolved);
                int leaves = 0;
                for (int leafIndex = 0; leafIndex < skeleton.Leaves.Count; leafIndex++)
                {
                    int parent = skeleton.LeafParents[leafIndex];
                    if (parent >= 0 && resolved.Contains(parent)) leaves++;
                }
                int score = resolved.Count * 4 + leaves;
                if (score <= bestScore) continue;
                bestScore = score;
                bestBranch = branchIndex;
            }
            return bestBranch;
        }

        private static int SelectLowerTrunkBranch(ProceduralTreeSkeleton skeleton)
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

        private static List<Rigidbody> FindDetachedBodies()
        {
            Rigidbody[] all = Resources.FindObjectsOfTypeAll<Rigidbody>();
            var result = new List<Rigidbody>();
            foreach (Rigidbody body in all)
            {
                if (body == null || !body.gameObject.scene.IsValid()) continue;
                if (!body.name.StartsWith("Detached tree limb")) continue;
                result.Add(body);
            }
            return result;
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
                if (capture != null) Object.Destroy(capture);
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                capture.Apply(false, false);
                byte[] png = capture.EncodeToPNG();
                Assert.That(png, Is.Not.Null);
                Assert.That(png.Length, Is.GreaterThan(0));
                File.WriteAllBytes(path, png);
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }
    }
}
