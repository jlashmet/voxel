using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree state. Healthy standing trees are combined into
    /// spatial render batches. Every tree retains its per-tree meshes as a dormant dynamic
    /// representation so damage can immediately move that tree out of the batch without changing
    /// semantic state or destruction behavior.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private const float BatchSizeMetres = 32f;
        private const int MinimumTreesPerBatch = 2;
        private const float HealthyDamageEpsilon = 0.0001f;

        private sealed class TreePresentation
        {
            public TreeInstance Instance;
            public ProceduralTreeSkeleton Skeleton;
            public GameObject Root;
            public MeshFilter[] LodFilters;
            public MeshRenderer[] LodRenderers;
            public Mesh[] LodMeshes;
            public int[][] BaseBarkIndices;
            public int[][] BaseLeafIndices;
            public int[][] BarkIndexOwners;
            public int[][] LeafIndexOwners;
            public readonly HashSet<int> ResolvedRemovedBranches = new();
            public int DirectCutCount;
            public bool IsBatched;
        }

        private sealed class BatchPresentation
        {
            public GameObject Root;
            public Mesh[] LodMeshes;
            public readonly List<int> TreeIndices = new();
        }

        private static ProceduralTreeRenderer s_Instance;
        private static readonly int s_Damage = Shader.PropertyToID("_Damage");

        private readonly List<TreePresentation> _trees = new();
        private readonly List<BatchPresentation> _batches = new();
        private readonly List<int> _filteredBarkIndices = new(16384);
        private readonly List<int> _filteredLeafIndices = new(16384);
        private MaterialPropertyBlock _damageProperties;
        private bool _snapshotDirty = true;
        private bool _damageDirty = true;

        public double LastRebuildMilliseconds { get; private set; }
        public int PresentationCount => _trees.Count;
        public int GeneratedMeshCount { get; private set; }
        public long TotalTriangleCountAllLods { get; private set; }
        public int BatchCount => _batches.Count;
        public int BatchedTreeCount { get; private set; }
        public int BatchMeshCount => _batches.Count * 3;
        public int EstimatedVisibleDrawCount => (_batches.Count + (_trees.Count - BatchedTreeCount)) * 2;

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

        private void OnEnable()
        {
            TreeWorldState.SnapshotChanged += OnSnapshotChanged;
            TreeWorldState.BranchCut += OnBranchCut;
            TreeWorldState.DamageChanged += OnDamageChanged;
            _snapshotDirty = true;
            _damageDirty = true;
        }

        private void OnDisable()
        {
            TreeWorldState.SnapshotChanged -= OnSnapshotChanged;
            TreeWorldState.BranchCut -= OnBranchCut;
            TreeWorldState.DamageChanged -= OnDamageChanged;
        }

        private void OnSnapshotChanged()
        {
            _snapshotDirty = true;
            _damageDirty = true;
        }

        private void OnBranchCut(TreeBranchCutEvent _) => _damageDirty = true;
        private void OnDamageChanged(TreeDamageChangedEvent _) => _damageDirty = true;

        private void Update()
        {
            if (!ProceduralTreeMaterials.Ensure()) return;
            ProceduralTreeMaterials.ApplyLighting();

            if (_snapshotDirty)
            {
                _snapshotDirty = false;
                Rebuild();
                _damageDirty = true;
            }

            if (_damageDirty)
            {
                _damageDirty = false;
                ApplyDamage();
            }
        }

        private void Rebuild()
        {
            var stopwatch = Stopwatch.StartNew();
            ClearGenerated();
            GeneratedMeshCount = 0;
            TotalTriangleCountAllLods = 0;

            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);

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
                };

                for (int lod = 0; lod < 3; lod++)
                {
                    var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                    child.transform.SetParent(root.transform, false);
                    var filter = child.AddComponent<MeshFilter>();
                    var renderer = child.AddComponent<MeshRenderer>();
                    ConfigureRenderer(renderer);
                    presentation.LodFilters[lod] = filter;
                    presentation.LodRenderers[lod] = renderer;
                }

                var group = root.AddComponent<LODGroup>();
                group.fadeMode = LODFadeMode.None;
                group.SetLODs(CreateLods(presentation.LodRenderers));

                BuildTreeMeshes(presentation);

                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                    skeleton, directCuts, presentation.ResolvedRemovedBranches);
                presentation.DirectCutCount = directCuts.Count;
                if (presentation.ResolvedRemovedBranches.Count > 0)
                    ApplyRemovedGeometry(presentation);

                group.RecalculateBounds();
                _trees.Add(presentation);
            }

            RebuildBatches();

            stopwatch.Stop();
            LastRebuildMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
        }

        private static LOD[] CreateLods(MeshRenderer[] renderers) => new[]
        {
            new LOD(0.34f, new Renderer[] { renderers[0] }),
            new LOD(0.13f, new Renderer[] { renderers[1] }),
            new LOD(0.025f, new Renderer[] { renderers[2] }),
        };

        private static void ConfigureRenderer(MeshRenderer renderer)
        {
            renderer.sharedMaterials = ProceduralTreeMaterials.Shared;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private void BuildTreeMeshes(TreePresentation tree)
        {
            for (int lod = 0; lod < 3; lod++)
            {
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
                GeneratedMeshCount++;
                TotalTriangleCountAllLods += (bark.Length + leaves.Length) / 3L;
            }
        }

        private void RebuildBatches()
        {
            ClearBatches();
            BatchedTreeCount = 0;

            for (int i = 0; i < _trees.Count; i++)
            {
                _trees[i].IsBatched = false;
                SetTreeRendererEnabled(_trees[i], true);
            }

            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            var groups = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < _trees.Count; i++)
            {
                if (!IsHealthyForBatch(i, damage)) continue;

                Vector3 position = (Vector3)_trees[i].Instance.PositionMetres;
                var key = new Vector2Int(
                    Mathf.FloorToInt(position.x / BatchSizeMetres),
                    Mathf.FloorToInt(position.z / BatchSizeMetres));
                if (!groups.TryGetValue(key, out List<int> treeIndices))
                {
                    treeIndices = new List<int>();
                    groups.Add(key, treeIndices);
                }
                treeIndices.Add(i);
            }

            foreach (KeyValuePair<Vector2Int, List<int>> pair in groups)
            {
                if (pair.Value.Count < MinimumTreesPerBatch) continue;
                BuildBatch(pair.Key, pair.Value);
            }
        }

        private bool IsHealthyForBatch(int treeIndex,
                                       IReadOnlyList<TreeWorldState.TreeDamageState> damage)
        {
            if (TreeWorldState.RemovedBranches(treeIndex).Count > 0) return false;
            if (treeIndex >= damage.Count) return true;
            float damageAmount = 1f - Mathf.Clamp01(damage[treeIndex].FoliageHealth);
            return damageAmount <= HealthyDamageEpsilon;
        }

        private void BuildBatch(Vector2Int key, List<int> treeIndices)
        {
            Vector3 origin = new Vector3(key.x * BatchSizeMetres, 0f, key.y * BatchSizeMetres);
            var root = new GameObject($"Tree Batch {key.x},{key.y}")
            {
                hideFlags = HideFlags.DontSave,
            };
            root.transform.SetParent(transform, false);
            root.transform.position = origin;

            var batch = new BatchPresentation
            {
                Root = root,
                LodMeshes = new Mesh[3],
            };
            batch.TreeIndices.AddRange(treeIndices);

            var lodRenderers = new MeshRenderer[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(root.transform, false);
                var filter = child.AddComponent<MeshFilter>();
                var renderer = child.AddComponent<MeshRenderer>();
                ConfigureRenderer(renderer);

                Mesh mesh = BuildCombinedBatchMesh(treeIndices, lod, origin, key);
                filter.sharedMesh = mesh;
                batch.LodMeshes[lod] = mesh;
                lodRenderers[lod] = renderer;
            }

            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(CreateLods(lodRenderers));
            group.RecalculateBounds();

            for (int i = 0; i < treeIndices.Count; i++)
            {
                TreePresentation tree = _trees[treeIndices[i]];
                tree.IsBatched = true;
                SetTreeRendererEnabled(tree, false);
            }

            BatchedTreeCount += treeIndices.Count;
            _batches.Add(batch);
        }

        private Mesh BuildCombinedBatchMesh(List<int> treeIndices, int lod,
                                            Vector3 batchOrigin, Vector2Int key)
        {
            var barkParts = new CombineInstance[treeIndices.Count];
            var leafParts = new CombineInstance[treeIndices.Count];

            for (int i = 0; i < treeIndices.Count; i++)
            {
                TreePresentation tree = _trees[treeIndices[i]];
                Vector3 offset = (Vector3)tree.Instance.PositionMetres - batchOrigin;
                Matrix4x4 matrix = Matrix4x4.Translate(offset);
                barkParts[i] = new CombineInstance
                {
                    mesh = tree.LodMeshes[lod],
                    subMeshIndex = 0,
                    transform = matrix,
                };
                leafParts[i] = new CombineInstance
                {
                    mesh = tree.LodMeshes[lod],
                    subMeshIndex = 1,
                    transform = matrix,
                };
            }

            var barkMesh = new Mesh
            {
                name = $"TreeBatch_{key.x}_{key.y}_LOD{lod}_BarkTemp",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
            };
            var leafMesh = new Mesh
            {
                name = $"TreeBatch_{key.x}_{key.y}_LOD{lod}_LeavesTemp",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
            };
            barkMesh.CombineMeshes(barkParts, true, true, false);
            leafMesh.CombineMeshes(leafParts, true, true, false);

            var combined = new Mesh
            {
                name = $"TreeBatch_{key.x}_{key.y}_LOD{lod}",
                indexFormat = IndexFormat.UInt32,
                hideFlags = HideFlags.DontSave,
            };
            var materialParts = new[]
            {
                new CombineInstance { mesh = barkMesh, subMeshIndex = 0, transform = Matrix4x4.identity },
                new CombineInstance { mesh = leafMesh, subMeshIndex = 0, transform = Matrix4x4.identity },
            };
            combined.CombineMeshes(materialParts, false, true, false);
            combined.RecalculateBounds();

            Destroy(barkMesh);
            Destroy(leafMesh);
            return combined;
        }

        private static void SetTreeRendererEnabled(TreePresentation tree, bool enabled)
        {
            if (tree.LodRenderers == null) return;
            for (int lod = 0; lod < tree.LodRenderers.Length; lod++)
            {
                MeshRenderer renderer = tree.LodRenderers[lod];
                if (renderer != null) renderer.enabled = enabled;
            }
        }

        private static int[] BuildBarkOwners(ProceduralTreeSkeleton skeleton,
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
                TreeBranchSegment branch = skeleton.Branches[branchIndex];
                if (lod == 2 && branch.Level >= 3 && branch.RadiusStart < 0.035f) continue;

                int end = Mathf.Min(indexCount, cursor + indicesPerBranch);
                for (; cursor < end; cursor++) owners[cursor] = branchIndex;
            }

            for (; cursor < indexCount; cursor++) owners[cursor] = -1;
            return owners;
        }

        private static int[] BuildLeafOwners(ProceduralTreeSkeleton skeleton,
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
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            int count = Mathf.Min(_trees.Count, damage.Count);
            if (_damageProperties == null) _damageProperties = new MaterialPropertyBlock();
            bool batchesDirty = false;

            for (int i = 0; i < count; i++)
            {
                TreePresentation tree = _trees[i];
                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                if (tree.DirectCutCount != directCuts.Count)
                {
                    ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                        tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
                    tree.DirectCutCount = directCuts.Count;
                    ApplyRemovedGeometry(tree);
                    if (tree.IsBatched) batchesDirty = true;
                }

                float damageAmount = 1f - Mathf.Clamp01(damage[i].FoliageHealth);
                if (tree.IsBatched && damageAmount > HealthyDamageEpsilon)
                    batchesDirty = true;

                _damageProperties.Clear();
                _damageProperties.SetFloat(s_Damage, damageAmount);
                for (int lod = 0; lod < tree.LodRenderers.Length; lod++)
                {
                    MeshRenderer renderer = tree.LodRenderers[lod];
                    if (renderer != null)
                        renderer.SetPropertyBlock(_damageProperties, 1);
                }
            }

            if (batchesDirty)
                RebuildBatches();
        }

        private void ClearBatches()
        {
            for (int i = 0; i < _batches.Count; i++)
            {
                BatchPresentation batch = _batches[i];
                if (batch.LodMeshes != null)
                {
                    for (int lod = 0; lod < batch.LodMeshes.Length; lod++)
                        if (batch.LodMeshes[lod] != null) Destroy(batch.LodMeshes[lod]);
                }
                if (batch.Root != null)
                {
                    batch.Root.SetActive(false);
                    Destroy(batch.Root);
                }
            }
            _batches.Clear();
        }

        private void ClearGenerated()
        {
            ClearBatches();
            BatchedTreeCount = 0;
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
        }

        private void OnDestroy()
        {
            ClearGenerated();
            if (s_Instance == this) s_Instance = null;
        }
    }
}
