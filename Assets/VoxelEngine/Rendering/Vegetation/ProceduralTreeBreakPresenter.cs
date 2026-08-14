using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Small presentation-only splinter caps emitted when semantic tree state is severed. Caps are
    /// transient meshes submitted directly through Graphics.DrawMesh; they never materialize a
    /// per-break GameObject. Only detached pieces that require physics use GameObjects.
    /// </summary>
    public sealed class ProceduralTreeBreakPresenter : MonoBehaviour
    {
        private const float LifetimeSeconds = 9f;

        private sealed class ActiveBreak
        {
            public Mesh Mesh;
            public Matrix4x4 Matrix;
            public float ExpiresAt;
        }

        private static ProceduralTreeBreakPresenter s_Instance;
        private readonly List<ActiveBreak> _breaks = new();

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

        private void Update()
        {
            if (!ProceduralTreeMaterials.Ensure()) return;

            float now = Time.time;
            for (int i = _breaks.Count - 1; i >= 0; i--)
            {
                ActiveBreak active = _breaks[i];
                if (now >= active.ExpiresAt)
                {
                    if (active.Mesh != null) Destroy(active.Mesh);
                    _breaks.RemoveAt(i);
                    continue;
                }

                Graphics.DrawMesh(active.Mesh, active.Matrix, ProceduralTreeMaterials.Bark,
                                  0, null, 0, null, ShadowCastingMode.On, true);
            }
        }

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

            _breaks.Add(new ActiveBreak
            {
                Mesh = BuildSplinterCap(radius, instance.Seed ^ (uint)severed.TreeIndex),
                Matrix = Matrix4x4.TRS((Vector3)breakPoint, Quaternion.identity, Vector3.one),
                ExpiresAt = Time.time + LifetimeSeconds,
            });
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

        private void OnDestroy()
        {
            for (int i = 0; i < _breaks.Count; i++)
                if (_breaks[i].Mesh != null) Destroy(_breaks[i].Mesh);
            _breaks.Clear();
            if (s_Instance == this) s_Instance = null;
        }
    }
}
