using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>
    /// A region's feature generation, resumable across frames.
    ///
    /// Generating a region's features in one call is the right shape for a capture tool and the
    /// wrong one for a streaming world. A settlement region carries hundreds of building
    /// instances, each of which evaluates a shape program and rasterises its primitives, and the
    /// whole of it landed in the frame that happened to finish that region's terrain — the visible
    /// result being a multi-hundred-millisecond stall as a town streamed in. Terrain already
    /// solved this by making its unit of work smaller than its unit of data; this does the same
    /// for what stands on top of it.
    ///
    /// <para>Slicing cannot change the result. Placements are walked in catalogue order and each
    /// one is a pure function of <c>(seed, catalogue, its own position)</c> that consults no
    /// neighbour, so pausing between any two of them produces the voxels an unsliced run would.
    /// <see cref="FeatureGeneration.GenerateRegion"/> is itself this class driven to completion,
    /// which is what keeps the two from drifting apart.</para>
    /// </summary>
    public sealed class FeatureRegionBuild
    {
        private readonly int3 _regionMin;
        private readonly int3 _regionMax;
        private FeatureGenerationReport _report;
        private int _ruleIndex;
        private int _explicitIndex;

        public FeatureRegionBuild(int3 regionCoord)
        {
            RegionCoord = regionCoord;
            _regionMin = regionCoord * VoxelGrid.RegionVoxelEdge;
            _regionMax = _regionMin + VoxelGrid.RegionVoxelEdge;
        }

        public int3 RegionCoord { get; }

        /// <summary>True once every placement in the catalogue has been walked.</summary>
        public bool IsComplete { get; private set; }

        /// <summary>Totals accumulated across every slice so far.</summary>
        public FeatureGenerationReport Report => _report;

        /// <summary>
        /// Rasterises up to <paramref name="maxInstances"/> placements that actually overlap this
        /// region, then returns. Placements that miss the region are skipped without counting
        /// against the budget: rejecting one is a footprint comparison, and charging for it would
        /// spread a catalogue scan over frames while doing no work.
        /// </summary>
        /// <returns>True when the region is finished and this build can be discarded.</returns>
        public bool Step(
            in FeatureCatalogue catalogue,
            uint seed,
            IRegionReadSource reads,
            IRegionMutationStore mutations,
            int maxInstances)
        {
            if (IsComplete) return true;
            if (!catalogue.IsCreated)
            {
                IsComplete = true;
                return true;
            }

            var primitives = new NativeList<Primitive>(64, Allocator.Temp);
            var anchors = new NativeList<ResolvedAnchor>(8, Allocator.Temp);
            int rasterised = 0;

            while (_ruleIndex < catalogue.Rules.Length)
            {
                var rule = catalogue.Rules[_ruleIndex];

                if ((uint)rule.DefinitionId >= (uint)catalogue.DefinitionCount)
                {
                    _ruleIndex++;
                    _explicitIndex = 0;
                    continue;
                }

                var definition = catalogue.Definitions[rule.DefinitionId];

                while (_explicitIndex < rule.ExplicitCount)
                {
                    if (rasterised >= maxInstances)
                    {
                        primitives.Dispose();
                        anchors.Dispose();
                        return false;
                    }

                    int index = rule.ExplicitOffset + _explicitIndex;
                    _explicitIndex++;

                    if ((uint)index >= (uint)catalogue.ExplicitPlacements.Length) continue;

                    var placement = catalogue.ExplicitPlacements[index];
                    _report.InstancesConsidered++;

                    if (!FeatureGeneration.FootprintIntersects(
                            placement.Position, definition.Footprint, _regionMin, _regionMax))
                        continue;

                    FeatureGeneration.RasteriseInstance(
                        in catalogue, seed, rule.DefinitionId, in definition, in placement,
                        _regionMin, _regionMax, reads, mutations,
                        primitives, anchors, ref _report);
                    rasterised++;
                }

                _ruleIndex++;
                _explicitIndex = 0;
            }

            primitives.Dispose();
            anchors.Dispose();
            IsComplete = true;
            return true;
        }
    }
}
