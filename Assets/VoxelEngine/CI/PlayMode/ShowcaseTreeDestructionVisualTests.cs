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
    /// Full Showcase destruction lifecycle proof. Standing tree damage remains GameObject-free;
    /// only the branch/crown that actually disconnects may materialize a temporary Rigidbody.
    /// </summary>
    public sealed class ShowcaseTreeDestructionVisualTests
    {
        private const int Width = 1024;
        private const int Height = 1024;
        private const float StartupTimeoutSeconds = 30f;
        private const float ImpactTimeoutSeconds = 4f;
        private const float DetachedLifetimeProofSeconds = 7.35f;

        [UnityTest]
        public IEnumerator ShowcaseTornado_DetachesOnlyPhysicalPiecesAsGameObjects()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
            string outputDirectory = Path.Combine(projectRoot, "Artifacts", "ShowcaseTreeDestruction");
            Directory.CreateDirectory(outputDirectory);

            GameObject cameraObject = null;
            RenderTexture target = null;
            Texture2D capture = null;

            try
            {
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
                for (int frame = 0; frame < 10; frame++) yield return null;

                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.transform.childCount, Is.EqualTo(0));

                int treeIndex = SelectTreeForDestruction();
                Assert.That(treeIndex, Is.GreaterThanOrEqualTo(0));
                TreeInstance instance = TreeWorldState.Instances[treeIndex];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                int branchTargetIndex = SelectStructuralUpperBranch(skeleton);
                int trunkTargetIndex = SelectLowerTrunkBranch(skeleton);
                Assert.That(branchTargetIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(trunkTargetIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.False);
                Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out _), Is.False);
                int dynamicBefore = renderer.DynamicPresentationCount;

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

                var cutsBeforeBranch = new HashSet<int>(TreeWorldState.RemovedBranches(treeIndex));
                showcase.LaunchTornado((Vector3)branchOrigin, (Vector3)branchDirection, 2);
                float branchDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
                while (TreeWorldState.RemovedBranches(treeIndex).Count <= cutsBeforeBranch.Count
                       && Time.realtimeSinceStartup < branchDeadline)
                    yield return null;

                var cutsAfterBranch = new HashSet<int>(TreeWorldState.RemovedBranches(treeIndex));
                Assert.That(cutsAfterBranch.Count, Is.EqualTo(cutsBeforeBranch.Count + 1));
                int detachedBranchIndex = FindSingleNewCut(cutsBeforeBranch, cutsAfterBranch);
                Assert.That(detachedBranchIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(skeleton.Branches[detachedBranchIndex].Level, Is.GreaterThan(0));
                Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out _), Is.False,
                            "Standing damaged tree must remain GameObject-free.");
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));
                Assert.That(renderer.transform.childCount, Is.EqualTo(0));
                Assert.That(renderer.DynamicPresentationCount, Is.GreaterThanOrEqualTo(dynamicBefore));

                Rigidbody branchBody = FindDetachedBody(treeIndex, detachedBranchIndex);
                float detachedDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
                while (branchBody == null && Time.realtimeSinceStartup < detachedDeadline)
                {
                    yield return null;
                    branchBody = FindDetachedBody(treeIndex, detachedBranchIndex);
                }
                Assert.That(branchBody, Is.Not.Null,
                            "Semantic branch was removed but no matching physical debris was materialized.");
                MeshRenderer branchRenderer = branchBody.GetComponent<MeshRenderer>();
                Assert.That(branchRenderer, Is.Not.Null);
                Assert.That(branchRenderer.enabled, Is.True);

                Vector3 branchStartPosition = branchBody.position;
                Quaternion branchStartRotation = branchBody.rotation;
                yield return new WaitForSeconds(0.45f);
                float branchTravel = Vector3.Distance(branchStartPosition, branchBody.position);
                float branchRotation = Quaternion.Angle(branchStartRotation, branchBody.rotation);
                Assert.That(branchTravel + branchBody.linearVelocity.magnitude * 0.08f, Is.GreaterThan(0.08f));
                Assert.That(branchRotation, Is.GreaterThan(2f));
                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "02-showcase-branch-falling.png"));

                yield return new WaitForSeconds(DetachedLifetimeProofSeconds);
                Assert.That(branchBody == null, Is.True);
                Assert.That(FindDetachedBody(treeIndex, detachedBranchIndex), Is.Null);
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

                var cutsBeforeTrunk = new HashSet<int>(TreeWorldState.RemovedBranches(treeIndex));
                showcase.LaunchTornado((Vector3)trunkOrigin, (Vector3)trunkDirection, 2);
                float trunkDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
                while ((!TreeWorldState.Damage[treeIndex].Severed
                        || TreeWorldState.RemovedBranches(treeIndex).Count <= cutsBeforeTrunk.Count)
                       && Time.realtimeSinceStartup < trunkDeadline)
                    yield return null;

                Assert.That(TreeWorldState.Damage[treeIndex].Severed, Is.True);
                var cutsAfterTrunk = new HashSet<int>(TreeWorldState.RemovedBranches(treeIndex));
                Assert.That(cutsAfterTrunk.Count, Is.EqualTo(cutsBeforeTrunk.Count + 1));
                int severedTrunkIndex = FindSingleNewCut(cutsBeforeTrunk, cutsAfterTrunk);
                Assert.That(severedTrunkIndex, Is.GreaterThanOrEqualTo(0));
                Assert.That(skeleton.Branches[severedTrunkIndex].Level, Is.EqualTo(0));
                Assert.That(renderer.TryGetDynamicPresentationRoot(treeIndex, out _), Is.False);
                Assert.That(renderer.ResidentRenderObjectCount, Is.EqualTo(0));

                Rigidbody crown = FindDetachedBody(treeIndex, severedTrunkIndex);
                float crownDeadline = Time.realtimeSinceStartup + ImpactTimeoutSeconds;
                while (crown == null && Time.realtimeSinceStartup < crownDeadline)
                {
                    yield return null;
                    crown = FindDetachedBody(treeIndex, severedTrunkIndex);
                }
                Assert.That(crown, Is.Not.Null);
                Assert.That(crown.GetComponent<CapsuleCollider>(), Is.Not.Null);

                Vector3 crownStart = crown.position;
                float startTilt = Vector3.Angle(crown.transform.up, Vector3.up);
                yield return new WaitForSeconds(0.70f);
                float crownTravel = Vector3.Distance(crownStart, crown.position);
                float crownTilt = Vector3.Angle(crown.transform.up, Vector3.up);
                Assert.That(crownTilt, Is.GreaterThan(startTilt + 8f));
                Assert.That(crownTravel + crown.linearVelocity.magnitude * 0.1f, Is.GreaterThan(0.10f));
                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "04-showcase-crown-toppling.png"));

                yield return new WaitForSeconds(DetachedLifetimeProofSeconds);
                Assert.That(crown == null, Is.True);
                Assert.That(FindDetachedBody(treeIndex, severedTrunkIndex), Is.Null);
                Capture(camera, target, ref capture,
                        Path.Combine(outputDirectory, "05-showcase-crown-gone.png"));

                string metadata =
                    $"treeIndex={treeIndex}\n" +
                    $"species={instance.Species}\n" +
                    $"standingRenderObjects={renderer.ResidentRenderObjectCount}\n" +
                    $"dynamicPresentationsBefore={dynamicBefore}\n" +
                    $"dynamicPresentationsAfterDamage={renderer.DynamicPresentationCount}\n" +
                    $"branchCutIndex={detachedBranchIndex}\n" +
                    $"branchTravelMetres={branchTravel:F3}\n" +
                    $"branchRotationDegrees={branchRotation:F2}\n" +
                    $"branchExpired={FindDetachedBody(treeIndex, detachedBranchIndex) == null}\n" +
                    $"trunkCutIndex={severedTrunkIndex}\n" +
                    $"severedAfterTrunk={TreeWorldState.Damage[treeIndex].Severed}\n" +
                    $"crownTravelMetres={crownTravel:F3}\n" +
                    $"crownTiltDegrees={crownTilt:F2}\n" +
                    $"crownExpired={FindDetachedBody(treeIndex, severedTrunkIndex) == null}\n";
                File.WriteAllText(Path.Combine(outputDirectory, "showcase-tree-destruction.txt"), metadata);
                Debug.Log($"CI Showcase tornado tree destruction written to {outputDirectory}\n{metadata}");
            }
            finally
            {
                if (capture != null) Object.Destroy(capture);
                if (target != null)
                {
                    target.Release();
                    Object.Destroy(target);
                }
                if (cameraObject != null) Object.Destroy(cameraObject);
            }
        }

        private static int SelectTreeForDestruction()
        {
            for (int i = 0; i < TreeWorldState.Instances.Count; i++)
            {
                TreeInstance instance = TreeWorldState.Instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                if (SelectStructuralUpperBranch(skeleton) >= 0 && SelectLowerTrunkBranch(skeleton) >= 0)
                    return i;
            }
            return -1;
        }

        private static int SelectStructuralUpperBranch(ProceduralTreeSkeleton skeleton)
        {
            int best = -1;
            float bestRadius = 0f;
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                if (branch.Level <= 0) continue;
                float radius = Mathf.Max(branch.RadiusStart, branch.RadiusEnd);
                if (radius <= bestRadius) continue;
                bestRadius = radius;
                best = i;
            }
            return best;
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

        private static int FindSingleNewCut(HashSet<int> before, HashSet<int> after)
        {
            foreach (int cut in after)
                if (!before.Contains(cut)) return cut;
            return -1;
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

        private static ProceduralTreeRenderer FindRuntimeRenderer()
        {
            ProceduralTreeRenderer[] all = Resources.FindObjectsOfTypeAll<ProceduralTreeRenderer>();
            foreach (ProceduralTreeRenderer renderer in all)
                if (renderer != null && renderer.gameObject.scene.IsValid()) return renderer;
            return null;
        }

        private static void Capture(Camera camera, RenderTexture target,
                                    ref Texture2D capture, string path)
        {
            camera.Render();
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            if (capture == null)
                capture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, false);
            capture.ReadPixels(new Rect(0, 0, Width, Height), 0, 0, false);
            capture.Apply(false, false);
            File.WriteAllBytes(path, capture.EncodeToPNG());
            RenderTexture.active = previous;
        }
    }
}
