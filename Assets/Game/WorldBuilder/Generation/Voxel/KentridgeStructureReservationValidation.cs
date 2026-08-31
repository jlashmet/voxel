using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Reservation-only validation at the production settlement-to-structure realization seam.
    /// Architecture continues to own form, orientation, support and emitted pieces; this helper only
    /// asks whether the already-resolved site geometry conflicts with another semantic owner.
    /// </summary>
    public static class KentridgeStructureReservationValidation
    {
        public static void Validate(
            BuildingPlot plot,
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form,
            SpatialReservationSnapshot source)
        {
            if (source == null || plot.Archetype == StructureArchetype.Well) return;
            if (!StructureSiteGeometryResolver.TryResolve(intent, theme, form, out StructureSiteGeometry geometry))
                throw new InvalidOperationException(
                    "Kentridge production architecture could not publish site geometry for role '" +
                    plot.RoleId + "'.");

            ReservationQueryResult result = Query(
                source,
                plot.RoleId,
                geometry,
                Math.Max(1, intent.EnvelopeDm.Y));
            if (!result.IsAccepted)
                throw new InvalidOperationException(
                    "Kentridge production architecture site violates shared reservation ownership: " +
                    result.Describe());
        }

        /// <summary>
        /// Returns the bounded role-local query used by production. Only the matching settlement plot
        /// owner is removed; unrelated building, plaza and road claims remain authoritative.
        /// </summary>
        public static ReservationQueryResult Query(
            SpatialReservationSnapshot source,
            int roleId,
            in StructureSiteGeometry geometry,
            int maxYDm)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (maxYDm <= 0) throw new ArgumentOutOfRangeException(nameof(maxYDm));

            string hostOwner = "kentridge-site:" + roleId;
            var external = new List<SpatialReservation>(source.Reservations.Count);
            for (int i = 0; i < source.Reservations.Count; i++)
            {
                SpatialReservation claim = source.Reservations[i];
                if (string.Equals(claim.OwnerId, hostOwner, StringComparison.Ordinal)) continue;
                external.Add(claim);
            }

            SpatialReservationSnapshot roleSource = SpatialReservationSnapshot.Create(
                external,
                source.Window,
                source.BucketSizeDm);
            SpatialReservation site = StructureSiteReservationAdapter.SiteClearance(
                "kentridge-production-architecture-site:" + roleId,
                geometry,
                minYDm: 0,
                maxYDm: maxYDm,
                horizontalClearanceDm: 0,
                compatibleConsumers: ReservationConsumerKind.Connector,
                provenance: "KentridgeSharedStructureVoxelCatalogue | StructureSiteGeometry");
            return roleSource.Query(
                site,
                ReservationConsumerKind.StructuralChild,
                ReservationCategory.Building | ReservationCategory.Plaza | ReservationCategory.Road);
        }
    }
}
