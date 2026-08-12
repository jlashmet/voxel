using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree state. Healthy trees are data-only records whose
    /// geometry lives in spatial batches. A per-tree GameObject/LOD/mesh presentation is created
    /// only when a tree leaves the healthy batch path because it is damaged.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private const float BatchSizeMetres = 32f;
        private const float HealthyDamageEpsilon = 0.0001f;

        private sealed class TreePresentation
        {
            public int Index;
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
            public Vector2Int Key;
            public GameObject Root;
            public Mesh[] LodMeshes;
            public readonly List<int> TreeIndices = new();
        }

        private static ProceduralTreeRenderer s_Instance;
        private static readonly int s_Damage = Shader.PropertyToID("_Damage");

        private readonly List<TreePresentation> _trees = new();
        private readonly List<BatchPresentation> _batches = new();
        private readonly Dictionary<Vector2Int, BatchPresentation> _batchByKey = new();
        private readonly HashSet<Vector2Int> _dirtyBatchKeys = new();
        private readonly List<int> _cellTreeIndices = new();
        private readonly List<int> _filteredBarkIndices = new(16384);
        private readonly List<int> _filteredLeafIndices = new(16384);
        private MaterialPropertyBlock _damageProperties;
        private bool _snapshotDirty = true;
        private bool _damageDirty = true;

        public double LastRebuildMilliseconds { get; private set; }
        public int LastDamageBatchRebuildCount { get; private set; }
        public int PresentationCount => _trees.Count;
        public int DynamicPresentationCount { get; private set; }
        public int DynamicMeshCount => DynamicPresentationCount * 3;
        public int BatchCount => _batches.Count;
        public int BatchedTreeCount { get; private set; }
        public int BatchMeshCount => _batches.Count * 3;
        public int GeneratedMeshCount => DynamicMeshCount + BatchMeshCount;
        public long TotalTriangleCountAllLods { get; private set; }
        public int ResidentRenderObjectCount => (DynamicPresentationCount + BatchCount) * 4;
        public int EstimatedVisibleDrawCount => (DynamicPresentationCount + BatchCount) * 2;

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

        public bool TryGetDynamicPresentationRoot(int treeIndex, out Transform root)
        {
            root = null;
            if ((uint)treeIndex >= (uint)_trees.Count) return false;
            GameObject go = _trees[treeIndex].Root;
            if (go == null) return false;
            root = go.transform;
            return true;
        }

        public bool TryGetTreeBounds(int treeIndex, out Bounds bounds)
        {
            bounds = default;
            if ((uint)treeIndex >= (uint)_trees.Count) return false;

            TreePresentation tree = _trees[treeIndex];
            Vector3 root = (Vector3)tree.Instance.PositionMetres;
            bool hasPoint = false;
            Vector3 min = root;
            Vector3 max = root;

            for (int i = 0; i < tree.Skeleton.Branches.Count; i++)
            {
                if (tree.ResolvedRemovedBranches.Contains(i)) continue;
                TreeBranchSegment branch = tree.Skeleton.Branches[i];
                float radius = Mathf.Max(branch.RadiusStart, branch.RadiusEnd);
                Vector3 r = Vector3.one * radius;
                Vector3 a = root + (Vector3)branch.Start;
                Vector3 b = root + (Vector3)branch.End;
                Vector3 branchMin = Vector3.Min(a, b) - r;
                Vector3 branchMax = Vector3.Max(a, b) + r;
                if (!hasPoint)
                {
                    min = branchMin;
                    max = branchMax;
                    hasPoint = true;
                }
                else
                {
                    min = Vector3.Min(min, branchMin);
                    max = Vector3.Max(max, branchMax);
                }
            }

            for (int i = 0; i < tree.Skeleton.Leaves.Count; i++)
            {
                int parent = tree.Skeleton.LeafParents != null && i < tree.Skeleton.LeafParents.Length
                    ? tree.Skeleton.LeafParents[i] : -1;
                if (parent >= 0 && tree.ResolvedRemovedBranches.Contains(parent)) continue;
                TreeLeafAnchor leaf = tree.Skeleton.Leaves[i];
                Vector3 p = root + (Vector3)leaf.Position;
                Vector3 r = Vector3.one * Mathf.Max(0.05f, leaf.Size);
                if (!hasPoint)
                {
                    min = p - r;
                    max = p + r;
                    hasPoint = true;
                }
                else
                {
                    min = Vector3.Min(min, p - r);
                    max = Vector3.Max(max, p + r);
                }
            }

            if (!hasPoint)
            {
                bounds = new Bounds(root, Vector3.one * 0.1f);
                return true;
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private void Rebuild()
        {
            var stopwatch = Stopwatch.StartNew();
            ClearGenerated();
            TotalTriangleCountAllLods = 0;

            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                ProceduralTreeSkeleton skeleton = ProceduralTreeSkeletonBuilder.Generate(in instance);
                var presentation = new TreePresentation
                {
                    Index = i,
                    Instance = instance,
                    Skeleton = skeleton,
                };

                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                    skeleton, directCuts, presentation.ResolvedRemovedBranches);
                presentation.DirectCutCount = directCuts.Count;
                TotalTriangleCountAllLods += EstimateTriangleCountAllLods(skeleton);
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

        private static Vector2Int BatchKeyFor(in TreeInstance instance)
        {
            Vector3 position = (Vector3)instance.PositionMetres;
            return new Vector2Int(
                Mathf.FloorToInt(position.x / BatchSizeMetres),
                Mathf.FloorToInt(position.z / BatchSizeMetres));
        }

        private void EnsureDynamicPresentation(TreePresentation tree)
        {
            if (tree.Root != null) return;

            var root = new GameObject($"Tree {tree.Index:000} {tree.Instance.Species}")
            {
                hideFlags = HideFlags.DontSave,
            };
            root.transform.SetParent(transform, false);
            root.transform.position = (Vector3)tree.Instance.PositionMetres;
            root.transform.localRotation = Quaternion.identity;

            tree.Root = root;
            tree.LodFilters = new MeshFilter[3];
            tree.LodRenderers = new MeshRenderer[3];
            tree.LodMeshes = new Mesh[3];
            tree.BaseBarkIndices = new int[3][];
            tree.BaseLeafIndices = new int[3][];
            tree.BarkIndexOwners = new int[3][];
            tree.LeafIndexOwners = new int[3][];

            for (int lod = 0; lod < 3; lod++)
            {
                var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(root.transform, false);
                var filter = child.AddComponent<MeshFilter>();
                var renderer = child.AddComponent<MeshRenderer>();
                ConfigureRenderer(renderer);
                tree.LodFilters[lod] = filter;
                tree.LodRenderers[lod] = renderer;
            }

            var group = root.AddComponent<LODGroup>();
            group.fadeMode = LODFadeMode.None;
            group.SetLODs(CreateLods(tree.LodRenderers));

            BuildDynamicTreeMeshes(tree);
            if (tree.ResolvedRemovedBranches.Count > 0)
                ApplyRemovedGeometry(tree);
            ApplyDamageMaterial(tree);
            group.RecalculateBounds();
            DynamicPresentationCount++;
        }

        private void DestroyDynamicPresentation(TreePresentation tree)
        {
            if (tree.Root == null) return;
            tree.Root.SetActive(false);

            if (tree.LodMeshes != null)
            {
                for (int lod = 0; lod < tree.LodMeshes.Length; lod++)
                    if (tree.LodMeshes[lod] != null) Destroy(tree.LodMeshes[lod]);
            }
            Destroy(tree.Root);
            tree.Root = null;
            tree.LodFilters = null;
            tree.LodRenderers = null;
            tree.LodMeshes = null;
            tree.BaseBarkIndices = null;
            tree.BaseLeafIndices = null;
            tree.BarkIndexOwners = null;
            tree.LeafIndexOwners = null;
            DynamicPresentationCount = Mathf.Max(0, DynamicPresentationCount - 1);
        }

        private void BuildDynamicTreeMeshes(TreePresentation tree)
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
            }
        }

        private void RebuildBatches()
        {
            ClearBatches();

            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            var groups = new Dictionary<Vector2Int, List<int>>();
            for (int i = 0; i < _trees.Count; i++)
            {
                if (!IsHealthyForBatch(i, damage)) continue;

                Vector2Int key = BatchKeyFor(_trees[i].Instance);
                if (!groups.TryGetValue(key, out List<int> treeIndices))
                {
                    treeIndices = new List<int>();
                    groups.Add(key, treeIndices);
                }
                treeIndices.Add(i);
            }

            foreach (KeyValuePair<Vector2Int, List<int>> pair in groups)
                BuildBatch(pair.Key, pair.Value);

            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (tree.IsBatched)
                    DestroyDynamicPresentation(tree);
                else
                    EnsureDynamicPresentation(tree);
            }
        }

        private bool IsHealthyForBatch(int treeIndex,
                                       IReadOnlyList<TreeWorldState.TreeDamageState> damage)
        {
            if (TreeWorldState.RemovedBranches(treeIndex).Count > 0) return false;
            if (treeIndex >= damage.Count) return true;
            if (damage[treeIndex].Severed) return false;
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
            root.transform.localRotation = Quaternion.identity;

            var batch = new BatchPresentation
            {
                Key = key,
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
                _trees[treeIndices[i]].IsBatched = true;

            BatchedTreeCount += treeIndices.Count;
            _batches.Add(batch);
            _batchByKey[key] = batch;
        }

        private Mesh BuildCombinedBatchMesh(List<int> treeIndices, int lod,
                                            Vector3 batchOrigin, Vector2Int key)
        {
            var barkParts = new CombineInstance[treeIndices.Count];
            var leafParts = new CombineInstance[treeIndices.Count];
            var temporarySourceMeshes = new Mesh[treeIndices.Count];

            for (int i = 0; i < treeIndices.Count; i++)
            {
                TreePresentation tree = _trees[treeIndices[i]];
                Mesh source = ProceduralTreeMeshBuilder.BuildMesh(tree.Skeleton, lod);
                source.hideFlags = HideFlags.DontSave;
                temporarySourceMeshes[i] = source;

                Vector3 offset = (Vector3)tree.Instance.PositionMetres - batchOrigin;
                Matrix4x4 matrix = Matrix4x4.Translate(offset);
                barkParts[i] = new CombineInstance
                {
                    mesh = source,
                    subMeshIndex = 0,
                    transform = matrix,
                };
                leafParts[i] = new CombineInstance
                {
                    mesh = source,
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
            for (int i = 0; i < temporarySourceMeshes.Length; i++)
                if (temporarySourceMeshes[i] != null) Destroy(temporarySourceMeshes[i]);
            return combined;
        }

        private static long EstimateTriangleCountAllLods(ProceduralTreeSkeleton skeleton)
        {
            long total = 0;
            for (int lod = 0; lod < 3; lod++)
            {
                int radialSides = lod == 0 ? 8 : lod == 1 ? 5 : 3;
                for (int i = 0; i < skeleton.Branches.Count; i++)
                {
                    TreeBranchSegment branch = skeleton.Branches[i];
                    if (lod == 2 && branch.Level >= 3 && branch.RadiusStart < 0.035f) continue;
                    total += radialSides * 2L;
                }

                int leafStride = lod == 0 ? 1 : lod == 1 ? 2 : 4;
                int leafPlanes = lod < 2 ? 2 : 1;
                for (int i = 0; i < skeleton.Leaves.Count; i += leafStride)
                    total += leafPlanes * 2L;
            }
            return total;
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
            if (tree.LodMeshes == null) return;
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
                mesh.RecalculateBounds();
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

        private void ApplyDamageMaterial(TreePresentation tree)
        {
            if (tree.LodRenderers == null) return;
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            float damageAmount = tree.Index < damage.Count
                ? 1f - Mathf.Clamp01(damage[tree.Index].FoliageHealth)
                : 0f;
            if (_damageProperties == null) _damageProperties = new MaterialPropertyBlock();
            _damageProperties.Clear();
            _damageProperties.SetFloat(s_Damage, damageAmount);
            for (int lod = 0; lod < tree.LodRenderers.Length; lod++)
            {
                MeshRenderer renderer = tree.LodRenderers[lod];
                if (renderer != null) renderer.SetPropertyBlock(_damageProperties, 1);
            }
        }

        private void ApplyDamage()
        {
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            int count = Mathf.Min(_trees.Count, damage.Count);
            _dirtyBatchKeys.Clear();

            for (int i = 0; i < count; i++)
            {
                TreePresentation tree = _trees[i];
                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(i);
                bool geometryChanged = tree.DirectCutCount != directCuts.Count;
                if (geometryChanged)
                {
                    ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                        tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
                    tree.DirectCutCount = directCuts.Count;
                }

                float damageAmount = 1f - Mathf.Clamp01(damage[i].FoliageHealth);
                bool shouldBatch = directCuts.Count == 0
                    && !damage[i].Severed
                    && damageAmount <= HealthyDamageEpsilon;
                if (tree.IsBatched != shouldBatch)
                    _dirtyBatchKeys.Add(BatchKeyFor(tree.Instance));

                if (!tree.IsBatched)
                {
                    EnsureDynamicPresentation(tree);
                    if (geometryChanged) ApplyRemovedGeometry(tree);
                    ApplyDamageMaterial(tree);
                }
            }

            LastDamageBatchRebuildCount = 0;
            if (_dirtyBatchKeys.Count == 0) return;

            foreach (Vector2Int key in _dirtyBatchKeys)
            {
                RebuildBatchCell(key, damage);
                LastDamageBatchRebuildCount++;
            }

            for (int i = 0; i < count; i++)
            {
                TreePresentation tree = _trees[i];
                if (tree.IsBatched) continue;
                if (tree.DirectCutCount > 0) ApplyRemovedGeometry(tree);
                ApplyDamageMaterial(tree);
            }
        }

        private void RebuildBatchCell(Vector2Int key,
                                      IReadOnlyList<TreeWorldState.TreeDamageState> damage)
        {
            if (_batchByKey.TryGetValue(key, out BatchPresentation oldBatch))
            {
                for (int i = 0; i < oldBatch.TreeIndices.Count; i++)
                {
                    int treeIndex = oldBatch.TreeIndices[i];
                    if ((uint)treeIndex >= (uint)_trees.Count) continue;
                    if (!_trees[treeIndex].IsBatched) continue;
                    _trees[treeIndex].IsBatched = false;
                    BatchedTreeCount = Mathf.Max(0, BatchedTreeCount - 1);
                }

                DestroyBatchObjects(oldBatch);
                _batchByKey.Remove(key);
                _batches.Remove(oldBatch);
            }

            _cellTreeIndices.Clear();
            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (BatchKeyFor(tree.Instance) != key) continue;
                if (IsHealthyForBatch(i, damage)) _cellTreeIndices.Add(i);
            }

            if (_cellTreeIndices.Count > 0)
                BuildBatch(key, _cellTreeIndices);

            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (BatchKeyFor(tree.Instance) != key) continue;
                if (tree.IsBatched)
                    DestroyDynamicPresentation(tree);
                else
                    EnsureDynamicPresentation(tree);
            }
        }

        private void DestroyBatchObjects(BatchPresentation batch)
        {
            if (batch == null) return;
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

        private void ClearBatches()
        {
            for (int i = 0; i < _batches.Count; i++)
                DestroyBatchObjects(_batches[i]);
            _batches.Clear();
            _batchByKey.Clear();
            for (int i = 0; i < _trees.Count; i++)
                _trees[i].IsBatched = false;
            BatchedTreeCount = 0;
            LastDamageBatchRebuildCount = 0;
        }

        private void ClearGenerated()
        {
            ClearBatches();
            for (int i = 0; i < _trees.Count; i++)
                DestroyDynamicPresentation(_trees[i]);
            _trees.Clear();
            DynamicPresentationCount = 0;
        }

        private void OnDestroy()
        {
            ClearGenerated();
            if (s_Instance == this) s_Instance = null;
        }
    }
}
