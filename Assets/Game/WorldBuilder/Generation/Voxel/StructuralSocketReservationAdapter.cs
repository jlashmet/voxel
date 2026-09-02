using System;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Bridges an already-solved typed structural attachment into the shared WorldBuilder spatial
    /// reservation service. Structural composition remains authoritative for compatibility,
    /// orientation, topology, support and sibling clearance; this adapter only checks the resolved
    /// socket clearance against external WorldBuilder claims.
    /// </summary>
    public static class StructuralSocketReservationAdapter
    {
        public static SpatialReservation ClearanceClaim(
            in SlotSpec socket,
            in StructuralAttachmentInspection inspection,
            byte parentOrientation,
            int voxelsPerDecimetre,
            string ownerId,
            int precedence = 40,
            string provenance = "Structural composition typed socket clearance")
        {
            if (!inspection.Accepted)
                throw new ArgumentException("Only an accepted structural attachment has resolved socket clearance.", nameof(inspection));
            if (socket.SocketId == 0 || inspection.SocketId != socket.SocketId)
                throw new ArgumentException("Structural attachment inspection does not match the supplied socket.", nameof(inspection));
            if (parentOrientation > 3)
                throw new ArgumentOutOfRangeException(nameof(parentOrientation));
            if (voxelsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(voxelsPerDecimetre));

            int3 voxelMin = inspection.AttachmentPosition + RotateVector(socket.ClearanceMin, parentOrientation);
            int3 voxelMax = inspection.AttachmentPosition + RotateVector(socket.ClearanceMax, parentOrientation);
            Normalize(ref voxelMin, ref voxelMax);
            if (voxelMax.x <= voxelMin.x || voxelMax.y <= voxelMin.y || voxelMax.z <= voxelMin.z)
                throw new ArgumentException("Typed socket must publish a non-empty 3D clearance volume.", nameof(socket));

            var bounds = new ReservationBoundsDm(
                FloorDiv(voxelMin.x, voxelsPerDecimetre),
                FloorDiv(voxelMin.y, voxelsPerDecimetre),
                FloorDiv(voxelMin.z, voxelsPerDecimetre),
                CeilDiv(voxelMax.x, voxelsPerDecimetre),
                CeilDiv(voxelMax.y, voxelsPerDecimetre),
                CeilDiv(voxelMax.z, voxelsPerDecimetre));
            return WorldBuilderReservationFactory.StructuralChildClearance(
                ownerId,
                bounds,
                precedence,
                ReservationConsumerKind.Connector,
                provenance);
        }

        public static ReservationQueryResult QueryClearance(
            SpatialReservationSnapshot snapshot,
            in SlotSpec socket,
            in StructuralAttachmentInspection inspection,
            byte parentOrientation,
            int voxelsPerDecimetre,
            ReservationCategory categoryMask,
            string ownerId,
            int precedence = 40,
            string provenance = "Structural composition typed socket clearance")
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            SpatialReservation claim = ClearanceClaim(
                in socket,
                in inspection,
                parentOrientation,
                voxelsPerDecimetre,
                ownerId,
                precedence,
                provenance);
            return snapshot.Query(claim, ReservationConsumerKind.StructuralChild, categoryMask);
        }

        private static int3 RotateVector(int3 value, byte orientation)
        {
            switch (orientation)
            {
                case 1: return new int3(-value.z, value.y, value.x);
                case 2: return new int3(-value.x, value.y, -value.z);
                case 3: return new int3(value.z, value.y, -value.x);
                default: return value;
            }
        }

        private static void Normalize(ref int3 min, ref int3 max)
        {
            int3 originalMin = min;
            min = math.min(originalMin, max);
            max = math.max(originalMin, max);
        }

        private static int FloorDiv(int value, int divisor)
        {
            int quotient = value / divisor;
            int remainder = value % divisor;
            return remainder != 0 && value < 0 ? quotient - 1 : quotient;
        }

        private static int CeilDiv(int value, int divisor) => -FloorDiv(-value, divisor);
    }
}
