using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>A region's feature generation, resumable within a single primitive.</summary>
    public sealed class FeatureRegionBuild : IDisposable
    {
        private const int TileEdge = VoxelReadGrid.BlockEdge;

        private readonly int3 _regionMin;
        private readonly int3 _regionMax;
        private NativeList<Primitive> _primitives;
        private NativeList<ResolvedAnchor> _anchors;
        private FeatureGenerationReport _report;
        private int _ruleIndex;
        private int _explicitIndex;
        private int _activePrimitiveIndex;
        private int3 _tileMin;
        private int3 _tileMax;
        private int3 _tileCursor;
        private bool _activeInstance;
        private bool _activeRasterisedAny;
        private bool _tileReady;
        private bool _markHardSurface;
        private bool _disposed;

        public FeatureRegionBuild(int3 regionCoord)
        {
            RegionCoord = regionCoord;
            _regionMin = regionCoord * VoxelGrid.RegionVoxelEdge;
            _regionMax = _regionMin + VoxelGrid.RegionVoxelEdge;
            _primitives = new NativeList<Primitive>(64, Allocator.Persistent);
            _anchors = new NativeList<ResolvedAnchor>(8, Allocator.Persistent);
        }

        public int3 RegionCoord { get; }
        public bool IsComplete { get; private set; }
        public FeatureGenerationReport Report => _report;

        /// <summary>
        /// Placements a single slice may reject before yielding.
        ///
        /// Rejecting one placement is only an integer footprint comparison, and while a world held
        /// a single settlement that made scanning effectively free. It stopped being free when one
        /// catalogue began describing two towns and the country between them: most regions in that
        /// world intersect nothing, and every one of them walked every placement of both towns
        /// before reporting that it had no work. The caller's frame budget cannot interrupt that,
        /// because it is only checked between slices, so the cost landed whole in one frame.
        ///
        /// Yielding on the scan is what puts those regions back under the caller's budget. It does
        /// not make the total work smaller — it makes it interruptible.
        /// </summary>
        private const int MaxPlacementsScannedPerSlice = 2048;

        /// <summary>
        /// Rasterises at most <paramref name="maxTiles"/> storage-block-sized pieces, preserving
        /// placement and primitive order. Rejecting catalogue entries that lie outside this region
        /// is charged against <see cref="MaxPlacementsScannedPerSlice"/> so a region that
        /// intersects nothing still returns to the caller promptly.
        /// </summary>
        public bool Step(
            in FeatureCatalogue catalogue,
            uint seed,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            int maxTiles)
        {
            ThrowIfDisposed();
            if (maxTiles <= 0) throw new ArgumentOutOfRangeException(nameof(maxTiles));
            if (IsComplete) return true;
            if (!catalogue.IsCreated)
            {
                IsComplete = true;
                return true;
            }

            int tilesRasterised = 0;
            int scanBudget = MaxPlacementsScannedPerSlice;
            while (tilesRasterised < maxTiles)
            {
                if (!_activeInstance)
                {
                    if (!TryBeginNextInstance(in catalogue, seed, ref scanBudget))
                    {
                        IsComplete = true;
                        return true;
                    }

                    // Out of scan budget rather than out of catalogue: the cursor is parked mid-scan
                    // and the next slice resumes from it. Reported as incomplete so the caller keeps
                    // this region queued.
                    if (scanBudget <= 0 && !_activeInstance) return false;

                    // Invalid programs are reported but have no voxel work to charge.
                    if (!_activeInstance) continue;
                }

                if (!TryPrepareTile())
                {
                    CompleteActiveInstance();
                    continue;
                }

                Primitive primitive = _primitives[_activePrimitiveIndex];
                bool surfacePaint = primitive.Mode == PrimitiveMode.PaintSurface;
                int3 tileMin = surfacePaint
                    ? new int3(_tileCursor.x, _regionMin.y, _tileCursor.z)
                    : _tileCursor;
                int3 tileMax = surfacePaint
                    ? new int3(math.min(_tileCursor.x + TileEdge, _tileMax.x),
                               _regionMax.y,
                               math.min(_tileCursor.z + TileEdge, _tileMax.z))
                    : math.min(_tileCursor + TileEdge, _tileMax);

                RasterResult raster = PrimitiveRasteriser.RasterisePrimitive(
                    in primitive, tileMin, tileMax, reads, mutations, _markHardSurface);
                _report.VoxelsWritten += raster.VoxelsWritten;
                _activeRasterisedAny |= raster.PrimitivesRasterised > 0;
                tilesRasterised++;
                AdvanceTile(surfacePaint);
            }

            return false;
        }

        private bool TryBeginNextInstance(
            in FeatureCatalogue catalogue, uint seed, ref int scanBudget)
        {
            while (_ruleIndex < catalogue.Rules.Length)
            {
                PlacementRule rule = catalogue.Rules[_ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)catalogue.DefinitionCount)
                {
                    MoveToNextRule();
                    continue;
                }

                FeatureDefinition definition = catalogue.Definitions[rule.DefinitionId];
                while (_explicitIndex < rule.ExplicitCount)
                {
                    // Yield with the cursor parked. Returning true without an active instance is
                    // what tells the caller there is more catalogue left to walk.
                    if (scanBudget <= 0) return true;

                    int index = rule.ExplicitOffset + _explicitIndex++;
                    if ((uint)index >= (uint)catalogue.ExplicitPlacements.Length) continue;

                    ExplicitPlacement placement = catalogue.ExplicitPlacements[index];
                    _report.InstancesConsidered++;
                    scanBudget--;
                    if (!FeatureGeneration.FootprintIntersects(
                            placement.Position, definition.Footprint, _regionMin, _regionMax))
                        continue;

                    EvaluationResult evaluation = FeatureGeneration.EvaluateInstance(
                        in catalogue, seed, rule.DefinitionId, in definition, in placement,
                        _primitives, _anchors);
                    _report.LastEvaluationResult = evaluation;
                    if (evaluation != EvaluationResult.Ok)
                    {
                        _report.BudgetExceeded |=
                            evaluation == EvaluationResult.PrimitiveLimitExceeded;
                        return true;
                    }

                    _report.PrimitivesEmitted += _primitives.Length;
                    _markHardSurface = definition.Kind == FeatureKind.Structure
                                    || definition.Kind == FeatureKind.Infrastructure;
                    _activePrimitiveIndex = 0;
                    _activeRasterisedAny = false;
                    _tileReady = false;
                    _activeInstance = true;
                    return true;
                }

                MoveToNextRule();
            }

            return false;
        }

        private bool TryPrepareTile()
        {
            while (_activePrimitiveIndex < _primitives.Length)
            {
                if (_tileReady) return true;

                Primitive primitive = _primitives[_activePrimitiveIndex];
                primitive.Bounds(out int3 min, out int3 max);

                // Curved boundary samples may extend two voxels beyond the solid bounds. Expanding
                // all shapes is cheap and keeps emitter-specific classification out of this layer.
                _tileMin = math.max(AlignDown(min - 2), _regionMin);
                _tileMax = math.min(AlignUp(max + 3), _regionMax);
                if (primitive.Mode == PrimitiveMode.PaintSurface)
                {
                    _tileMin.y = _regionMin.y;
                    _tileMax.y = _regionMax.y;
                }

                if (math.any(_tileMin >= _tileMax))
                {
                    _activePrimitiveIndex++;
                    continue;
                }

                _tileCursor = _tileMin;
                _tileReady = true;
                return true;
            }

            return false;
        }

        private void AdvanceTile(bool surfacePaint)
        {
            _tileCursor.x += TileEdge;
            if (_tileCursor.x < _tileMax.x) return;
            _tileCursor.x = _tileMin.x;

            if (!surfacePaint)
            {
                _tileCursor.y += TileEdge;
                if (_tileCursor.y < _tileMax.y) return;
                _tileCursor.y = _tileMin.y;
            }

            _tileCursor.z += TileEdge;
            if (_tileCursor.z < _tileMax.z) return;

            _activePrimitiveIndex++;
            _tileReady = false;
        }

        private void CompleteActiveInstance()
        {
            if (_activeRasterisedAny) _report.InstancesRasterised++;
            _activeInstance = false;
            _primitives.Clear();
            _anchors.Clear();
        }

        private void MoveToNextRule()
        {
            _ruleIndex++;
            _explicitIndex = 0;
        }

        private static int3 AlignDown(int3 value) => value & new int3(-TileEdge);
        private static int3 AlignUp(int3 value) =>
            (value + TileEdge - 1) & new int3(-TileEdge);

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FeatureRegionBuild));
        }

        public void Dispose()
        {
            if (_disposed) return;
            if (_primitives.IsCreated) _primitives.Dispose();
            if (_anchors.IsCreated) _anchors.Dispose();
            _disposed = true;
        }
    }
}
