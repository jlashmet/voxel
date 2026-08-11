using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree instances. This deliberately uses ordinary Unity
    /// meshes/LODGroup for the first milestone; the semantic registry and shared-skeleton mesher
    /// can later feed GPU instancing without changing world generation or species definitions.
    ///
    /// Damage is kept separate from identity. The same deterministic skeleton stays allocated;
    /// authoritative gameplay/proxy state only changes foliage presentation and whether the tree
    /// has been severed from its root.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private sealed class TreePresentation
        {
            public TreeInstance Instance;
            public GameObject Root;
            public MeshRenderer[] LodRenderers;
            public bool Falling;
            public float FallStartTime;
            public Vector3 FallAxis;
        }

        private static ProceduralTreeRenderer s_Instance;
        private static readonly int s_Damage = Shader.PropertyToID("_Damage");

        private readonly List<TreePresentation> _trees = new();
        private readonly List<Mesh> _meshes = new();
        private readonly MaterialPropertyBlock _damageProperties = new();
        private Material _barkMaterial;
        private Material _leafMaterial;
        private Material[] _sharedMaterials;
        private int _seenVersion = int.MinValue;
        private int _seenDamageVersion = int.MinValue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic() => s_Instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (s_Instance != null) return;
            var go = new GameObject("Procedural Tree Renderer")
            {
                hideFlags = HideFlags.DontSave,
            };
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<ProceduralTreeRenderer>();
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

        private void Update()
        {
            EnsureMaterials();
            ApplyLighting();

            int version = ProceduralTreeRegistry.Version;
            if (_seenVersion != version)
            {
                _seenVersion = version;
                Rebuild();
                _seenDamageVersion = int.MinValue;
            }

            int damageVersion = ProceduralTreeRegistry.DamageVersion;
            if (_seenDamageVersion != damageVersion)
            {
                _seenDamageVersion = damageVersion;
                ApplyDamage();
            }

            UpdateFallingTrees();
        }

        private void EnsureMaterials()
        {
            if (_barkMaterial != null && _leafMaterial != null) return;

            Shader bark = Shader.Find("VoxelEngine/ProceduralTreeBark");
            Shader leaves = Shader.Find("VoxelEngine/ProceduralTreeLeaves");
            if (bark == null || leaves == null)
            {
                if (bark == null) Debug.LogError("Procedural tree bark shader was not found.");
                if (leaves == null) Debug.LogError("Procedural tree leaf shader was not found.");
                return;
            }

            _barkMaterial = new Material(bark)
            {
                name = "Procedural Tree Bark (Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            _leafMaterial = new Material(leaves)
            {
                name = "Procedural Tree Leaves (Runtime)",
                enableInstancing = true,
                hideFlags = HideFlags.DontSave,
            };
            _sharedMaterials = new[] { _barkMaterial, _leafMaterial };
        }

        private void ApplyLighting()
        {
            if (_barkMaterial == null || _leafMaterial == null) return;
            Vector3 sun = VoxelRenderBridge.SunDirection;
            Color horizon = VoxelRenderBridge.SkyHorizon;
            Color zenith = VoxelRenderBridge.SkyZenith;

            _barkMaterial.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0f));
            _barkMaterial.SetColor("_SkyHorizon", horizon);
            _barkMaterial.SetColor("_SkyZenith", zenith);
            _leafMaterial.SetVector("_SunDirection", new Vector4(sun.x, sun.y, sun.z, 0f));
            _leafMaterial.SetColor("_SkyHorizon", horizon);
            _leafMaterial.SetColor("_SkyZenith", zenith);
        }

        private void Rebuild()
        {
            ClearGenerated();
            if (_sharedMaterials == null) return;

            IReadOnlyList<TreeInstance> instances = ProceduralTreeRegistry.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeMeshBuilder.TreeSkeleton skeleton =
                    ProceduralTreeMeshBuilder.GenerateSkeleton(in instance);

                var root = new GameObject($"Tree {i:000} {instance.Species}")
                {
                    hideFlags = HideFlags.DontSave,
                };
                root.transform.SetParent(transform, false);
                root.transform.position = (Vector3)instance.PositionMetres;

                var lodRenderers = new MeshRenderer[3];
                for (int lod = 0; lod < 3; lod++)
                {
                    Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(skeleton, lod);
                    mesh.name = $"{instance.Species}_{instance.Seed}_LOD{lod}";
                    mesh.hideFlags = HideFlags.DontSave;
                    _meshes.Add(mesh);

                    var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                    child.transform.SetParent(root.transform, false);
                    var filter = child.AddComponent<MeshFilter>();
                    filter.sharedMesh = mesh;
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = _sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    lodRenderers[lod] = renderer;
                }

                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[]
                {
                    new LOD(0.34f, new Renderer[] { lodRenderers[0] }),
                    new LOD(0.13f, new Renderer[] { lodRenderers[1] }),
                    new LOD(0.025f, new Renderer[] { lodRenderers[2] }),
                });
                group.RecalculateBounds();

                _trees.Add(new TreePresentation
                {
                    Instance = instance,
                    Root = root,
                    LodRenderers = lodRenderers,
                    Falling = false,
                });
            }
        }

        private void ApplyDamage()
        {
            IReadOnlyList<ProceduralTreeRegistry.TreeDamageState> damage =
                ProceduralTreeRegistry.Damage;
            int count = Mathf.Min(_trees.Count, damage.Count);

            for (int i = 0; i < count; i++)
            {
                TreePresentation tree = _trees[i];
                ProceduralTreeRegistry.TreeDamageState state = damage[i];
                float damageAmount = 1f - Mathf.Clamp01(state.FoliageHealth);

                _damageProperties.Clear();
                _damageProperties.SetFloat(s_Damage, damageAmount);
                for (int lod = 0; lod < tree.LodRenderers.Length; lod++)
                {
                    MeshRenderer renderer = tree.LodRenderers[lod];
                    if (renderer != null)
                        renderer.SetPropertyBlock(_damageProperties, 1); // leaf submesh/material
                }

                if (state.Severed && !tree.Falling)
                {
                    tree.Falling = true;
                    tree.FallStartTime = Time.time;
                    uint seed = tree.Instance.Seed == 0 ? 1u : tree.Instance.Seed;
                    float angle = (seed & 0xFFFFu) * (Mathf.PI * 2f / 65535f);
                    tree.FallAxis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
            }
        }

        private void UpdateFallingTrees()
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (!tree.Falling || tree.Root == null) continue;

                float t = Mathf.Clamp01((Time.time - tree.FallStartTime) / 1.25f);
                float angle = Mathf.SmoothStep(0f, 88f, t);
                tree.Root.transform.localRotation = Quaternion.AngleAxis(angle, tree.FallAxis);
            }
        }

        private void ClearGenerated()
        {
            for (int i = 0; i < _trees.Count; i++)
                if (_trees[i].Root != null) Destroy(_trees[i].Root);
            _trees.Clear();

            for (int i = 0; i < _meshes.Count; i++)
                if (_meshes[i] != null) Destroy(_meshes[i]);
            _meshes.Clear();
        }

        private void OnDestroy()
        {
            ClearGenerated();
            if (_barkMaterial != null) Destroy(_barkMaterial);
            if (_leafMaterial != null) Destroy(_leafMaterial);
            if (s_Instance == this) s_Instance = null;
        }
    }
}
