using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree instances. Geometry is generated once per semantic
    /// tree snapshot. Destruction never regenerates vertices: branch ownership is derived from the
    /// deterministic mesh layout and damage only rewrites the affected mesh index lists.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private const float RecoveryRenderChunkMetres = 12.8f;
        private const float LegacyProxyBoundsPadding = 0.75f;
        private const float FallDuration = 1.25f;
        private const float FallenHoldDuration = 0.75f;

        private sealed class TreePresentation
        {
            public TreeInstance Instance;
            public ProceduralTreeMeshBuilder.TreeSkeleton Skeleton;
            public GameObject Root;
            public MeshFilter[] LodFilters;
            public MeshRenderer[] LodRenderers;
            public Mesh[] LodMeshes;

            // Immutable topology captured from the full generated meshes. Owners run parallel to
            // the corresponding index array and identify which semantic branch owns each triangle.
            // Damage filters these arrays into the live submeshes without touching vertex data.
            public int[][] BaseBarkIndices;
            public int[][] BaseLeafIndices;
            public int[][] BarkIndexOwners;
            public int[][] LeafIndexOwners;

            public readonly HashSet<int> ResolvedRemovedBranches = new();
            public int DirectCutCount;
            public bool Falling;
            public bool Retired;
            public float FallStartTime;
            public Vector3 FallAxis;
            public int3 RenderChunkMin;
            public int3 RenderChunkMax;
        }

        private static ProceduralTreeRenderer s_Instance;
        private static readonly int s_Damage = Shader.PropertyToID("_Damage");

        private readonly List<TreePresentation> _trees = new();
        private readonly List<int> _filteredBarkIndices = new(16384);
        private readonly List<int> _filteredLeafIndices = new(16384);
        private MaterialPropertyBlock _damageProperties;
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
            _damageProperties = new MaterialPropertyBlock();
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

            UpdateFallbackVisibility();
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
                CalculateRenderChunkBounds(in instance, skeleton,
                    out int3 renderChunkMin, out int3 renderChunkMax);

                var root = new GameObject($"Tree {i:000} {instance.Species}")
                {
                    hideFlags = HideFlags.DontSave,
                };
                root.transform.SetParent(transform, false);
                root.transform.position = (Vector3)instance.PositionMetres;

                var presentation = new TreePresentation
                {
                    Instance = instance,
                    Skeleton = skeleton,
                    Root = root,
                    LodFilters = new MeshFilter[3],
                    LodRenderers = new MeshRenderer[3],
                    LodMeshes = new Mesh[3],
                    BaseBarkIndices = new int[3][],
                    BaseLeafIndices = new int[3][],
                    BarkIndexOwners = new int[3][],
                    LeafIndexOwners = new int[3][],
                    Falling = false,
                    Retired = false,
                    RenderChunkMin = renderChunkMin,
                    RenderChunkMax = renderChunkMax,
                };

                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                    child.transform.SetParent(root.transform, false);
                    var filter = child.AddComponent<MeshFilter>();
                    var renderer = child.AddComponent<MeshRenderer>();
                    renderer.sharedMaterials = _sharedMaterials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                    renderer.lightProbeUsage = LightProbeUsage.Off;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    presentation.LodFilters[lod] = filter;
                    presentation.LodRenderers[lod] = renderer;
                }

                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(new[]
                {
                    new LOD(0.34f, new Renderer[] { presentation.LodRenderers[0] }),
                    new LOD(0.13f, new Renderer[] { presentation.LodRenderers[1] }),
                    new LOD(0.025f, new Renderer[] { presentation.LodRenderers[2] }),
                });

                BuildTreeMeshes(presentation);

                IReadOnlyCollection<int> directCuts = ProceduralTreeRegistry.RemovedBranches(i);
                ProceduralTreeMeshBuilder.ResolveRemovedBranches(
                    skeleton, directCuts, presentation.ResolvedRemovedBranches);
                presentation.DirectCutCount = directCuts.Count;
                if (presentation.ResolvedRemovedBranches.Count > 0)
                    ApplyRemovedGeometry(presentation);

                group.RecalculateBounds();
                _trees.Add(presentation);
            }
        }

        private void BuildTreeMeshes(TreePresentation tree)
        {
            for (int lod = 0; lod < 3; lod++)
            {
                // Always generate the complete immutable vertex set. Destruction changes only the
                // index buffers below, so repeated hits never allocate/regenerate tree geometry.
                Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(tree.Skeleton, lod);
                mesh.name = $"{tree.Instance.Species}_{tree.Instance.Seed}_LOD{lod}";
                mesh.hideFlags = HideFlags.DontSave;
                mesh.MarkDynamic();
                tree.LodMeshes[lod] = mesh;
                tree.LodFilters[lod].sharedMesh = mesh;

                int[] bark = mesh.GetTriangles(0);
                int[] leaves = mesh.GetTriangles(1);
                tree.BaseBarkIndices[lod] = bark;
                tree.BaseLeafIndices[lod] = leaves;
                tree.BarkIndexOwners[lod] = BuildBarkOwners(tree.Skeleton, lod, bark.Length);
                tree.LeafIndexOwners[lod] = BuildLeafOwners(tree.Skeleton, lod, leaves.Length);
            }
        }

        private static int[] BuildBarkOwners(ProceduralTreeMeshBuilder.TreeSkeleton skeleton,
                                             int lod, int indexCount)
        {
            int radialSides = lod == 0 ? 8 : lod == 1 ? 5 : 3;
            int indicesPerBranch = radialSides * 6;
            var owners = new int[indexCount];
            int cursor = 0;

            for (int branchIndex = 0;
                 branchIndex < skeleton.Branches.Count && cursor < indexCount;
                 branchIndex++)
            {
                ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[branchIndex];
                if (lod == 2 && branch.Level >= 3 && branch.RadiusStart < 0.035f) continue;

                int end = Mathf.Min(indexCount, cursor + indicesPerBranch);
                for (; cursor < end; cursor++) owners[cursor] = branchIndex;
            }

            // A mismatch should never happen because this mirrors BuildMesh's deterministic layout.
            // Treat any future unrecognised tail as unowned/always-visible rather than deleting it.
            for (; cursor < indexCount; cursor++) owners[cursor] = -1;
            return owners;
        }

        private static int[] BuildLeafOwners(ProceduralTreeMeshBuilder.TreeSkeleton skeleton,
                                             int lod, int indexCount)
        {
            int leafStride = lod == 0 ? 1 : lod == 1 ? 2 : 4;
            int leafPlanes = lod < 2 ? 2 : 1;
            int indicesPerLeaf = leafPlanes * 6;
            var owners = new int[indexCount];
            int cursor = 0;

            for (int leafIndex = 0;
                 leafIndex < skeleton.Leaves.Count && cursor < indexCount;
                 leafIndex += leafStride)
            {
                int parent = skeleton.LeafParents != null
                          && leafIndex < skeleton.LeafParents.Length
                    ? skeleton.LeafParents[leafIndex] : -1;
                int end = Mathf.Min(indexCount, cursor + indicesPerLeaf);
                for (; cursor < end; cursor++) owners[cursor] = parent;
            }

            for (; cursor < indexCount; cursor++) owners[cursor] = -1;
            return owners;
        }

        private void ApplyRemovedGeometry(TreePresentation tree)
        {
            for (int lod = 0; lod < tree.LodMeshes.Length; lod++)
            {
                Mesh mesh = tree.LodMeshes[lod];
                if (mesh == null) continue;

                FilterIndices(tree.BaseBarkIndices[lod], tree.BarkIndexOwners[lod],
                              tree.ResolvedRemovedBranches, _filteredBarkIndices);
                FilterIndices(tree.BaseLeafIndices[lod], tree.LeafIndexOwners[lod],
                              tree.ResolvedRemovedBranches, _filteredLeafIndices);

                // calculateBounds=false is intentional. Damage only removes geometry, so the
                // original bounds remain a conservative valid LOD/culling bound and avoid another
                // CPU walk over the mesh on every hit.
                mesh.SetTriangles(_filteredBarkIndices, 0, false);
                mesh.SetTriangles(_filteredLeafIndices, 1, false);
            }
        }

        private static void FilterIndices(int[] source, int[] owners, HashSet<int> removed,
                                          List<int> destination)
        {
            destination.Clear();
            if (source == null) return;

            for (int i = 0; i < source.Length; i++)
            {
                int owner = owners != null && i < owners.Length ? owners[i] : -1;
                if (owner >= 0 && removed.Contains(owner)) continue;
                destination.Add(source[i]);
            }
        }

        private void ApplyDamage()
        {
            IReadOnlyList<ProceduralTreeRegistry.TreeDamageState> damage =
                ProceduralTreeRegistry.Damage;
            int count = Mathf.Min(_trees.Count, damage.Count);

            if (_damageProperties == null) _damageProperties = new MaterialPropertyBlock();

            for (int i = 0; i < count; i++)
            {
                TreePresentation tree = _trees[i];
                IReadOnlyCollection<int> directCuts = ProceduralTreeRegistry.RemovedBranches(i);
                if (tree.DirectCutCount != directCuts.Count)
                {
                    ProceduralTreeMeshBuilder.ResolveRemovedBranches(
                        tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
                    tree.DirectCutCount = directCuts.Count;
                    ApplyRemovedGeometry(tree);
                }

                ProceduralTreeRegistry.TreeDamageState state = damage[i];
                float damageAmount = 1f - Mathf.Clamp01(state.FoliageHealth);

                _damageProperties.Clear();
                _damageProperties.SetFloat(s_Damage, damageAmount);
                for (int lod = 0; lod < tree.LodRenderers.Length; lod++)
                {
                    MeshRenderer renderer = tree.LodRenderers[lod];
                    if (renderer != null)
                        renderer.SetPropertyBlock(_damageProperties, 1);
                }

                if (state.Severed && !tree.Falling && !tree.Retired)
                {
                    tree.Falling = true;
                    tree.FallStartTime = Time.time;
                    uint seed = tree.Instance.Seed == 0 ? 1u : tree.Instance.Seed;
                    float angle = (seed & 0xFFFFu) * (Mathf.PI * 2f / 65535f);
                    tree.FallAxis = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
            }
        }

        /// <summary>
        /// Surface Nets is still allowed to cover terrain while Transvoxel warms up, but it also
        /// contains the showcase's old voxel tree proxy. Never draw the semantic tree at the same
        /// time as that coarse proxy. Once every render chunk touched by this tree has handed off,
        /// the exact same procedural root becomes visible; no geometry is rebuilt or cloned.
        /// </summary>
        private void UpdateFallbackVisibility()
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (tree.Root == null) continue;

                bool coarseProxyVisible = false;
                for (int z = tree.RenderChunkMin.z;
                     z <= tree.RenderChunkMax.z && !coarseProxyVisible; z++)
                for (int y = tree.RenderChunkMin.y;
                     y <= tree.RenderChunkMax.y && !coarseProxyVisible; y++)
                for (int x = tree.RenderChunkMin.x;
                     x <= tree.RenderChunkMax.x; x++)
                {
                    if (!ProceduralTreeRegistry.IsCoarseLegacyProxyRenderChunk(new int3(x, y, z)))
                        continue;
                    coarseProxyVisible = true;
                    break;
                }

                bool shouldBeActive = !tree.Retired && !coarseProxyVisible;
                if (tree.Root.activeSelf != shouldBeActive)
                    tree.Root.SetActive(shouldBeActive);
            }
        }

        private void UpdateFallingTrees()
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (!tree.Falling || tree.Retired || tree.Root == null) continue;

                float elapsed = Time.time - tree.FallStartTime;
                if (elapsed >= FallDuration + FallenHoldDuration)
                {
                    tree.Retired = true;
                    tree.Root.SetActive(false);
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / FallDuration);
                float angle = Mathf.SmoothStep(0f, 88f, t);
                tree.Root.transform.localRotation = Quaternion.AngleAxis(angle, tree.FallAxis);
            }
        }

        private static void CalculateRenderChunkBounds(
            in TreeInstance instance, ProceduralTreeMeshBuilder.TreeSkeleton skeleton,
            out int3 chunkMin, out int3 chunkMax)
        {
            float3 root = instance.PositionMetres;
            float3 min = root;
            float3 max = root;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                ProceduralTreeMeshBuilder.BranchSegment branch = skeleton.Branches[i];
                float radius = math.max(branch.RadiusStart, branch.RadiusEnd);
                float3 r = new(radius);
                min = math.min(min, root + math.min(branch.Start, branch.End) - r);
                max = math.max(max, root + math.max(branch.Start, branch.End) + r);
            }

            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                ProceduralTreeMeshBuilder.LeafAnchor leaf = skeleton.Leaves[i];
                float3 r = new(math.max(0.05f, leaf.Size));
                min = math.min(min, root + leaf.Position - r);
                max = math.max(max, root + leaf.Position + r);
            }

            min -= LegacyProxyBoundsPadding;
            max += LegacyProxyBoundsPadding;
            chunkMin = (int3)math.floor(min / RecoveryRenderChunkMetres);
            chunkMax = (int3)math.floor(max / RecoveryRenderChunkMetres);
        }

        private void ClearGenerated()
        {
            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (tree.LodMeshes != null)
                {
                    for (int lod = 0; lod < tree.LodMeshes.Length; lod++)
                        if (tree.LodMeshes[lod] != null) Destroy(tree.LodMeshes[lod]);
                }
                if (tree.Root != null) Destroy(tree.Root);
            }
            _trees.Clear();
            _filteredBarkIndices.Clear();
            _filteredLeafIndices.Clear();
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
