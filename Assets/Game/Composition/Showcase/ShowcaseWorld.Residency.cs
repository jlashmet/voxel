using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    public sealed partial class ShowcaseWorld
    {
        private void QueueRegion(int3 rc)
        {
            if (_generated.Contains(rc)) return;
            if (_gen.Active && _gen.Coord.Equals(rc)) return;
            if (!_pendingLoadSet.Add(rc)) return;
            _pendingLoads.Add(rc);
        }

        private void RefreshPending(int3 centre)
        {
            _pendingLoads.Clear();
            _pendingLoadSet.Clear();

            // Residency follows the terrain surface through the vertical region stack rather
            // than pinning a single layer. An empty region still costs 1 MB of brick pointers,
            // so only the layers the ground actually crosses are loaded — plus the layer the
            // camera occupies, so standing in mid-air over a valley still has a region to
            // stand in and to collide against.
            for (int dx = -LoadRadiusRegions; dx <= LoadRadiusRegions; dx++)
            for (int dz = -LoadRadiusRegions; dz <= LoadRadiusRegions; dz++)
            {
                if (dx * dx + dz * dz > LoadRadiusRegions * LoadRadiusRegions) continue;

                int rx = centre.x + dx;
                int rz = centre.z + dz;
                SurfaceLayerSpan(rx, rz, out int minLayer, out int maxLayer);

                // Bound the span. A near-vertical column on a mountain face can legitimately
                // cross many layers, but loading an unbounded run of them stalls streaming for
                // one cliff, so the surface is followed from its floor upward and the rest is
                // left to be picked up as the viewer climbs.
                if (maxLayer - minLayer > MaxSurfaceLayersPerColumn)
                    maxLayer = minLayer + MaxSurfaceLayersPerColumn;

                for (int ry = minLayer; ry <= maxLayer; ry++)
                    QueueRegion(new int3(rx, ry, rz));

                // Feature residency is vertical-only: the existing radius decides which X/Z
                // columns are wanted, and authored feature bounds add only the Y layers those
                // accepted columns actually occupy.
                QueueFeatureRegionsForColumn(rx, rz);

                // The viewer's own layer, when the surface does not already cover it — one
                // layer, not the fill between. Extending the span to reach the camera meant
                // that standing a kilometre above the ground queued every layer in between:
                // hundreds of regions per column, none of which contain anything.
                if (centre.y < minLayer || centre.y > maxLayer)
                    QueueRegion(new int3(rx, centre.y, rz));
            }

            // The castle is atomic: its builder cannot start until every terrain region it
            // touches exists. Keep those dependencies in the same bounded queue even if a
            // future castle plan grows beyond the ordinary camera residency radius.
            if (_castleTerrainQueued && !_hasCastlePlan)
            {
                for (int i = 0; i < _castleRegions.Count; i++)
                    QueueRegion(_castleRegions[i]);
            }

            // Landmark dependencies first, then nearest camera residency. Appending castle
            // regions after sorting made a complete landmark wait behind the entire radius and
            // could never meet the startup contract.
            _pendingLoadComparer.Centre = centre;
            _pendingLoadComparer.PrioritizeCastle = _castleTerrainQueued && !_hasCastlePlan;
            _pendingLoadComparer.CastleRegions = _castleRegionSet;
            _pendingLoads.Sort(_pendingLoadComparer);
        }
    }
}
