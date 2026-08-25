using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Rendering.Runtime.Vegetation
{
    /// <summary>
    /// Presentation subscriber for branch-cut domain events. It derives only the newly disconnected
    /// subtree and gives that temporary visual a Rigidbody. The exact skeleton is shared with the
    /// damage service so a hit never regenerates the same procedural tree just for debris.
    /// </summary>
    public sealed class ProceduralTreeDetachedLimbPresenter : MonoBehaviour
    {
        private const float LifetimeSeconds = 7f;
        private const float TrunkHingeSeconds = 0.90f;
        private static ProceduralTreeDetachedLimbPresenter s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => s_Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Instance != null) return;
            var go = new GameObject("Procedural Tree Detached Limbs")
            {
                hideFlags = HideFlags.DontSave,
            };
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<ProceduralTreeDetachedLimbPresenter>();
        }

        private void Awake()
        {
            if (s_Instance != null && s_Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            s_Instance = this;
        }

        private void OnEnable() => TreeWorldReadRegistry.Current.BranchCut += OnBranchCut;
        private void OnDisable() => TreeWorldReadRegistry.Current.BranchCut -= OnBranchCut;

        private void OnBranchCut(TreeBranchCutEvent cut)
        {
            if ((uint)cut.TreeIndex >= (uint)TreeWorldReadRegistry.Current.Instances.Count) return;
            if (!ProceduralTreeMaterials.Ensure()) return;

            TreeInstance instance = TreeWorldReadRegistry.Current.Instances[cut.TreeIndex];
            TreeSkeletonSnapshot skeleton = TreeWorldReadRegistry.Current.SkeletonFor(cut.TreeIndex);
            if (skeleton == null || (uint)cut.BranchIndex >= (uint)skeleton.Branches.Count) return;

            TreeBranchSegment cutBranch = skeleton.Branches[cut.BranchIndex];
            bool levelZeroCut = cutBranch.Level == 0;

            // BranchCut is fired after the direct cut has entered TreeWorldReadRegistry.Current.
            // Subtract geometry removed by earlier branch cuts so already-fallen limbs never spawn
            // a second time when the remaining tree is later severed.
            var previousCuts = new HashSet<int>(TreeWorldReadRegistry.Current.RemovedBranches(cut.TreeIndex));
            previousCuts.Remove(cut.BranchIndex);
            bool hadPreviousTrunkCut = false;
            foreach (int previousCut in previousCuts)
            {
                if ((uint)previousCut >= (uint)skeleton.Branches.Count) continue;
                if (skeleton.Branches[previousCut].Level == 0)
                {
                    hadPreviousTrunkCut = true;
                    break;
                }
            }

            bool fallingCrown = levelZeroCut && !hadPreviousTrunkCut;
            var subtree = new HashSet<int>();
            if (fallingCrown)
            {
                // A structural trunk sever retires the rooted tree completely. Move the entire
                // remaining skeleton into one falling body rather than leaving the lower trunk as
                // a second standing presentation (SceneIssue 20260825-033015-205).
                for (int i = 0; i < skeleton.Branches.Count; i++) subtree.Add(i);
            }
            else
            {
                subtree.Add(cut.BranchIndex);
                IReadOnlyList<int> parents = skeleton.BranchParents;
                if (parents != null)
                {
                    for (int i = cut.BranchIndex + 1; i < parents.Count; i++)
                    {
                        int parent = parents[i];
                        if (parent >= 0 && subtree.Contains(parent)) subtree.Add(i);
                    }
                }
            }

            if (previousCuts.Count > 0)
            {
                subtree.RemoveWhere(branchIndex =>
                    TreeSkeletonTopology.IsBranchRemoved(
                        skeleton, previousCuts, branchIndex));
            }
            if (subtree.Count == 0) return;

            // Author around the actual sever point for the whole-tree fall. The direct branch
            // index still records which trunk segment was hit, but the physics pivot must match
            // the shot rather than the root of that segment.
            bool hasHitPoint = math.lengthsq(cut.HitPointMetres) > 1e-8f;
            Vector3 cutLocal = fallingCrown && hasHitPoint
                ? (Vector3)(cut.HitPointMetres - instance.PositionMetres)
                : (Vector3)cutBranch.Start;
            Mesh mesh = ProceduralTreeMeshBuilder.BuildSubsetMesh(
                skeleton, 0, subtree, -cutLocal);
            if (mesh == null || mesh.vertexCount == 0)
            {
                if (mesh != null) Destroy(mesh);
                return;
            }

            mesh.name = $"Detached_{instance.Species}_{cut.TreeIndex}_{cut.BranchIndex}";
            mesh.hideFlags = HideFlags.DontSave;

            var go = new GameObject($"Detached tree limb {cut.TreeIndex}:{cut.BranchIndex}")
            {
                hideFlags = HideFlags.DontSave,
            };
            Vector3 cutWorld = (Vector3)instance.PositionMetres + cutLocal;
            go.transform.position = cutWorld;
            go.transform.rotation = Quaternion.identity;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = ProceduralTreeMaterials.Shared;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Bounds bounds = mesh.bounds;
            if (fallingCrown)
                AddTrunkCollider(go, in cutBranch, bounds);
            else
                AddBranchCollider(go, bounds);

            var body = go.AddComponent<Rigidbody>();
            body.mass = fallingCrown
                ? Mathf.Clamp(bounds.size.y * 1.35f, 3f, 35f)
                : Mathf.Clamp(bounds.size.magnitude * 0.35f, 0.35f, 12f);
            body.linearDamping = fallingCrown ? 0.30f : 0.12f;
            body.angularDamping = fallingCrown ? 0.38f : 0.18f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Vector3 impulse = (Vector3)cut.Impulse;
            if (impulse.sqrMagnitude < 1e-4f)
            {
                uint seed = instance.Seed ^ (uint)(cut.BranchIndex * 2654435761u);
                float angle = (seed & 0xFFFFu) * (Mathf.PI * 2f / 65535f);
                impulse = new Vector3(Mathf.Cos(angle), 0.12f, Mathf.Sin(angle));
            }

            if (fallingCrown)
            {
                // The sever point is the body origin. Pin it briefly while leaving rotation free
                // so the whole remaining tree visibly hinges from the shot before ordinary fall.
                body.constraints = RigidbodyConstraints.FreezePositionX
                                 | RigidbodyConstraints.FreezePositionY
                                 | RigidbodyConstraints.FreezePositionZ;
                ApplyTopple(body, bounds, impulse);
                StartCoroutine(ReleaseTrunkHinge(body));
            }
            else
            {
                ApplyBranchThrow(body, instance.Seed, cut.BranchIndex, impulse);
            }

            var cleanup = go.AddComponent<GeneratedTreeMeshCleanup>();
            cleanup.Mesh = mesh;
            Destroy(go, LifetimeSeconds);
        }

        private static void AddTrunkCollider(GameObject go, in TreeBranchSegment cutBranch,
                                             Bounds bounds)
        {
            float trunkRadius = Mathf.Max(0.10f,
                Mathf.Max(cutBranch.RadiusStart, cutBranch.RadiusEnd) * 1.55f);
            float availableHeight = Mathf.Max(0.5f, bounds.max.y - Mathf.Max(0f, bounds.min.y));
            float colliderHeight = Mathf.Clamp(availableHeight * 0.72f,
                trunkRadius * 2.05f, Mathf.Max(trunkRadius * 2.05f, 8f));

            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.direction = 1;
            capsule.radius = trunkRadius;
            capsule.height = colliderHeight;
            capsule.center = new Vector3(0f, colliderHeight * 0.5f, 0f);
        }

        private static void AddBranchCollider(GameObject go, Bounds bounds)
        {
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size * 0.68f, Vector3.one * 0.08f);
        }

        private static void ApplyTopple(Rigidbody body, Bounds bounds, Vector3 impulse)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(impulse, Vector3.up);
            if (horizontal.sqrMagnitude < 1e-4f) horizontal = Vector3.right;
            horizontal.Normalize();

            body.centerOfMass = new Vector3(
                0f, Mathf.Clamp(bounds.extents.y * 0.18f, 0.30f, 1.25f), 0f);
            float leverHeight = Mathf.Clamp(bounds.size.y * 0.38f, 1.0f, 4.5f);
            Vector3 forcePoint = body.worldCenterOfMass + Vector3.up * leverHeight;
            body.AddForceAtPosition(horizontal * 0.85f,
                                    forcePoint, ForceMode.VelocityChange);

            Vector3 toppleAxis = Vector3.Cross(Vector3.up, horizontal).normalized;
            if (toppleAxis.sqrMagnitude < 0.1f) toppleAxis = Vector3.right;
            body.angularVelocity = toppleAxis * 0.90f;
        }

        private static void ApplyBranchThrow(Rigidbody body, uint seed, int branchIndex,
                                             Vector3 impulse)
        {
            if (impulse.sqrMagnitude < 1e-4f) impulse = Vector3.right;
            impulse.Normalize();
            body.AddForce(impulse * 3.2f + Vector3.up * 1.15f, ForceMode.VelocityChange);

            uint spinSeed = seed ^ (uint)(branchIndex * 2246822519u);
            Vector3 spinAxis = new Vector3(
                ((spinSeed & 255u) / 127.5f) - 1f,
                (((spinSeed >> 8) & 255u) / 127.5f) - 1f,
                (((spinSeed >> 16) & 255u) / 127.5f) - 1f).normalized;
            if (spinAxis.sqrMagnitude < 0.1f) spinAxis = Vector3.right;
            body.angularVelocity = spinAxis * 2.4f;
        }

        private static IEnumerator ReleaseTrunkHinge(Rigidbody body)
        {
            yield return new WaitForSeconds(TrunkHingeSeconds);
            if (body == null) yield break;

            Vector3 angularVelocity = body.angularVelocity;
            body.linearVelocity = Vector3.zero;
            body.constraints = RigidbodyConstraints.None;
            body.angularVelocity = angularVelocity;
            body.WakeUp();
        }

        private sealed class GeneratedTreeMeshCleanup : MonoBehaviour
        {
            public Mesh Mesh;
            private void OnDestroy()
            {
                if (Mesh != null) Destroy(Mesh);
            }
        }
    }
}
