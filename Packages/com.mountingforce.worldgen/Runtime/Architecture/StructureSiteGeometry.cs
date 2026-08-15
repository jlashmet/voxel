using System;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Renderer-independent horizontal realization facts for a generated structure. Bounds describe
    /// the structure intent's fixed placement envelope in world decimetres; the public entrance is
    /// the architecture-resolved door anchor after the authored frontage rotation is applied.
    /// </summary>
    public readonly struct StructureSiteGeometry
    {
        public readonly Int2 FootprintMinDm;
        public readonly Int2 FootprintMaxDm;
        public readonly Int2 PublicEntranceDm;
        public readonly FrontageDirection PublicEntranceFacing;

        public StructureSiteGeometry(
            Int2 footprintMinDm,
            Int2 footprintMaxDm,
            Int2 publicEntranceDm,
            FrontageDirection publicEntranceFacing)
        {
            if (footprintMaxDm.X <= footprintMinDm.X)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));
            if (footprintMaxDm.Y <= footprintMinDm.Y)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));

            FootprintMinDm = footprintMinDm;
            FootprintMaxDm = footprintMaxDm;
            PublicEntranceDm = publicEntranceDm;
            PublicEntranceFacing = publicEntranceFacing;
        }
    }

    /// <summary>
    /// Resolves gameplay-facing site geometry from the same architectural form consumed by rendering
    /// backends. Bespoke forms intentionally fail closed until their footprint/entrance facts are
    /// promoted into the Architecture handoff as well.
    /// </summary>
    public static class StructureSiteGeometryResolver
    {
        private const int FrontInsetDm = 10;
        private const int ResidentialDoorWidthDm = 13;
        private const int ShopDoorWidthDm = 17;
        private const int DoorSideClearanceDm = 7;

        public static bool TryResolve(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form,
            out StructureSiteGeometry geometry)
        {
            if (!form.IsGenerated)
            {
                geometry = default(StructureSiteGeometry);
                return false;
            }

            ArchitectureCompiler.ValidateGenerated(intent, theme, form);

            if (intent.EnvelopeDm.X <= 0 || intent.EnvelopeDm.Z <= 0)
                throw new ArgumentException(
                    "Structure intent must provide a positive horizontal envelope.",
                    nameof(intent));

            int x0 = (intent.EnvelopeDm.X - form.WidthDm) / 2;
            int doorWidth = form.IsShop ? ShopDoorWidthDm : ResidentialDoorWidthDm;
            int doorX = x0
                      + form.WidthDm / 2
                      - doorWidth / 2
                      + form.DoorOffsetDm;
            doorX = Clamp(
                doorX,
                x0 + DoorSideClearanceDm,
                x0 + form.WidthDm - doorWidth - DoorSideClearanceDm);

            var localEntrance = new Int2(doorX + doorWidth / 2, FrontInsetDm);
            Int2 rotatedEntrance = RotatePoint(
                localEntrance,
                intent.EnvelopeDm.X,
                intent.EnvelopeDm.Z,
                (byte)intent.Frontage);

            var min = intent.PositionDm;
            var max = new Int2(
                intent.PositionDm.X + intent.EnvelopeDm.X,
                intent.PositionDm.Y + intent.EnvelopeDm.Z);
            var entrance = new Int2(
                intent.PositionDm.X + rotatedEntrance.X,
                intent.PositionDm.Y + rotatedEntrance.Y);

            geometry = new StructureSiteGeometry(min, max, entrance, intent.Frontage);
            return true;
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (maximum < minimum)
                throw new InvalidOperationException(
                    "Generated structure is too narrow to place its public door safely.");
            if (value < minimum) return minimum;
            return value > maximum ? maximum : value;
        }

        // Mirrors VoxelEngine ShapeProgram.RotatePoint without taking a VoxelEngine dependency.
        // FrontageDirection values intentionally equal the quarter-turn orientation values.
        private static Int2 RotatePoint(Int2 point, int footprintX, int footprintZ, byte orientation)
        {
            int maxX = footprintX - 1;
            int maxZ = footprintZ - 1;

            switch (orientation & 3)
            {
                case 1: return new Int2(maxZ - point.Y, point.X);
                case 2: return new Int2(maxX - point.X, maxZ - point.Y);
                case 3: return new Int2(point.Y, maxX - point.X);
                default: return point;
            }
        }
    }
}
