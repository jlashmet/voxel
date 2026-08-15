using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Vegetation.Api;
using TreeInstance = VoxelEngine.Vegetation.Api.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// Runtime presentation of semantic tree state. Healthy trees are data-only records whose
    /// geometry lives in spatial batches. Damage punches only the affected tree's index ranges out
    /// of its existing batch and lazily materializes that tree; neighbouring batch geometry is never
    /// regenerated on the destruction frame.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private const float BatchSizeMetres = 32f;
        private const float HealthyDamageEpsilon = 0.0001f;

        private sealed class TreePresentation
        {
            public int Index;
            public TreeInstance Instance;
            public TreeSkeletonSnapshot Skeleton;
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

        private sealed class BatchTreeRanges
        {
            public readonly int[] BarkStart = new int[3];
            public readonly int[] BarkCount = new int[3];
            public readonly int[] LeafStart = new int[3];
            public readonly int[] LeafCount = new int[3];
        }

        private sealed class BatchPresentation
        {
            public Vector2Int Key;
            public GameObject Root;
            public Mesh[] LodMeshes;
            public readonly List<int> TreeIndices = new();
            public readonly Dictionary<int, BatchTreeRanges> TreeRanges = new();
            public readonly HashSet<int> HiddenTreeIndices = new();
        }

        private sealed class BatchMeshBuffers
        {
            public readonly List<Vector3> Vertices = new(8192);
            public readonly List<Vector3> Normals = new(8192);
            public readonly List<Color> Colours = new(8192);
            public readonly List<Vector2> Uv0 = new(8192);
            public readonly List<Vector2> Uv1 = new(8192);
            public readonly List<int> BarkIndices = new(12288);
            public readonly List<int> LeafIndices = new(12288);
        }

        private readonly struct PendingDynamicLod
        {
            public readonly TreePresentation Tree;
            public readonly int Lod;

            public PendingDynamicLod(TreePresentation tree, int lod)
            {
                Tree = tree;
                Lod = lod;
            }
        }

        private static ProceduralTreeRenderer s_Instance;
        private static readonly int s_Damage = Shader.PropertyToID("_Damage");

        private readonly List<TreePresentation> _trees = new();
        private readonly List<BatchPresentation> _batches = new();
        private readonly Dictionary<Vector2Int, BatchPresentation> _batchByKey = new();
        private readonly HashSet<int> _dirtyTreeIndices = new();
        private readonly Queue<PendingDynamicLod> _pendingDynamicLods = new();
        private readonly List<int> _filteredBarkIndices = new(16384);
        private readonly List<int> _filteredLeafIndices = new(16384);
        private ushort[] _zeroIndices16 = System.Array.Empty<ushort>();
        private uint[] _zeroIndices32 = System.Array.Empty<uint>();
        private MaterialPropertyBlock _damageProperties;
        private bool _snapshotDirty = true;
        private bool _countSnapshotTriangles;

        public double LastRebuildMilliseconds { get; private set; }
        // Retained as a diagnostic/compatibility contract: runtime damage must leave this at zero.
        public int LastDamageBatchRebuildCount { get; private set; }
        public int LastDamageBatchReleaseCount { get; private set; }
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
        public int PeakResidentSkeletonCountDuringLastRebuild { get; private set; }
        public int ResidentSkeletonCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _trees.Count; i++)
                    if (_trees[i].Skeleton != null) count++;
                return count;
            }
        }

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
            TreeWorldReadRegistry.Current.SnapshotChanged += OnSnapshotChanged;
            TreeWorldReadRegistry.Current.BranchCut += OnBranchCut;
            TreeWorldReadRegistry.Current.DamageChanged += OnDamageChanged;
            _snapshotDirty = true;
        }

        private void OnDisable()
        {
            TreeWorldReadRegistry.Current.SnapshotChanged -= OnSnapshotChanged;
            TreeWorldReadRegistry.Current.BranchCut -= OnBranchCut;
            TreeWorldReadRegistry.Current.DamageChanged -= OnDamageChanged;
        }

        private void OnSnapshotChanged()
        {
            _snapshotDirty = true;
            _dirtyTreeIndices.Clear();
            _pendingDynamicLods.Clear();
        }

        private void OnBranchCut(TreeBranchCutEvent cut) => _dirtyTreeIndices.Add(cut.TreeIndex);
        private void OnDamageChanged(TreeDamageChangedEvent damage) =>
            _dirtyTreeIndices.Add(damage.TreeIndex);

        private void Update()
        {
            if (!ProceduralTreeMaterials.Ensure()) return;
            ProceduralTreeMaterials.ApplyLighting();

            if (_snapshotDirty)
            {
                _snapshotDirty = false;
                Rebuild();
                _dirtyTreeIndices.Clear();
                return;
            }

            // Lower dynamic LODs are intentionally amortized. The destruction frame builds only the
            // immediately visible LOD0; later frames fill one lower-detail LOD at a time.
            ProcessOnePendingDynamicLod();

            if (_dirtyTreeIndices.Count > 0)
                ApplyDamage();
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
            TreeSkeletonSnapshot skeleton = tree.Skeleton
                ?? TreeWorldReadRegistry.Current.SkeletonFor(in tree.Instance);
            Vector3 root = (Vector3)tree.Instance.PositionMetres;
            bool hasPoint = false;
            Vector3 min = root;
            Vector3 max = root;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (tree.ResolvedRemovedBranches.Contains(i)) continue;
                TreeBranchSegment branch = skeleton.Branches[i];
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

            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                int parent = skeleton.LeafParents != null && i < skeleton.LeafParents.Count
                    ? skeleton.LeafParents[i] : -1;
                if (parent >= 0 && tree.ResolvedRemovedBranches.Contains(parent)) continue;
                TreeLeafAnchor leaf = skeleton.Leaves[i];
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
            PeakResidentSkeletonCountDuringLastRebuild = 0;

            IReadOnlyList<TreeInstance> instances = TreeWorldReadRegistry.Current.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                _trees.Add(new TreePresentation
                {
                    Index = i,
                    Instance = instance,
                    Skeleton = null,
                    DirectCutCount = TreeWorldReadRegistry.Current.RemovedBranches(i).Count,
                });
            }

            _countSnapshotTriangles = true;
            try
            {
                RebuildBatches();
            }
            finally
            {
                _countSnapshotTriangles = false;
            }

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
            if (tree.Root != null || IsFullyRemoved(tree)) return;
            EnsureSkeleton(tree);
            if (tree.Skeleton == null || IsFullyRemoved(tree)) return;
            if (_countSnapshotTriangles)
                TotalTriangleCountAllLods += EstimateTriangleCountAllLods(tree.Skeleton);

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

            // Only the close LOD is generated on the damage frame. Use it temporarily for the two
            // farther slots and stream their cheaper meshes one per later frame.
            BuildDynamicTreeLod(tree, 0);
            tree.LodFilters[1].sharedMesh = tree.LodMeshes[0];
            tree.LodFilters[2].sharedMesh = tree.LodMeshes[0];
            _pendingDynamicLods.Enqueue(new PendingDynamicLod(tree, 1));
            _pendingDynamicLods.Enqueue(new PendingDynamicLod(tree, 2));

            if (tree.ResolvedRemovedBranches.Count > 0)
                ApplyRemovedGeometryLod(tree, 0);
            ApplyDamageMaterial(tree);
            group.RecalculateBounds();
            DynamicPresentationCount++;
        }

        private void ProcessOnePendingDynamicLod()
        {
            while (_pendingDynamicLods.Count > 0)
            {
                PendingDynamicLod pending = _pendingDynamicLods.Dequeue();
                TreePresentation tree = pending.Tree;
                if (tree == null || tree.Root == null || tree.LodMeshes == null) continue;
                if ((uint)pending.Lod >= 3u || tree.LodMeshes[pending.Lod] != null) continue;

                BuildDynamicTreeLod(tree, pending.Lod);
                if (tree.ResolvedRemovedBranches.Count > 0)
                    ApplyRemovedGeometryLod(tree, pending.Lod);
                return;
            }
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

        private void EnsureSkeleton(TreePresentation tree)
        {
            if (tree.Skeleton != null) return;
            // Startup batching should not populate the gameplay collision cache. During actual
            // damage, however, the damage service has already generated this exact skeleton, so
            // share it instead of deterministically regenerating the same tree a second time.
            tree.Skeleton = _countSnapshotTriangles
                ? TreeWorldReadRegistry.Current.SkeletonFor(in tree.Instance)
                : TreeWorldReadRegistry.Current.SkeletonFor(tree.Index);
            if (tree.Skeleton == null)
                tree.Skeleton = TreeWorldReadRegistry.Current.SkeletonFor(in tree.Instance);

            IReadOnlyCollection<int> directCuts = TreeWorldReadRegistry.Current.RemovedBranches(tree.Index);
            TreeSkeletonTopology.ResolveRemovedBranches(
                tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
            tree.DirectCutCount = directCuts.Count;
            if (_countSnapshotTriangles)
                PeakResidentSkeletonCountDuringLastRebuild = Mathf.Max(
                    PeakResidentSkeletonCountDuringLastRebuild, ResidentSkeletonCount);
        }

        private static void ReleaseBatchedSkeleton(TreePresentation tree)
        {
            if (tree.IsBatched && tree.Root == null)
                tree.Skeleton = null;
        }

        private void BuildDynamicTreeLod(TreePresentation tree, int lod)
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

        private void RebuildBatches()
        {
            ClearBatches();
            IReadOnlyList<TreeDamageState> damage = TreeWorldReadRegistry.Current.Damage;
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
                {
                    DestroyDynamicPresentation(tree);
                    ReleaseBatchedSkeleton(tree);
                }
                else
                {
                    EnsureSkeleton(tree);
                    if (!IsFullyRemoved(tree)) EnsureDynamicPresentation(tree);
                }
            }
        }

        private bool IsHealthyForBatch(int treeIndex,
                                       IReadOnlyList<TreeDamageState> damage)
        {
            if (TreeWorldReadRegistry.Current.RemovedBranches(treeIndex).Count > 0) return false;
            if (treeIndex >= damage.Count) return true;
            if (damage[treeIndex].Severed) return false;
            float damageAmount = 1f - Mathf.Clamp01(damage[treeIndex].FoliageHealth);
            return damageAmount <= HealthyDamageEpsilon;
        }

        private void BuildBatch(Vector2Int key, List<int> treeIndices)
        {
            Vector3 origin = new Vector3(key.x * BatchSizeMetres, 0f, key.y * BatchSizeMetres);
            var batch = new BatchPresentation { Key = key };
            Mesh[] meshes = BuildCombinedBatchMeshes(treeIndices, origin, key, batch);

            var root = new GameObject($"Tree Batch {key.x},{key.y}")
            {
                hideFlags = HideFlags.DontSave,
            };
            root.transform.SetParent(transform, false);
            root.transform.position = origin;
            root.transform.localRotation = Quaternion.identity;
            batch.Root = root;
            batch.LodMeshes = meshes;
            batch.TreeIndices.AddRange(treeIndices);

            var lodRenderers = new MeshRenderer[3];
            for (int lod = 0; lod < 3; lod++)
            {
                var child = new GameObject($"LOD{lod}") { hideFlags = HideFlags.DontSave };
                child.transform.SetParent(root.transform, false);
                var filter = child.AddComponent<MeshFilter>();
                var renderer = child.AddComponent<MeshRenderer>();
                ConfigureRenderer(renderer);
                filter.sharedMesh = meshes[lod];
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

        private Mesh[] BuildCombinedBatchMeshes(List<int> treeIndices,
                                                Vector3 batchOrigin, Vector2Int key,
                                                BatchPresentation batch)
        {
            var buffers = new[]
            {
                new BatchMeshBuffers(),
                new BatchMeshBuffers(),
                new BatchMeshBuffers(),
            };

            for (int i = 0; i < treeIndices.Count; i++)
            {
                TreePresentation tree = _trees[treeIndices[i]];
                EnsureSkeleton(tree);
                TreeSkeletonSnapshot skeleton = tree.Skeleton;
                if (_countSnapshotTriangles)
                    TotalTriangleCountAllLods += EstimateTriangleCountAllLods(skeleton);

                var ranges = new BatchTreeRanges();
                Vector3 offset = (Vector3)tree.Instance.PositionMetres - batchOrigin;
                for (int lod = 0; lod < 3; lod++)
                {
                    BatchMeshBuffers destination = buffers[lod];
                    ranges.BarkStart[lod] = destination.BarkIndices.Count;
                    ranges.LeafStart[lod] = destination.LeafIndices.Count;
                    ProceduralTreeMeshBuilder.AppendMeshData(
                        skeleton, lod, offset,
                        destination.Vertices, destination.Normals, destination.Colours,
                        destination.Uv0, destination.Uv1,
                        destination.BarkIndices, destination.LeafIndices);
                    ranges.BarkCount[lod] = destination.BarkIndices.Count - ranges.BarkStart[lod];
                    ranges.LeafCount[lod] = destination.LeafIndices.Count - ranges.LeafStart[lod];
                }
                batch.TreeRanges[tree.Index] = ranges;

                if (tree.Root == null)
                    tree.Skeleton = null;
            }

            var meshes = new Mesh[3];
            for (int lod = 0; lod < 3; lod++)
                meshes[lod] = CreateBatchMesh(buffers[lod], key, lod);
            return meshes;
        }

        private static Mesh CreateBatchMesh(BatchMeshBuffers data, Vector2Int key, int lod)
        {
            var combined = new Mesh
            {
                name = $"TreeBatch_{key.x}_{key.y}_LOD{lod}",
                indexFormat = data.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                hideFlags = HideFlags.DontSave,
            };
            combined.SetVertices(data.Vertices);
            combined.SetNormals(data.Normals);
            combined.SetColors(data.Colours);
            combined.SetUVs(0, data.Uv0);
            combined.SetUVs(1, data.Uv1);
            combined.subMeshCount = 2;
            combined.SetTriangles(data.BarkIndices, 0, false);
            combined.SetTriangles(data.LeafIndices, 1, false);
            combined.RecalculateBounds();
            return combined;
        }

        private bool ReleaseTreeFromBatch(TreePresentation tree)
        {
            if (!tree.IsBatched) return false;
            Vector2Int key = BatchKeyFor(tree.Instance);
            if (!_batchByKey.TryGetValue(key, out BatchPresentation batch)) return false;
            if (!HideTreeInBatch(batch, tree.Index)) return false;
            tree.IsBatched = false;
            BatchedTreeCount = Mathf.Max(0, BatchedTreeCount - 1);
            LastDamageBatchReleaseCount++;
            return true;
        }

        private bool HideTreeInBatch(BatchPresentation batch, int treeIndex)
        {
            if (batch == null || !batch.HiddenTreeIndices.Add(treeIndex)) return false;
            if (!batch.TreeRanges.TryGetValue(treeIndex, out BatchTreeRanges ranges)) return false;

            for (int lod = 0; lod < 3; lod++)
            {
                Mesh mesh = batch.LodMeshes[lod];
                HideIndexRange(mesh, 0, ranges.BarkStart[lod], ranges.BarkCount[lod]);
                HideIndexRange(mesh, 1, ranges.LeafStart[lod], ranges.LeafCount[lod]);
            }
            return true;
        }

        private void HideIndexRange(Mesh mesh, int subMesh, int relativeStart, int count)
        {
            if (mesh == null || count <= 0) return;
            int bufferStart = checked((int)mesh.GetIndexStart(subMesh)) + relativeStart;
            const MeshUpdateFlags flags = MeshUpdateFlags.DontRecalculateBounds
                                        | MeshUpdateFlags.DontValidateIndices
                                        | MeshUpdateFlags.DontNotifyMeshUsers;
            if (mesh.indexFormat == IndexFormat.UInt16)
            {
                EnsureZeroIndexCapacity16(count);
                mesh.SetIndexBufferData(_zeroIndices16, 0, bufferStart, count, flags);
            }
            else
            {
                EnsureZeroIndexCapacity32(count);
                mesh.SetIndexBufferData(_zeroIndices32, 0, bufferStart, count, flags);
            }
        }

        private void EnsureZeroIndexCapacity16(int count)
        {
            if (_zeroIndices16.Length >= count) return;
            _zeroIndices16 = new ushort[Mathf.NextPowerOfTwo(count)];
        }

        private void EnsureZeroIndexCapacity32(int count)
        {
            if (_zeroIndices32.Length >= count) return;
            _zeroIndices32 = new uint[Mathf.NextPowerOfTwo(count)];
        }

        private static long EstimateTriangleCountAllLods(TreeSkeletonSnapshot skeleton)
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

        private static int[] BuildBarkOwners(TreeSkeletonSnapshot skeleton,
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

        private static int[] BuildLeafOwners(TreeSkeletonSnapshot skeleton,
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
                          && leafIndex < skeleton.LeafParents.Count
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
                ApplyRemovedGeometryLod(tree, lod);
        }

        private void ApplyRemovedGeometryLod(TreePresentation tree, int lod)
        {
            if (tree.LodMeshes == null || (uint)lod >= (uint)tree.LodMeshes.Length) return;
            Mesh mesh = tree.LodMeshes[lod];
            if (mesh == null) return;
            FilterIndices(tree.BaseBarkIndices[lod], tree.BarkIndexOwners[lod],
                          tree.ResolvedRemovedBranches, _filteredBarkIndices);
            FilterIndices(tree.BaseLeafIndices[lod], tree.LeafIndexOwners[lod],
                          tree.ResolvedRemovedBranches, _filteredLeafIndices);
            mesh.SetTriangles(_filteredBarkIndices, 0, false);
            mesh.SetTriangles(_filteredLeafIndices, 1, false);
            // Keep the original conservative bounds. Recalculating bounds on every branch hit is
            // unnecessary CPU work and can synchronize with mesh uploads on the destruction frame.
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
            IReadOnlyList<TreeDamageState> damage = TreeWorldReadRegistry.Current.Damage;
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
            LastDamageBatchRebuildCount = 0;
            LastDamageBatchReleaseCount = 0;
            if (_dirtyTreeIndices.Count == 0) return;

            foreach (int index in _dirtyTreeIndices)
            {
                if ((uint)index >= (uint)_trees.Count) continue;
                TreePresentation tree = _trees[index];
                IReadOnlyCollection<int> directCuts = TreeWorldReadRegistry.Current.RemovedBranches(index);
                bool geometryChanged = tree.DirectCutCount != directCuts.Count;
                if (geometryChanged)
                {
                    EnsureSkeleton(tree);
                    TreeSkeletonTopology.ResolveRemovedBranches(
                        tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
                    tree.DirectCutCount = directCuts.Count;
                }

                IReadOnlyList<TreeDamageState> damage = TreeWorldReadRegistry.Current.Damage;
                float damageAmount = index < damage.Count
                    ? 1f - Mathf.Clamp01(damage[index].FoliageHealth)
                    : 0f;
                bool shouldBatch = directCuts.Count == 0
                    && (index >= damage.Count || !damage[index].Severed)
                    && damageAmount <= HealthyDamageEpsilon;

                if (tree.IsBatched && !shouldBatch)
                    ReleaseTreeFromBatch(tree);

                if (IsFullyRemoved(tree))
                {
                    DestroyDynamicPresentation(tree);
                    tree.Skeleton = null;
                    continue;
                }

                if (!tree.IsBatched)
                {
                    EnsureDynamicPresentation(tree);
                    if (geometryChanged) ApplyRemovedGeometry(tree);
                    ApplyDamageMaterial(tree);
                }
            }

            _dirtyTreeIndices.Clear();
        }

        private static bool IsFullyRemoved(TreePresentation tree)
        {
            return tree.Skeleton != null
                && tree.Skeleton.Branches.Count > 0
                && tree.ResolvedRemovedBranches.Count >= tree.Skeleton.Branches.Count;
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
            LastDamageBatchReleaseCount = 0;
        }

        private void ClearGenerated()
        {
            _pendingDynamicLods.Clear();
            _dirtyTreeIndices.Clear();
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