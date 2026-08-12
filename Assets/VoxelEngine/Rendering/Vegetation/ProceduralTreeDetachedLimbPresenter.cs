using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Presentation subscriber for branch-cut domain events. It derives a mesh for the disconnected
    /// subtree and gives that visual temporary physics without putting GameObject/Rigidbody concerns
    /// into gameplay or tree state.
    /// </summary>
    public sealed class ProceduralTreeDetachedLimbPresenter : MonoBehaviour
    {
        private const float LifetimeSeconds = 7f;
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

        private void OnEnable() => TreeWorldState.BranchCut += OnBranchCut;
        private void OnDisable() => TreeWorldState.BranchCut -= OnBranchCut;

        private void OnBranchCut(TreeBranchCutEvent cut)
        {
            if ((uint)cut.TreeIndex >= (uint)TreeWorldState.Instances.Count) return;
            if (!ProceduralTreeMaterials.Ensure()) return;

            TreeInstance instance = TreeWorldState.Instances[cut.TreeIndex];
            ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            if ((uint)cut.BranchIndex >= (uint)skeleton.Branches.Count) return;

            var subtree = new HashSet<int> { cut.BranchIndex };
            int[] parents = skeleton.BranchParents;
            if (parents != null)
            {
                for (int i = cut.BranchIndex + 1; i < parents.Length; i++)
                {
                    int parent = parents[i];
                    if (parent >= 0 && subtree.Contains(parent)) subtree.Add(i);
                }
            }

            Mesh mesh = ProceduralTreeMeshBuilder.BuildSubsetMesh(skeleton, 0, subtree);
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
            go.transform.position = (Vector3)instance.PositionMetres;

            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = ProceduralTreeMaterials.Shared;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            Bounds bounds = mesh.bounds;
            var collider = go.AddComponent<BoxCollider>();
            collider.center = bounds.center;
            collider.size = Vector3.Max(bounds.size * 0.72f, Vector3.one * 0.08f);

            var body = go.AddComponent<Rigidbody>();
            body.mass = Mathf.Clamp(bounds.size.magnitude * 0.35f, 0.35f, 12f);
            body.linearDamping = 0.12f;
            body.angularDamping = 0.18f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Vector3 impulse = (Vector3)cut.Impulse;
            if (impulse.sqrMagnitude < 1e-4f)
            {
                uint seed = instance.Seed ^ (uint)(cut.BranchIndex * 2654435761u);
                float angle = (seed & 0xFFFFu) * (Mathf.PI * 2f / 65535f);
                impulse = new Vector3(Mathf.Cos(angle), 0.2f, Mathf.Sin(angle));
            }
            impulse.Normalize();
            body.AddForce(impulse * 3.2f + Vector3.up * 1.15f, ForceMode.VelocityChange);

            uint spinSeed = instance.Seed ^ (uint)(cut.BranchIndex * 2246822519u);
            Vector3 spinAxis = new Vector3(
                ((spinSeed & 255u) / 127.5f) - 1f,
                (((spinSeed >> 8) & 255u) / 127.5f) - 1f,
                (((spinSeed >> 16) & 255u) / 127.5f) - 1f).normalized;
            if (spinAxis.sqrMagnitude < 0.1f) spinAxis = Vector3.right;
            body.angularVelocity = spinAxis * 2.4f;

            var cleanup = go.AddComponent<GeneratedTreeMeshCleanup>();
            cleanup.Mesh = mesh;
            Destroy(go, LifetimeSeconds);
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
