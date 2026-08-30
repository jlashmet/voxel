using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private readonly Dictionary<int2, int[]> _featureLayerCache = new();
        private ulong _featureLayerCacheCatalogueHash;

        /// <summary>
        /// Queues only vertical regions occupied by explicit authored features whose oriented
        /// voxel footprint intersects an already-demanded horizontal region column.
        ///
        /// Horizontal interest remains owned by <see cref="RefreshPending"/>. This method only
        /// adds Y coordinates for that accepted X/Z column, so a tall building crossing a region
        /// boundary becomes resident without blanket-loading an extra layer around the viewer.
        /// </summary>
        private void QueueFeatureRegionsForColumn(int regionX, int regionZ)
        {
            if (!_catalogue.IsCreated) return;

            if (_featureLayerCacheCatalogueHash != _catalogue.Hash)
            {
                _featureLayerCache.Clear();
                _featureLayerCacheCatalogueHash = _catalogue.Hash;
            }

            var column = new int2(regionX, regionZ);
            if (!_featureLayerCache.TryGetValue(column, out int[] layers))
            {
                layers = FindFeatureLayersForColumn(regionX, regionZ);
                _featureLayerCache.Add(column, layers);
            }

            for (var i = 0; i < layers.Length; i++)
                QueueRegion(new int3(regionX, layers[i], regionZ));
        }

        private int[] FindFeatureLayersForColumn(int regionX, int regionZ)
        {
            int columnMinX = regionX << VoxelGrid.RegionVoxelEdgeLog2;
            int columnMinZ = regionZ << VoxelGrid.RegionVoxelEdgeLog2;
            int columnMaxX = columnMinX + RegionVoxelEdge;
            int columnMaxZ = columnMinZ + RegionVoxelEdge;
            var layers = new HashSet<int>();

            for (var ruleIndex = 0; ruleIndex < _catalogue.Rules.Length; ruleIndex++)
            {
                var rule = _catalogue.Rules[ruleIndex];
                if ((uint)rule.DefinitionId >= (uint)_catalogue.Definitions.Length) continue;
                var definition = _catalogue.Definitions[rule.DefinitionId];

                int placementEnd = math.min(
                    rule.ExplicitOffset + rule.ExplicitCount,
                    _catalogue.ExplicitPlacements.Length);
                for (int placementIndex = math.max(0, rule.ExplicitOffset);
                     placementIndex < placementEnd;
                     placementIndex++)
                {
                    var placement = _catalogue.ExplicitPlacements[placementIndex];
                    int3 footprint = definition.Footprint;
                    if ((placement.Orientation & 1) != 0)
                        footprint = new int3(footprint.z, footprint.y, footprint.x);
                    if (footprint.x <= 0 || footprint.y <= 0 || footprint.z <= 0) continue;

                    int3 origin = placement.Position;
                    int maxX = origin.x + footprint.x;
                    int maxZ = origin.z + footprint.z;
                    if (origin.x >= columnMaxX || maxX <= columnMinX
                        || origin.z >= columnMaxZ || maxZ <= columnMinZ)
                        continue;

                    int minLayer = origin.y >> VoxelGrid.RegionVoxelEdgeLog2;
                    int maxLayer = (origin.y + footprint.y - 1)
                                   >> VoxelGrid.RegionVoxelEdgeLog2;
                    for (int layer = minLayer; layer <= maxLayer; layer++)
                        layers.Add(layer);
                }
            }

            if (layers.Count == 0) return System.Array.Empty<int>();
            var result = new int[layers.Count];
            layers.CopyTo(result);
            System.Array.Sort(result);
            return result;
        }
    }
}
