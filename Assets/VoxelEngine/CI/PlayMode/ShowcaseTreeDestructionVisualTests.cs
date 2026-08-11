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
    /// Full Showcase integration proof. The production tornado must route into Core semantic tree
    /// collision/damage, visibly detach an upper limb, then leave a rooted stump plus an independently
    /// moving crown on a lower-trunk hit.
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

            Assert.That(ShowcaseTreePopulation.Completed, Is.True);
            Assert.That(TreeWorldState.Instances.Count, Is.GreaterThan(0));

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.That(showcase, Is.Not.Null);
            ProceduralTreeRenderer renderer = null;
            while ((renderer == null
                    || renderer.PresentationCount < TreeWorldState.Instances.Count)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                renderer = FindRuntimeRenderer();
                yield return null;
            }
            Assert.That(renderer, Is.Not.Null);
            Assert.That(renderer.PresentationCount, Is.EqualTo(TreeWorldState.Instances.Count));

            for (int frame = 0; frame < 20; frame++) yield return null;
            int treeIndex = SelectActiveTree(renderer);
            Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0));

            TreeInstance instance = TreeWorldState.Instances[treeIndex];
            Transform treeRoot = renderer.transform.GetChild(treeIndex);
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f));
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);

            Transform lod0 = treeRoot.Find("LOD0");
            Assert.That(lod0, Is.Not.Null);
            Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(liveMesh, Is.Not.Null);
            int barkBefore = (int)liveMesh.GetIndexCount(0) / 3;
            int leavesBefore = (int)liveMesh.GetIndexCount(1) / 3;
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

            ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            int branchIndex = SelectLeafBearingUpperBranch(skeleton);
            Assert.That(branchIndex, Is.GreaterThanOrEqualTo(0));
            TreeBranchSegment branch = skeleton.Branches[branchIndex];
            float3 branchTarget = instance.PositionMetres + (branch.Start + branch.End) * 0.5f;
            float3 branchDirection = PerpendicularSweepDirection(branch.End - branch.Start);
            float branchOffset = math.max(1.1f,
                math.max(branch.RadiusStart, branch.RadiusEnd) * 5f + 0.55f);
            float3 branchOrigin = branchTarget - branchDirection * branchOffset;

            int detachedBeforeBranch = CountDetachedBodies();
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(0));
            showcase.LaunchTornado((Vector3)branchOrigin, (Vector3)branchDirection, 2);

            float branchDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while ((TreeWorldState.RemovedBranches(treeIndex).Count <= cutsBefore
                    || CountDetachedBodies() <= detachedBeforeBranch)
                   && Time.realtimeSinceStartup < branchDeadline)
                yield return null;

            int cutsAfterBranch = TreeWorldState.RemovedBranches(treeIndex).Count;
            int detachedAfterBranch = CountDetachedBodies();
            Assert.That(cutsAfterBranch, Is.GreaterThan(cutsBefore));
            Assert.That(detachedAfterBranch, Is.GreaterThan(detachedBeforeBranch));
            for (int frame = 0; frame < 5; frame++) yield return null;

            int barkAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
            int leavesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
            Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);
            Assert.That(barkAfterBranch, Is.LessThan(barkBefore));
            Assert.That(leavesAfterBranch, Is.LessThan(leavesBefore));
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
                        "The rooted presentation should remain the stump, not rotate as a whole tree.");

            // Semantic severing and detached debris can complete before the standing-tree renderer
            // consumes its dirty event on Update. Wait for the actual mesh result instead of racing
            // that subscriber in the same frame.
            for (int frame = 0;
                 frame < 8 && (int)liveMesh.GetIndexCount(0) / 3 >= barkAfterBranch;
                 frame++)
                yield return null;

            int barkAfterTrunk = (int)liveMesh.GetIndexCount(0) / 3;
            Assert.That(barkAfterTrunk, Is.LessThan(barkAfterBranch));
            Assert.That(CountBreakCaps(), Is.GreaterThan(0));

            Rigidbody crown = FindLargestDetachedBody();
            Assert.That(crown, Is.Not.Null);
            Vector3 crownStart = crown.position;
            yield return new WaitForSeconds(0.55f);
            float crownTravel = Vector3.Distance(crownStart, crown.position);
            Assert.That(crownTravel + crown.linearVelocity.magnitude * 0.1f, Is.GreaterThan(0.10f));

            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "03-showcase-after-trunk.png"));

            string metadata =
                $"treeIndex={treeIndex}\n" +
                $"species={instance.Species}\n" +
                $"presentationRoots={renderer.PresentationCount}\n" +
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
                $"activeTornadoesAtEnd={showcase.ActiveTornadoCount}\n";
            File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree-destruction.txt"), metadata);
            Debug.Log($"CI Showcase tornado tree destruction written to {outputDirectory}\n{metadata}");

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
            int count = math.min(renderer.PresentationCount, TreeWorldState.Instances.Count);
            for (int i = 0; i < count; i++)
            {
                if (!renderer.transform.GetChild(i).gameObject.activeSelf) continue;
                if (i < TreeWorldState.Damage.Count && TreeWorldState.Damage[i].Severed) continue;
                return i;
            }
            return -1;
        }

        private static int SelectLeafBearingUpperBranch(ProceduralTreeSkeleton skeleton)
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
                    int parent = skeleton.LeafParents[leafIndex];
                    if (parent >= 0 && resolved.Contains(parent)) leaves++;
                }
                if (leaves <= bestLeaves) continue;
                bestLeaves = leaves;
                bestBranch = branchIndex;
            }
            return bestLeaves > 0 ? bestBranch : -1;
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

        private static Bounds CalculateBounds(Transform root)
        {
            MeshRenderer[] meshRenderers = root.GetComponentsInChildren<MeshRenderer>(true);
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (MeshRenderer meshRenderer in meshRenderers)
            {
                if (!hasBounds) { bounds = meshRenderer.bounds; hasBounds = true; }
                else bounds.Encapsulate(meshRenderer.bounds);
            }
            return bounds;
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
                Renderer r = body.GetComponent<Renderer>();
                float size = r != null ? r.bounds.size.sqrMagnitude : 0f;
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
