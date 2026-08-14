using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Vegetation;
using TreeInstance = VoxelEngine.Core.Vegetation.TreeInstance;

namespace VoxelEngine.Rendering.Vegetation
{
    /// <summary>
    /// GameObject-free presentation of standing procedural trees. Healthy trees live in spatial
    /// mesh batches and damaged standing trees live in standalone meshes; both are submitted
    /// directly through Graphics.DrawMesh. GameObjects are reserved for detached/falling debris in
    /// the dedicated destruction presenters.
    /// </summary>
    public sealed class ProceduralTreeRenderer : MonoBehaviour
    {
        private const float BatchSizeMetres = 32f;
        private const float HealthyDamageEpsilon = 0.0001f;
        private const float Lod0DistanceMetres = 45f;
        private const float Lod1DistanceMetres = 120f;
        private const float Lod2DistanceMetres = 300f;
        private const float ImpostorCullDistanceMetres = 1400f;

        private sealed class TreePresentation
        {
            public int Index;
            public TreeInstance Instance;
            public ProceduralTreeSkeleton Skeleton;
            public Mesh[] LodMeshes;
            public Mesh ImpostorMesh;
            public int[][] BaseBarkIndices;
            public int[][] BaseLeafIndices;
            public int[][] BarkIndexOwners;
            public int[][] LeafIndexOwners;
            public readonly HashSet<int> ResolvedRemovedBranches = new();
            public int DirectCutCount;
            public bool IsBatched;
            public MaterialPropertyBlock DamageProperties;
        }

        private sealed class BatchTreeRanges
        {
            public readonly int[] BarkStart = new int[3];
            public readonly int[] BarkCount = new int[3];
            public readonly int[] LeafStart = new int[3];
            public readonly int[] LeafCount = new int[3];
            public int ImpostorStart;
            public int ImpostorCount;
        }

        private sealed class BatchPresentation
        {
            public Vector2Int Key;
            public Vector3 Origin;
            public Mesh[] LodMeshes;
            public Mesh ImpostorMesh;
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

        private sealed class ImpostorMeshBuffers
        {
            public readonly List<Vector3> Vertices = new(2048);
            public readonly List<Vector3> Normals = new(2048);
            public readonly List<Color> Colours = new(2048);
            public readonly List<Vector2> Uv0 = new(2048);
            public readonly List<Vector2> Uv1 = new(2048);
            public readonly List<int> Indices = new(3072);
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
        private bool _snapshotDirty = true;
        private bool _countSnapshotTriangles;

        public double LastRebuildMilliseconds { get; private set; }
        public int LastDamageBatchRebuildCount { get; private set; }
        public int LastDamageBatchReleaseCount { get; private set; }
        public int PresentationCount => _trees.Count;
        public int DynamicPresentationCount { get; private set; }
        public int DynamicMeshCount => DynamicPresentationCount * 4;
        public int BatchCount => _batches.Count;
        public int BatchedTreeCount { get; private set; }
        public int BatchMeshCount => _batches.Count * 4;
        public int GeneratedMeshCount => DynamicMeshCount + BatchMeshCount;
        public long TotalTriangleCountAllLods { get; private set; }

        public int ResidentRenderObjectCount => 0;
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
        }

        private void OnEnable()
        {
            TreeWorldState.SnapshotChanged += OnSnapshotChanged;
            TreeWorldState.BranchCut += OnBranchCut;
            TreeWorldState.DamageChanged += OnDamageChanged;
            _snapshotDirty = true;
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
            _dirtyTreeIndices.Clear();
            _pendingDynamicLods.Clear();
        }

        private void OnBranchCut(TreeBranchCutEvent cut) => _dirtyTreeIndices.Add(cut.TreeIndex);
        private void OnDamageChanged(TreeDamageChangedEvent damage) => _dirtyTreeIndices.Add(damage.TreeIndex);

        private void Update()
        {
            if (!ProceduralTreeMaterials.Ensure()) return;
            ProceduralTreeMaterials.ApplyLighting();

            if (_snapshotDirty)
            {
                _snapshotDirty = false;
                Rebuild();
                _dirtyTreeIndices.Clear();
            }
            else
            {
                ProcessOnePendingDynamicLod();
                if (_dirtyTreeIndices.Count > 0)
                    ApplyDamage();
            }

            DrawStandingTrees();
        }

