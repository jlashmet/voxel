using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact physical lookup for the same Kentridge hidden-space geometries consumed by the voxel
    /// catalogue. Candidate and entrance bounds therefore identify the actual emitted voxels rather
    /// than a separately reconstructed approximation.
    /// </summary>
    public sealed class KentridgeHiddenSpaceVoxelRealizationFacts : IHiddenSpaceRealizationFacts
    {
        private readonly Dictionary<string, RealizedWorldBounds> _candidates =
            new Dictionary<string, RealizedWorldBounds>(StringComparer.Ordinal);
        private readonly Dictionary<string, RealizedWorldBounds> _entrances =
            new Dictionary<string, RealizedWorldBounds>(StringComparer.Ordinal);

        public KentridgeHiddenSpaceVoxelRealizationFacts(
            SettlementPlan plan,
            int voxelsPerDecimetre,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> geometries)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));
            if (!string.Equals(plan.Theme.Id, KentridgeDefinition.Id, StringComparison.Ordinal))
                throw new ArgumentException(
                    "Kentridge hidden-space realization facts require a Kentridge settlement plan.",
                    nameof(plan));

            var plots = new Dictionary<int, BuildingPlot>();
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (!plots.TryAdd(plot.RoleId, plot))
                    throw new InvalidOperationException(
                        "Kentridge settlement plan contains duplicate role id '" + plot.RoleId + "'.");
            }

            for (var i = 0; i < geometries.Count; i++)
            {
                KentridgeHiddenSpaceGeometry geometry = geometries[i]
                    ?? throw new InvalidOperationException(
                        "Hidden-space geometry collection contains null at index " + i + ".");
                SiteHiddenSpaceRealization realization = geometry.Realization;

                BuildingPlot plot;
                if (!plots.TryGetValue(realization.RoleId, out plot))
                    throw new InvalidOperationException(
                        "Hidden-space realization targets unknown Kentridge role '" +
                        realization.RoleId + "'.");

                RealizedWorldBounds candidateBounds =
                    KentridgeVoxelPlacementTransform.TransformBounds(
                        plan,
                        plot,
                        realization.LocalBoundsDm,
                        voxelsPerDecimetre);
                if (!_candidates.TryAdd(realization.CandidateId, candidateBounds))
                    throw new InvalidOperationException(
                        "Hidden-space candidate id appears more than once: '" +
                        realization.CandidateId + "'.");

                HiddenSpaceEntranceRealization entrance = realization.Entrance;
                RealizedWorldBounds entranceBounds =
                    KentridgeVoxelPlacementTransform.TransformBounds(
                        plan,
                        plot,
                        entrance.LocalBoundsDm,
                        voxelsPerDecimetre);
                if (!_entrances.TryAdd(entrance.Id, entranceBounds))
                    throw new InvalidOperationException(
                        "Hidden-space entrance id appears more than once: '" + entrance.Id + "'.");
            }
        }

        public bool TryGetCandidateBounds(
            string candidateId,
            out RealizedWorldBounds bounds)
        {
            if (candidateId == null)
            {
                bounds = default(RealizedWorldBounds);
                return false;
            }
            return _candidates.TryGetValue(candidateId, out bounds);
        }

        public bool TryGetEntranceBounds(
            string entranceId,
            out RealizedWorldBounds bounds)
        {
            if (entranceId == null)
            {
                bounds = default(RealizedWorldBounds);
                return false;
            }
            return _entrances.TryGetValue(entranceId, out bounds);
        }
    }
}
