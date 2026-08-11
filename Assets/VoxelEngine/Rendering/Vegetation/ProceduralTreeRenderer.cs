using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree instances. This deliberately uses ordinary Unity
    /// meshes/LODGroup for the first milestone; the semantic registry and shared-skeleton mesher
    /// can later feed GPU instancing without changing world generation or species definitions.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private static ProceduralTreeRenderer s_Instance;

        private readonly List<GameObject> _treeRoots = new();
        private readonly List<Mesh> _meshes = new();
        private Material _barkMaterial;
        private Material _leafMaterial;
        private Material[] _sharedMaterials;
        private int _seenVersion = int.MinValue;

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
            if (_seenVersion == version) return;
            _seenVersion = version;
            Rebuild();
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
                root.transform.position = instance.PositionMetres;
                _treeRoots.Add(root);

                var lodRenderers = new Renderer[3];
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
                    new LOD(0.34f, new[] { lodRenderers[0] }),
                    new LOD(0.13f, new[] { lodRenderers[1] }),
                    new LOD(0.025f, new[] { lodRenderers[2] }),
                });
                group.RecalculateBounds();
            }
        }

        private void ClearGenerated()
        {
            for (int i = 0; i < _treeRoots.Count; i++)
                if (_treeRoots[i] != null) Destroy(_treeRoots[i]);
            _treeRoots.Clear();

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
