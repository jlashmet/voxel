using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VoxelEngine.Vegetation.Runtime;
using VoxelEngine.Vegetation.Api;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Showcase;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.CI
{
    /// <summary>
    /// Full Showcase destruction lifecycle proof. A targeted hit must remove one connected branch
    /// from the standing mesh, render that exact branch as independently falling debris, then retire
    /// it. A lower-trunk hit must leave only the stump standing while the remaining crown topples
    /// from the cut and eventually disappears as well.
    /// </summary>
    /// <remarks>
    /// <see cref="NUnit.Framework.ExplicitAttribute"/>: this captures images for a human to
    /// look at rather than asserting behaviour, and it is one of the slowest things in the
    /// suite. Run it by name when you want the artefacts:
    /// <c>tools/unity-run.sh ... -testFilter ShowcaseTreeDestructionVisualTests</c>
    /// </remarks>
    [NUnit.Framework.Explicit("Artefact capture for human review; run by name.")]
    public sealed class ShowcaseTreeDestructionVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float StartupTimeoutSeconds = 30f;
        private const float ImpactTimeoutSeconds = 4f;
        private const float DetachedLifetimeProofSeconds = 7.35f;

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

            // ShowcaseTreePopulation.Completed only resets on SubsystemRegistration, not per
            // scene load. Retire any previous population so the wait below actually waits for
            // this scene's trees rather than observing the last test's registry.
            TreeWorldRuntime.Clear();

            // Load by path, not by name: VoxelShowcase is deliberately not in the build profile
            // (KentridgePlayableSlice is the launch scene), and LoadSceneAsync by name resolves
            // only against that list. Every other showcase test loads this scene the same way.
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

            VoxelShowcase showcase = Object.FindFirstObjectByType<VoxelShowcase>();
            Assert.That(showcase, Is.Not.Null);
            ProceduralTreeRenderer renderer = null;
            while ((renderer == null || renderer.PresentationCount < TreeWorldRuntime.Instances.Count)
                   && Time.realtimeSinceStartup < startupDeadline)
            {
                renderer = FindRuntimeRenderer();
                yield return null;
            }
            Assert.That(renderer, Is.Not.Null);
            for (int frame = 0; frame < 20; frame++) yield return null;

            int treeIndex = SelectTreeForDestruction();
            Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0));
            TreeInstance instance = TreeWorldRuntime.Instances[treeIndex];
            TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            int branchTargetIndex = SelectStructuralUpperBranch(skeleton);
            int trunkTargetIndex = SelectLowerTrunkBranch(skeleton);
            Assert.That(branchTargetIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(trunkTargetIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(TreeWorldRuntime.Damage[treeIndex].Severed, Is.False);

            bool beganBatched = !renderer.TryGetDynamicPresentationRoot(treeIndex, out _);
            int dynamicBefore = renderer.DynamicPresentationCount;

            baselineMesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, 0);
            int barkBefore = (int)baselineMesh.GetIndexCount(0) / 3;
            int leavesBefore = (int)baselineMesh.GetIndexCount(1) / 3;
            var cutsBeforeBranch = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
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

            // ---- Individual branch lifecycle ---------------------------------
            TreeBranchSegment branchTarget = skeleton.Branches[branchTargetIndex];
            float3 branchTargetMetres = instance.PositionMetres
                                      + (branchTarget.Start + branchTarget.End) * 0.5f;
            float3 branchDirection = PerpendicularSweepDirection(branchTarget.End - branchTarget.Start);
            float branchOffset = math.max(1.1f,
                math.max(branchTarget.RadiusStart, branchTarget.RadiusEnd) * 5f + 0.55f);
            float3 branchOrigin = branchTargetMetres - branchDirection * branchOffset;

            showcase.LaunchTornado((Vector3)branchOrigin, (Vector3)branchDirection, 2);
            float branchDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while ((TreeWorldRuntime.RemovedBranches(treeIndex).Count <= cutsBeforeBranch.Count
                    || !renderer.TryGetDynamicPresentationRoot(treeIndex, out _))
                   && Time.realtimeSinceStartup < branchDeadline)
                yield return null;

            var cutsAfterBranch = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
            Assert.That(cutsAfterBranch.Count, Is.EqualTo(cutsBeforeBranch.Count + 1),
                        "A localized branch hit must create one connected semantic cut, not shred several branches at once.");
            int detachedBranchIndex = FindSingleNewCut(cutsBeforeBranch, cutsAfterBranch);
            Assert.That(detachedBranchIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(skeleton.Branches[detachedBranchIndex].Level, Is.GreaterThan(0),
                        "The first targeted hit unexpectedly severed the trunk instead of an individual branch.");
            Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out Transform treeRoot), Is.True,
                        "First real damage did not lazily materialize the tree presentation.");
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f));
            if (beganBatched)
                Assert.That(renderer.DynamicPresentationCount, Is.EqualTo(dynamicBefore + 1));

            Transform lod0 = treeRoot.Find("LOD0");
            Assert.That(lod0, Is.Not.Null);
            Mesh liveMesh = lod0.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(liveMesh, Is.Not.Null);
            for (int frame = 0; frame < 3; frame++) yield return null;

            int barkAfterBranch = (int)liveMesh.GetIndexCount(0) / 3;
            int leavesAfterBranch = (int)liveMesh.GetIndexCount(1) / 3;
            Assert.That(TreeWorldRuntime.Damage[treeIndex].Severed, Is.False);
            Assert.That(barkAfterBranch, Is.LessThan(barkBefore - 48),
                        "The standing tree still contains too much of the severed branch; this looks like twig damage.");
            Assert.That(leavesAfterBranch, Is.LessThan(leavesBefore));

            Rigidbody branchBody = FindDetachedBody(treeIndex, detachedBranchIndex);
            Assert.That(branchBody, Is.Not.Null,
                        "The semantic branch was removed but no matching falling branch was presented.");
            MeshRenderer branchRenderer = branchBody.GetComponent<MeshRenderer>();
            Assert.That(branchRenderer, Is.Not.Null);
            Assert.That(branchRenderer.enabled, Is.True);
            int branchVisiblePixels = CountExactRendererPixels(camera, target, branchRenderer, 8);
            Assert.That(branchVisiblePixels, Is.GreaterThan(48),
                        "The detached branch exists but does not contribute visible pixels to the Showcase frame.");

            Vector3 branchStartPosition = branchBody.position;
            Quaternion branchStartRotation = branchBody.rotation;
            yield return new WaitForSeconds(0.45f);
            float branchTravel = Vector3.Distance(branchStartPosition, branchBody.position);
            float branchRotation = Quaternion.Angle(branchStartRotation, branchBody.rotation);
            Assert.That(branchTravel + branchBody.linearVelocity.magnitude * 0.08f, Is.GreaterThan(0.08f),
                        "The detached branch did not visibly fall/move after separation.");
            Assert.That(branchRotation, Is.GreaterThan(2f),
                        "The detached branch translated but did not tumble independently.");
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "02-showcase-branch-falling.png"));

            yield return new WaitForSeconds(DetachedLifetimeProofSeconds);
            Assert.That(branchBody == null, Is.True,
                        "Detached branch remained resident after its debris lifetime expired.");
            Assert.That(FindDetachedBody(treeIndex, detachedBranchIndex), Is.Null,
                        "Expired branch still has a Rigidbody/renderer in the scene.");
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "03-showcase-branch-gone.png"));

            // ---- Base hit / remaining crown lifecycle -------------------------
            TreeBranchSegment trunkTarget = skeleton.Branches[trunkTargetIndex];
            float3 trunkTargetMetres = instance.PositionMetres
                                     + (trunkTarget.Start + trunkTarget.End) * 0.5f;
            float3 trunkDirection = PerpendicularSweepDirection(trunkTarget.End - trunkTarget.Start);
            float trunkOffset = math.max(1.2f,
                math.max(trunkTarget.RadiusStart, trunkTarget.RadiusEnd) * 6f + 0.65f);
            float3 trunkOrigin = trunkTargetMetres - trunkDirection * trunkOffset;

            float tornadoWaitDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while (showcase.ActiveTornadoCount != 0
                   && Time.realtimeSinceStartup < tornadoWaitDeadline)
                yield return null;
            Assert.That(showcase.ActiveTornadoCount, Is.EqualTo(0));

            var cutsBeforeTrunk = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
            showcase.LaunchTornado((Vector3)trunkOrigin, (Vector3)trunkDirection, 2);
            float trunkDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
            while ((!TreeWorldRuntime.Damage[treeIndex].Severed
                    || TreeWorldRuntime.RemovedBranches(treeIndex).Count <= cutsBeforeTrunk.Count)
                   && Time.realtimeSinceStartup < trunkDeadline)
                yield return null;

            Assert.That(TreeWorldRuntime.Damage[treeIndex].Severed, Is.True);
            var cutsAfterTrunk = new HashSet<int>(TreeWorldRuntime.RemovedBranches(treeIndex));
            Assert.That(cutsAfterTrunk.Count, Is.EqualTo(cutsBeforeTrunk.Count + 1),
                        "The base hit should make one connected trunk sever.");
            int severedTrunkIndex = FindSingleNewCut(cutsBeforeTrunk, cutsAfterTrunk);
            Assert.That(severedTrunkIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(skeleton.Branches[severedTrunkIndex].Level, Is.EqualTo(0));
            Assert.That(Quaternion.Angle(treeRoot.localRotation, Quaternion.identity), Is.LessThan(1f),
                        "The rooted presentation must remain the stump, not rotate as a whole tree.");

            for (int frame = 0;
                 frame < 8 && (int)liveMesh.GetIndexCount(0) / 3 >= barkAfterBranch;
                 frame++)
                yield return null;

            int barkAfterTrunk = (int)liveMesh.GetIndexCount(0) / 3;
            Assert.That(barkAfterTrunk, Is.LessThan(barkAfterBranch));
            Assert.That(CountBreakCaps(), Is.GreaterThan(0));

            Rigidbody crown = FindDetachedBody(treeIndex, severedTrunkIndex);
            Assert.That(crown, Is.Not.Null,
                        "Base sever removed the upper tree semantically but did not create the falling crown.");
            Assert.That(crown.GetComponent<CapsuleCollider>(), Is.Not.Null,
                        "The falling crown should collide like a trunk, not like a giant canopy box.");
            MeshRenderer crownRenderer = crown.GetComponent<MeshRenderer>();
            Assert.That(crownRenderer, Is.Not.Null);
            int crownVisiblePixels = CountExactRendererPixels(camera, target, crownRenderer, 8);
            Assert.That(crownVisiblePixels, Is.GreaterThan(128),
                        "The remaining crown exists but is not visibly rendered after the base hit.");

            Vector3 crownStart = crown.position;
            float startTilt = Vector3.Angle(crown.transform.up, Vector3.up);
            yield return new WaitForSeconds(0.70f);
            float crownTravel = Vector3.Distance(crownStart, crown.position);
            float crownTilt = Vector3.Angle(crown.transform.up, Vector3.up);
            Assert.That(crownTilt, Is.GreaterThan(startTilt + 8f),
                        "The remaining tree translated but did not visibly topple from the severed base.");
            Assert.That(crownTravel + crown.linearVelocity.magnitude * 0.1f, Is.GreaterThan(0.10f));
            int crownToppleVisiblePixels = CountExactRendererPixels(camera, target, crownRenderer, 8);
            Assert.That(crownToppleVisiblePixels, Is.GreaterThan(128),
                        "The crown toppled numerically but left the camera before the fall could be seen.");
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "04-showcase-crown-toppling.png"));

            yield return new WaitForSeconds(DetachedLifetimeProofSeconds);
            Assert.That(crown == null, Is.True,
                        "Fallen crown remained resident after its debris lifetime expired.");
            Assert.That(FindDetachedBody(treeIndex, severedTrunkIndex), Is.Null,
                        "Expired fallen crown still has a Rigidbody/renderer in the scene.");
            Capture(camera, target, ref capture,
                    Path.Combine(outputDirectory, "05-showcase-crown-gone.png"));

            string metadata =
                $"treeIndex={treeIndex}\n" +
                $"species={instance.Species}\n" +
                $"beganBatched={beganBatched}\n" +
                $"dynamicPresentationsBefore={dynamicBefore}\n" +
                $"dynamicPresentationsAfterDamage={renderer.DynamicPresentationCount}\n" +
                $"branchCutIndex={detachedBranchIndex}\n" +
                $"branchVisiblePixels={branchVisiblePixels}\n" +
                $"branchTravelMetres={branchTravel:F3}\n" +
                $"branchRotationDegrees={branchRotation:F2}\n" +
                $"branchExpired={FindDetachedBody(treeIndex, detachedBranchIndex) == null}\n" +
                $"barkTrianglesBefore={barkBefore}\n" +
                $"barkTrianglesAfterBranch={barkAfterBranch}\n" +
                $"leafTrianglesBefore={leavesBefore}\n" +
                $"leafTrianglesAfterBranch={leavesAfterBranch}\n" +
                $"trunkCutIndex={severedTrunkIndex}\n" +
                $"severedAfterTrunk={TreeWorldRuntime.Damage[treeIndex].Severed}\n" +
                $"barkTrianglesAfterTrunk={barkAfterTrunk}\n" +
                $"breakCaps={CountBreakCaps()}\n" +
                $"crownVisiblePixels={crownVisiblePixels}\n" +
                $"crownToppleVisiblePixels={crownToppleVisiblePixels}\n" +
                $"crownTravelMetres={crownTravel:F3}\n" +
                $"crownTiltDegrees={crownTilt:F2}\n" +
                $"crownExpired={FindDetachedBody(treeIndex, severedTrunkIndex) == null}\n" +
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
            int count = math.min(TreeWorldRuntime.Instances.Count, TreeWorldRuntime.Damage.Count);
            for (int i = 0; i < count; i++)
            {
                if (TreeWorldRuntime.Damage[i].Severed) continue;
                TreeInstance instance = TreeWorldRuntime.Instances[i];
                TreeSkeletonSnapshot skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                if (SelectStructuralUpperBranch(skeleton) < 0) continue;
                if (SelectLowerTrunkBranch(skeleton) < 0) continue;
                return i;
            }
            return -1;
        }

        private static int SelectStructuralUpperBranch(TreeSkeletonSnapshot skeleton)
        {
            int bestBranch = -1;
            int bestScore = -1;
            var resolved = new HashSet<int>();
            for (int branchIndex = 0; branchIndex < skeleton.Branches.Count; branchIndex++)
            {
                if (skeleton.Branches[branchIndex].Level != 1) continue;
                TreeSkeletonTopology.ResolveRemovedBranches(
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

        private static int FindSingleNewCut(HashSet<int> before, HashSet<int> after)
        {
            int found = -1;
            foreach (int index in after)
            {
                if (before.Contains(index)) continue;
                Assert.That(found, Is.EqualTo(-1), "More than one new direct cut was created.");
                found = index;
            }
            return found;
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

        private static Rigidbody FindDetachedBody(int treeIndex, int branchIndex)
        {
            string expectedName = $"Detached tree limb {treeIndex}:{branchIndex}";
            Rigidbody[] all = Resources.FindObjectsOfTypeAll<Rigidbody>();
            foreach (Rigidbody body in all)
            {
                if (body == null || body.gameObject == null || !body.gameObject.scene.IsValid()) continue;
                if (body.name == expectedName) return body;
            }
            return null;
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

        private static int CountExactRendererPixels(Camera camera, RenderTexture target,
                                                    MeshRenderer exactRenderer, int threshold)
        {
            Assert.That(exactRenderer, Is.Not.Null);
            bool wasEnabled = exactRenderer.enabled;
            Texture2D withRenderer = null;
            Texture2D withoutRenderer = null;
            try
            {
                exactRenderer.enabled = true;
                withRenderer = CaptureTexture(camera, target);
                exactRenderer.enabled = false;
                withoutRenderer = CaptureTexture(camera, target);
                return CountChangedPixels(withRenderer, withoutRenderer, threshold);
            }
            finally
            {
                if (exactRenderer != null) exactRenderer.enabled = wasEnabled;
                if (withRenderer != null) Object.Destroy(withRenderer);
                if (withoutRenderer != null) Object.Destroy(withoutRenderer);
            }
        }

        private static Texture2D CaptureTexture(Camera camera, RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            try
            {
                camera.Render();
                RenderTexture.active = target;
                var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
                texture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
                texture.Apply(false, false);
                return texture;
            }
            finally
            {
                RenderTexture.active = previous;
            }
        }

        private static int CountChangedPixels(Texture2D a, Texture2D b, int threshold)
        {
            Color32[] first = a.GetPixels32();
            Color32[] second = b.GetPixels32();
            Assert.That(first.Length, Is.EqualTo(second.Length));
            int changed = 0;
            for (int i = 0; i < first.Length; i++)
            {
                int maxDelta = Mathf.Max(
                    Mathf.Abs(first[i].r - second[i].r),
                    Mathf.Max(Mathf.Abs(first[i].g - second[i].g),
                              Mathf.Abs(first[i].b - second[i].b)));
                if (maxDelta >= threshold) changed++;
            }
            return changed;
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
