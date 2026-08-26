using System;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Exact integer transform shared by Kentridge voxel emission facts. It mirrors ShapeProgram's
    /// inclusive primitive orientation: rotate both inclusive corners inside the full feature footprint,
    /// canonicalize min/max, then add the explicit placement origin.
    /// </summary>
    internal static class KentridgeVoxelPlacementTransform
    {
        private const int FoundationSinkDm = 5;

        public static Int3 WorldOrigin(
            SettlementPlan plan,
            BuildingPlot plot,
            int unitsPerDecimetre)
        {
            RequireScale(unitsPerDecimetre);
            int surfaceY = KentridgeVerticalProfile.PlotSurfaceY(plan,
                plot,
                plan.Seed,
                unitsPerDecimetre);
            return new Int3(
                plot.PositionDm.X * unitsPerDecimetre,
                surfaceY - FoundationSinkDm * unitsPerDecimetre,
                plot.PositionDm.Y * unitsPerDecimetre);
        }

        public static RealizedWorldPoint TransformPoint(
            SettlementPlan plan,
            BuildingPlot plot,
            Int3 localUnits,
            int unitsPerDecimetre)
        {
            RequireScale(unitsPerDecimetre);
            Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
            var footprintUnits = new Int3(
                footprintDm.X * unitsPerDecimetre,
                footprintDm.Y * unitsPerDecimetre,
                footprintDm.Z * unitsPerDecimetre);
            Int3 rotated = RotatePoint(localUnits, footprintUnits, (byte)plot.Frontage);
            Int3 origin = WorldOrigin(plan, plot, unitsPerDecimetre);
            return new RealizedWorldPoint(Add(origin, rotated), unitsPerDecimetre);
        }

        public static RealizedWorldBounds TransformBounds(
            SettlementPlan plan,
            BuildingPlot plot,
            HiddenSpaceBoundsDm localBoundsDm,
            int unitsPerDecimetre)
        {
            RequireScale(unitsPerDecimetre);
            var localMin = new Int3(
                localBoundsDm.MinX * unitsPerDecimetre,
                localBoundsDm.MinY * unitsPerDecimetre,
                localBoundsDm.MinZ * unitsPerDecimetre);
            var localMax = new Int3(
                localMin.X + localBoundsDm.SizeX * unitsPerDecimetre - 1,
                localMin.Y + localBoundsDm.SizeY * unitsPerDecimetre - 1,
                localMin.Z + localBoundsDm.SizeZ * unitsPerDecimetre - 1);

            Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
            var footprintUnits = new Int3(
                footprintDm.X * unitsPerDecimetre,
                footprintDm.Y * unitsPerDecimetre,
                footprintDm.Z * unitsPerDecimetre);
            Int3 a = RotatePoint(localMin, footprintUnits, (byte)plot.Frontage);
            Int3 b = RotatePoint(localMax, footprintUnits, (byte)plot.Frontage);
            var rotatedMin = new Int3(
                Math.Min(a.X, b.X),
                Math.Min(a.Y, b.Y),
                Math.Min(a.Z, b.Z));
            var rotatedMax = new Int3(
                Math.Max(a.X, b.X),
                Math.Max(a.Y, b.Y),
                Math.Max(a.Z, b.Z));
            Int3 origin = WorldOrigin(plan, plot, unitsPerDecimetre);

            return new RealizedWorldBounds(
                Add(origin, rotatedMin),
                Add(origin, rotatedMax),
                unitsPerDecimetre);
        }

        private static Int3 RotatePoint(Int3 point, Int3 footprint, byte orientation)
        {
            int maxX = footprint.X - 1;
            int maxZ = footprint.Z - 1;
            switch (orientation & 3)
            {
                case 1: return new Int3(maxZ - point.Z, point.Y, point.X);
                case 2: return new Int3(maxX - point.X, point.Y, maxZ - point.Z);
                case 3: return new Int3(point.Z, point.Y, maxX - point.X);
                default: return point;
            }
        }

        private static Int3 Add(Int3 left, Int3 right) =>
            new Int3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        private static void RequireScale(int unitsPerDecimetre)
        {
            if (unitsPerDecimetre <= 0)
                throw new ArgumentOutOfRangeException(nameof(unitsPerDecimetre));
        }
    }
}