        public bool TryGetDynamicPresentationRoot(int treeIndex, out Transform root)
        {
            root = null;
            return false;
        }

        public bool TryGetTreeBounds(int treeIndex, out Bounds bounds)
        {
            bounds = default;
            if ((uint)treeIndex >= (uint)_trees.Count) return false;

            TreePresentation tree = _trees[treeIndex];
            ProceduralTreeSkeleton skeleton = tree.Skeleton
                ?? ProceduralTreeSkeletonBuilder.Generate(in tree.Instance);
            Vector3 root = (Vector3)tree.Instance.PositionMetres;
            return TryCalculateTreeBounds(skeleton, tree.ResolvedRemovedBranches, root, out bounds);
        }

        private void Rebuild()
        {
            var stopwatch = Stopwatch.StartNew();
            ClearGenerated();
            TotalTriangleCountAllLods = 0;
            PeakResidentSkeletonCountDuringLastRebuild = 0;

            IReadOnlyList<TreeInstance> instances = TreeWorldState.Instances;
            for (int i = 0; i < instances.Count; i++)
            {
                TreeInstance instance = instances[i];
                _trees.Add(new TreePresentation
                {
                    Index = i,
                    Instance = instance,
                    Skeleton = null,
                    DirectCutCount = TreeWorldState.RemovedBranches(i).Count,
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

        private static Vector2Int BatchKeyFor(in TreeInstance instance)
        {
            Vector3 position = (Vector3)instance.PositionMetres;
            return new Vector2Int(
                Mathf.FloorToInt(position.x / BatchSizeMetres),
                Mathf.FloorToInt(position.z / BatchSizeMetres));
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
                {
                    DestroyStandalonePresentation(tree);
                    ReleaseBatchedSkeleton(tree);
                }
                else
                {
                    EnsureSkeleton(tree);
                    if (!IsFullyRemoved(tree)) EnsureStandalonePresentation(tree);
                }
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
            var batch = new BatchPresentation { Key = key, Origin = origin };
            BuildCombinedBatchMeshes(treeIndices, origin, key, batch,
                                     out Mesh[] meshes, out Mesh impostor);
            batch.LodMeshes = meshes;
            batch.ImpostorMesh = impostor;
            batch.TreeIndices.AddRange(treeIndices);

            for (int i = 0; i < treeIndices.Count; i++)
                _trees[treeIndices[i]].IsBatched = true;

            BatchedTreeCount += treeIndices.Count;
            _batches.Add(batch);
            _batchByKey[key] = batch;
        }

        private void BuildCombinedBatchMeshes(List<int> treeIndices,
                                              Vector3 batchOrigin,
                                              Vector2Int key,
                                              BatchPresentation batch,
                                              out Mesh[] meshes,
                                              out Mesh impostorMesh)
        {
            var buffers = new[]
            {
                new BatchMeshBuffers(),
                new BatchMeshBuffers(),
                new BatchMeshBuffers(),
            };
            var impostor = new ImpostorMeshBuffers();

            for (int i = 0; i < treeIndices.Count; i++)
            {
                TreePresentation tree = _trees[treeIndices[i]];
                EnsureSkeleton(tree);
                ProceduralTreeSkeleton skeleton = tree.Skeleton;
                if (_countSnapshotTriangles)
                    TotalTriangleCountAllLods += EstimateTriangleCountAllLods(skeleton) + 8;

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

                ranges.ImpostorStart = impostor.Indices.Count;
                AppendTreeImpostor(skeleton, null, offset, impostor);
                ranges.ImpostorCount = impostor.Indices.Count - ranges.ImpostorStart;
                batch.TreeRanges[tree.Index] = ranges;

                tree.Skeleton = null;
            }

            meshes = new Mesh[3];
            for (int lod = 0; lod < 3; lod++)
                meshes[lod] = CreateBatchMesh(buffers[lod], key, lod);
            impostorMesh = CreateImpostorMesh(impostor, $"TreeBatch_{key.x}_{key.y}_LOD3");
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

        private static Mesh CreateImpostorMesh(ImpostorMeshBuffers data, string name)
        {
            var mesh = new Mesh
            {
                name = name,
                indexFormat = data.Vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
                hideFlags = HideFlags.DontSave,
            };
            mesh.SetVertices(data.Vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetColors(data.Colours);
            mesh.SetUVs(0, data.Uv0);
            mesh.SetUVs(1, data.Uv1);
            mesh.SetTriangles(data.Indices, 0, false);
            mesh.RecalculateBounds();
            return mesh;
        }

        private void EnsureStandalonePresentation(TreePresentation tree)
        {
            if (tree.LodMeshes != null || IsFullyRemoved(tree)) return;
            EnsureSkeleton(tree);
            if (tree.Skeleton == null || IsFullyRemoved(tree)) return;

            if (_countSnapshotTriangles)
                TotalTriangleCountAllLods += EstimateTriangleCountAllLods(tree.Skeleton) + 8;

            tree.LodMeshes = new Mesh[3];
            tree.BaseBarkIndices = new int[3][];
            tree.BaseLeafIndices = new int[3][];
            tree.BarkIndexOwners = new int[3][];
            tree.LeafIndexOwners = new int[3][];

            BuildStandaloneTreeLod(tree, 0);
            if (tree.ResolvedRemovedBranches.Count > 0)
                ApplyRemovedGeometryLod(tree, 0);
            _pendingDynamicLods.Enqueue(new PendingDynamicLod(tree, 1));
            _pendingDynamicLods.Enqueue(new PendingDynamicLod(tree, 2));
            RebuildStandaloneImpostor(tree);
            ApplyDamageMaterial(tree);
            DynamicPresentationCount++;
        }

        private void ProcessOnePendingDynamicLod()
        {
            while (_pendingDynamicLods.Count > 0)
            {
                PendingDynamicLod pending = _pendingDynamicLods.Dequeue();
                TreePresentation tree = pending.Tree;
                if (tree == null || tree.LodMeshes == null) continue;
                if ((uint)pending.Lod >= 3u || tree.LodMeshes[pending.Lod] != null) continue;

                BuildStandaloneTreeLod(tree, pending.Lod);
                if (tree.ResolvedRemovedBranches.Count > 0)
                    ApplyRemovedGeometryLod(tree, pending.Lod);
                return;
            }
        }

        private void BuildStandaloneTreeLod(TreePresentation tree, int lod)
        {
            Mesh mesh = ProceduralTreeMeshBuilder.BuildMesh(tree.Skeleton, lod);
            mesh.name = $"{tree.Instance.Species}_{tree.Instance.Seed}_LOD{lod}";
            mesh.hideFlags = HideFlags.DontSave;
            mesh.MarkDynamic();
            tree.LodMeshes[lod] = mesh;

            int[] bark = mesh.GetTriangles(0);
            int[] leaves = mesh.GetTriangles(1);
            tree.BaseBarkIndices[lod] = bark;
            tree.BaseLeafIndices[lod] = leaves;
            tree.BarkIndexOwners[lod] = BuildBarkOwners(tree.Skeleton, lod, bark.Length);
            tree.LeafIndexOwners[lod] = BuildLeafOwners(tree.Skeleton, lod, leaves.Length);
        }

        private void RebuildStandaloneImpostor(TreePresentation tree)
        {
            if (tree.ImpostorMesh != null) Destroy(tree.ImpostorMesh);
            var data = new ImpostorMeshBuffers();
            AppendTreeImpostor(tree.Skeleton, tree.ResolvedRemovedBranches, Vector3.zero, data);
            tree.ImpostorMesh = CreateImpostorMesh(
                data, $"{tree.Instance.Species}_{tree.Instance.Seed}_LOD3");
        }

        private void DestroyStandalonePresentation(TreePresentation tree)
        {
            bool hadPresentation = tree.LodMeshes != null || tree.ImpostorMesh != null;
            if (tree.LodMeshes != null)
            {
                for (int lod = 0; lod < tree.LodMeshes.Length; lod++)
                    if (tree.LodMeshes[lod] != null) Destroy(tree.LodMeshes[lod]);
            }
            if (tree.ImpostorMesh != null) Destroy(tree.ImpostorMesh);

            tree.LodMeshes = null;
            tree.ImpostorMesh = null;
            tree.BaseBarkIndices = null;
            tree.BaseLeafIndices = null;
            tree.BarkIndexOwners = null;
            tree.LeafIndexOwners = null;
            tree.DamageProperties = null;
            if (hadPresentation)
                DynamicPresentationCount = Mathf.Max(0, DynamicPresentationCount - 1);
        }

        private void EnsureSkeleton(TreePresentation tree)
        {
            if (tree.Skeleton != null) return;
            tree.Skeleton = _countSnapshotTriangles
                ? ProceduralTreeSkeletonBuilder.Generate(in tree.Instance)
                : ProceduralTreeDamageService.SkeletonFor(tree.Index);
            if (tree.Skeleton == null)
                tree.Skeleton = ProceduralTreeSkeletonBuilder.Generate(in tree.Instance);

            IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(tree.Index);
            ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
            tree.DirectCutCount = directCuts.Count;
            if (_countSnapshotTriangles)
                PeakResidentSkeletonCountDuringLastRebuild = Mathf.Max(
                    PeakResidentSkeletonCountDuringLastRebuild, ResidentSkeletonCount);
        }

        private static void ReleaseBatchedSkeleton(TreePresentation tree)
        {
            if (tree.IsBatched && tree.LodMeshes == null)
                tree.Skeleton = null;
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
            HideIndexRange(batch.ImpostorMesh, 0, ranges.ImpostorStart, ranges.ImpostorCount);
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

        private void ApplyRemovedGeometry(TreePresentation tree)
        {
            if (tree.LodMeshes != null)
            {
                for (int lod = 0; lod < tree.LodMeshes.Length; lod++)
                    ApplyRemovedGeometryLod(tree, lod);
            }
            RebuildStandaloneImpostor(tree);
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
            IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
            float damageAmount = tree.Index < damage.Count
                ? 1f - Mathf.Clamp01(damage[tree.Index].FoliageHealth)
                : 0f;
            if (tree.DamageProperties == null)
                tree.DamageProperties = new MaterialPropertyBlock();
            tree.DamageProperties.Clear();
            tree.DamageProperties.SetFloat(s_Damage, damageAmount);
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
                IReadOnlyCollection<int> directCuts = TreeWorldState.RemovedBranches(index);
                bool geometryChanged = tree.DirectCutCount != directCuts.Count;
                if (geometryChanged)
                {
                    EnsureSkeleton(tree);
                    ProceduralTreeSkeletonBuilder.ResolveRemovedBranches(
                        tree.Skeleton, directCuts, tree.ResolvedRemovedBranches);
                    tree.DirectCutCount = directCuts.Count;
                }

                IReadOnlyList<TreeWorldState.TreeDamageState> damage = TreeWorldState.Damage;
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
                    DestroyStandalonePresentation(tree);
                    tree.Skeleton = null;
                    continue;
                }

                if (!tree.IsBatched)
                {
                    EnsureStandalonePresentation(tree);
                    if (geometryChanged) ApplyRemovedGeometry(tree);
                    ApplyDamageMaterial(tree);
                }
            }

            _dirtyTreeIndices.Clear();
        }

        private void DrawStandingTrees()
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            Vector3 cameraPosition = camera.transform.position;

            for (int i = 0; i < _batches.Count; i++)
            {
                BatchPresentation batch = _batches[i];
                Vector3 centre = batch.Origin + new Vector3(BatchSizeMetres * 0.5f, 8f,
                                                            BatchSizeMetres * 0.5f);
                float distance = Vector3.Distance(cameraPosition, centre);
                if (distance >= ImpostorCullDistanceMetres) continue;
                Matrix4x4 matrix = Matrix4x4.TRS(batch.Origin, Quaternion.identity, Vector3.one);

                if (distance >= Lod2DistanceMetres)
                {
                    DrawImpostor(batch.ImpostorMesh, matrix, null);
                    continue;
                }

                int lod = distance < Lod0DistanceMetres ? 0
                        : distance < Lod1DistanceMetres ? 1 : 2;
                DrawTreeMesh(batch.LodMeshes[lod], matrix, null);
            }

            for (int i = 0; i < _trees.Count; i++)
            {
                TreePresentation tree = _trees[i];
                if (tree.IsBatched || tree.LodMeshes == null) continue;
                Vector3 position = (Vector3)tree.Instance.PositionMetres;
                float distance = Vector3.Distance(cameraPosition, position);
                if (distance >= ImpostorCullDistanceMetres) continue;
                Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);

                if (distance >= Lod2DistanceMetres)
                {
                    DrawImpostor(tree.ImpostorMesh, matrix, tree.DamageProperties);
                    continue;
                }

                int lod = distance < Lod0DistanceMetres ? 0
                        : distance < Lod1DistanceMetres ? 1 : 2;
                Mesh mesh = tree.LodMeshes[lod] ?? tree.LodMeshes[0];
                DrawTreeMesh(mesh, matrix, tree.DamageProperties);
            }
        }

        private static void DrawTreeMesh(Mesh mesh, Matrix4x4 matrix,
                                         MaterialPropertyBlock leafProperties)
        {
            if (mesh == null) return;
            Graphics.DrawMesh(mesh, matrix, ProceduralTreeMaterials.Bark, 0, null, 0, null,
                              ShadowCastingMode.On, true);
            Graphics.DrawMesh(mesh, matrix, ProceduralTreeMaterials.Leaves, 0, null, 1,
                              leafProperties, ShadowCastingMode.On, true);
        }

        private static void DrawImpostor(Mesh mesh, Matrix4x4 matrix,
                                         MaterialPropertyBlock properties)
        {
            if (mesh == null) return;
            Graphics.DrawMesh(mesh, matrix, ProceduralTreeMaterials.Impostor, 0, null, 0,
                              properties, ShadowCastingMode.Off, true);
        }

        private static void AppendTreeImpostor(ProceduralTreeSkeleton skeleton,
                                               HashSet<int> removedBranches,
                                               Vector3 offset,
                                               ImpostorMeshBuffers destination)
        {
            if (!TryCalculateLocalTreeBounds(skeleton, removedBranches, out Bounds bounds)) return;

            // Keep the far card inside the semantic tree footprint. The prior 1.12x expansion
            // exaggerated the crown at the LOD2->LOD3 handoff and caused a visible coverage pop.
            float width = Mathf.Max(0.35f, Mathf.Max(bounds.size.x, bounds.size.z) * 0.96f);
            float height = Mathf.Max(0.6f, bounds.size.y);
            Vector3 centre = bounds.center + offset;
            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            Color colour = AverageLeafColour(skeleton, removedBranches);
            AppendImpostorPlane(centre, Vector3.right, halfW, halfH, colour, destination);
            AppendImpostorPlane(centre, Vector3.forward, halfW, halfH, colour, destination);
        }

        private static void AppendImpostorPlane(Vector3 centre, Vector3 horizontal,
                                                float halfW, float halfH, Color colour,
                                                ImpostorMeshBuffers destination)
        {
            int start = destination.Vertices.Count;
            Vector3 vertical = Vector3.up * halfH;
            Vector3 side = horizontal * halfW;
            Vector3 normal = Vector3.Cross(horizontal, Vector3.up).normalized;

            destination.Vertices.Add(centre - side - vertical);
            destination.Vertices.Add(centre + side - vertical);
            destination.Vertices.Add(centre + side + vertical);
            destination.Vertices.Add(centre - side + vertical);
            for (int i = 0; i < 4; i++)
            {
                destination.Normals.Add(normal);
                destination.Colours.Add(colour);
                destination.Uv1.Add(Vector2.zero);
            }
            destination.Uv0.Add(new Vector2(0f, 0f));
            destination.Uv0.Add(new Vector2(1f, 0f));
            destination.Uv0.Add(new Vector2(1f, 1f));
            destination.Uv0.Add(new Vector2(0f, 1f));

            destination.Indices.Add(start);
            destination.Indices.Add(start + 1);
            destination.Indices.Add(start + 2);
            destination.Indices.Add(start);
            destination.Indices.Add(start + 2);
            destination.Indices.Add(start + 3);
            destination.Indices.Add(start + 2);
            destination.Indices.Add(start + 1);
            destination.Indices.Add(start);
            destination.Indices.Add(start + 3);
            destination.Indices.Add(start + 2);
            destination.Indices.Add(start);
        }

        private static Color AverageLeafColour(ProceduralTreeSkeleton skeleton,
                                               HashSet<int> removedBranches)
        {
            Vector4 sum = Vector4.zero;
            int count = 0;
            for (int i = 0; i < skeleton.Leaves.Count; i++)
            {
                int parent = skeleton.LeafParents != null && i < skeleton.LeafParents.Length
                    ? skeleton.LeafParents[i] : -1;
                if (parent >= 0 && removedBranches != null && removedBranches.Contains(parent))
                    continue;
                TreeLeafAnchor leaf = skeleton.Leaves[i];
                sum += new Vector4(leaf.Colour.x, leaf.Colour.y, leaf.Colour.z, leaf.Colour.w);
                count++;
            }

            if (count == 0) return new Color(0.22f, 0.42f, 0.14f, 1f);
            Vector4 average = sum / count;
            return new Color(average.x, average.y, average.z, average.w);
        }

        private static bool TryCalculateLocalTreeBounds(ProceduralTreeSkeleton skeleton,
                                                        HashSet<int> removedBranches,
                                                        out Bounds bounds)
        {
            return TryCalculateTreeBounds(skeleton, removedBranches, Vector3.zero, out bounds);
        }

        private static bool TryCalculateTreeBounds(ProceduralTreeSkeleton skeleton,
                                                   HashSet<int> removedBranches,
                                                   Vector3 root,
                                                   out Bounds bounds)
        {
            bool hasPoint = false;
            Vector3 min = root;
            Vector3 max = root;

            for (int i = 0; i < skeleton.Branches.Count; i++)
            {
                if (removedBranches != null && removedBranches.Contains(i)) continue;
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
                int parent = skeleton.LeafParents != null && i < skeleton.LeafParents.Length
                    ? skeleton.LeafParents[i] : -1;
                if (parent >= 0 && removedBranches != null && removedBranches.Contains(parent))
                    continue;
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
                return false;
            }

            bounds = new Bounds((min + max) * 0.5f, max - min);
            return true;
        }

        private static bool IsFullyRemoved(TreePresentation tree)
        {
            return tree.Skeleton != null
                && tree.Skeleton.Branches.Count > 0
                && tree.ResolvedRemovedBranches.Count >= tree.Skeleton.Branches.Count;
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

        private void DestroyBatchMeshes(BatchPresentation batch)
        {
            if (batch.LodMeshes != null)
            {
                for (int lod = 0; lod < batch.LodMeshes.Length; lod++)
                    if (batch.LodMeshes[lod] != null) Destroy(batch.LodMeshes[lod]);
            }
            if (batch.ImpostorMesh != null) Destroy(batch.ImpostorMesh);
        }

        private void ClearBatches()
        {
            for (int i = 0; i < _batches.Count; i++)
                DestroyBatchMeshes(_batches[i]);
            _batches.Clear();
            _batchByKey.Clear();
            BatchedTreeCount = 0;
        }

        private void ClearGenerated()
        {
            for (int i = 0; i < _trees.Count; i++)
                DestroyStandalonePresentation(_trees[i]);
            _trees.Clear();
            _pendingDynamicLods.Clear();
            ClearBatches();
            DynamicPresentationCount = 0;
            TotalTriangleCountAllLods = 0;
        }

        private void OnDestroy()
        {
            TreeWorldState.SnapshotChanged -= OnSnapshotChanged;
            TreeWorldState.BranchCut -= OnBranchCut;
            TreeWorldState.DamageChanged -= OnDamageChanged;
            ClearGenerated();
            if (s_Instance == this) s_Instance = null;
        }
    }
}
