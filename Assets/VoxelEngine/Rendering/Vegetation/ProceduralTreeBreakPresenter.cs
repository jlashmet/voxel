using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>Small presentation-only splinter cap emitted when semantic tree state is severed.</summary>
    public sealed class ProceduralTreeBreakPresenter : MonoBehaviour
    {
        private const float LifetimeSeconds = 9f;
        private static ProceduralTreeBreakPresenter s_Instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => s_Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Instance != null) return;
            var go = new GameObject("Procedural Tree Breaks") { hideFlags = HideFlags.DontSave };
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<ProceduralTreeBreakPresenter>();
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

        private void OnEnable() => TreeWorldState.TreeSevered += OnTreeSevered;
        private void OnDisable() => TreeWorldState.TreeSevered -= OnTreeSevered;

        private void OnTreeSevered(TreeSeveredEvent severed)
        {
            if ((uint)severed.TreeIndex >= (uint)TreeWorldState.Instances.Count) return;
            if (!ProceduralTreeMaterials.Ensure()) return;

            TreeInstance instance = TreeWorldState.Instances[severed.TreeIndex];
            ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
            float rootY = instance.PositionMetres.y;
            float hitY = math.clamp(severed.HitPointMetres.y,
                                    rootY + 0.08f,
                                    rootY + skeleton.Height * 0.46f);
            float3 breakPoint = severed.HitPointMetres;
            if (math.lengthsq(breakPoint) < 1e-8f)
                breakPoint = instance.PositionMetres + new float3(0f, skeleton.Height * 0.12f, 0f);
            breakPoint.y = hitY;

            float radius = 0.18f;
            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                TreeBranchSegment branch = skeleton.Branches[i];
                if (branch.Level != 0) continue;
                float minY = instance.PositionMetres.y + math.min(branch.Start.y, branch.End.y);
                float maxY = instance.PositionMetres.y + math.max(branch.Start.y, branch.End.y);
                if (hitY < minY - 0.2f || hitY > maxY + 0.2f) continue;
                radius = math.max(radius, math.max(branch.RadiusStart, branch.RadiusEnd));
                break;
            }

            Mesh mesh = BuildSplinterCap(radius, instance.Seed ^ (uint)severed.TreeIndex);
            var go = new GameObject($"Tree break {severed.TreeIndex}")
            {
                hideFlags = HideFlags.DontSave,
            };
            go.transform.position = (Vector3)breakPoint;
            var filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ProceduralTreeMaterials.Bark;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            var cleanup = go.AddComponent<BreakMeshCleanup>();
            cleanup.Mesh = mesh;
            Destroy(go, LifetimeSeconds);
        }

        private static Mesh BuildSplinterCap(float radius, uint seed)
        {
            const int sides = 12;
            var vertices = new List<Vector3>(sides + 1);
            var normals = new List<Vector3>(sides + 1);
            var colours = new List<Color>(sides + 1);
            var indices = new List<int>(sides * 3);
            var rng = new Unity.Mathematics.Random(seed == 0 ? 1u : seed);
            Color bark = new(0.30f, 0.19f, 0.10f, 1f);

            vertices.Add(new Vector3(0f, -0.03f, 0f));
            normals.Add(Vector3.up);
            colours.Add(bark);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float jag = rng.NextFloat(0.08f, 0.38f);
                float r = radius * rng.NextFloat(0.82f, 1.08f);
                vertices.Add(new Vector3(Mathf.Cos(angle) * r, jag,
                                         Mathf.Sin(angle) * r));
                normals.Add(Vector3.up);
                colours.Add(bark);
            }
            for (int i = 0; i < sides; i++)
            {
                int a = i + 1;
                int b = ((i + 1) % sides) + 1;
                indices.Add(0); indices.Add(b); indices.Add(a);
            }

            var mesh = new Mesh { name = "Procedural tree splinter cap", hideFlags = HideFlags.DontSave };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private sealed class BreakMeshCleanup : MonoBehaviour
        {
            public Mesh Mesh;
            private void OnDestroy()
            {
                if (Mesh != null) Destroy(Mesh);
            }
        }
    }
}
