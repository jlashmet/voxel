using System;
using MountingForce.WorldGen.Content.Kentridge;

namespace MountingForce.WorldGen.Architecture
{
    /// <summary>
    /// Renderer-independent realization facts for a structure site. Horizontal bounds describe the
    /// fixed placement envelope in world decimetres; PublicEntranceDm is the actual authored/generated
    /// door anchor after frontage rotation. PublicEntranceHeightDm is local to the structure placement
    /// origin and lets a realization backend combine this canonical door with its exact terrain Y.
    /// Non-building interaction anchors (for example Kentridge's well) are intentionally not coerced
    /// into this contract.
    /// </summary>
    public readonly struct StructureSiteGeometry
    {
        public readonly Int2 FootprintMinDm;
        public readonly Int2 FootprintMaxDm;
        public readonly Int2 PublicEntranceDm;
        public readonly int PublicEntranceHeightDm;
        public readonly FrontageDirection PublicEntranceFacing;

        public StructureSiteGeometry(
            Int2 footprintMinDm,
            Int2 footprintMaxDm,
            Int2 publicEntranceDm,
            int publicEntranceHeightDm,
            FrontageDirection publicEntranceFacing)
        {
            if (footprintMaxDm.X <= footprintMinDm.X)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));
            if (footprintMaxDm.Y <= footprintMinDm.Y)
                throw new ArgumentOutOfRangeException(nameof(footprintMaxDm));
            if (publicEntranceHeightDm < 0)
                throw new ArgumentOutOfRangeException(nameof(publicEntranceHeightDm));

            FootprintMinDm = footprintMinDm;
            FootprintMaxDm = footprintMaxDm;
            PublicEntranceDm = publicEntranceDm;
            PublicEntranceHeightDm = publicEntranceHeightDm;
            PublicEntranceFacing = publicEntranceFacing;
        }
    }

    /// <summary>
    /// Guaranteed entrance-connected open rectangle for gameplay that needs usable interior space.
    /// It is deliberately separate from site geometry so outdoor/non-interior sites remain representable
    /// without inventing room dimensions.
    /// </summary>
    public readonly struct StructureInteriorEnvelope
    {
        public readonly int HalfWidthDm;
        public readonly int DepthDm;

        public StructureInteriorEnvelope(int halfWidthDm, int depthDm)
        {
            if (halfWidthDm <= 0) throw new ArgumentOutOfRangeException(nameof(halfWidthDm));
            if (depthDm <= 0) throw new ArgumentOutOfRangeException(nameof(depthDm));
            HalfWidthDm = halfWidthDm;
            DepthDm = depthDm;
        }
    }

    /// <summary>
    /// Resolves gameplay-facing geometry from the same Architecture handoff consumed by realization
    /// backends. Generated forms use their resolved dimensions. Kentridge's legacy bespoke buildings
    /// publish their authored shell/door facts here so Composition no longer has to reach into Voxel.
    /// Unsupported bespoke content fails closed.
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
            ValidateIdentity(intent, form);

            Int2 localEntrance;
            int entranceHeightDm;
            if (form.IsGenerated)
            {
                ArchitectureCompiler.ValidateGenerated(intent, theme, form);
                localEntrance = ResolveGeneratedLocalEntrance(intent, form);
                entranceHeightDm = theme.FoundationHeightDm;
            }
            else if (!TryResolveKentridgeBespokeLocalEntrance(
                         intent,
                         out localEntrance,
                         out entranceHeightDm))
            {
                geometry = default(StructureSiteGeometry);
                return false;
            }

            Int2 rotatedEntrance = RotatePoint(
                localEntrance,
                intent.EnvelopeDm.X,
                intent.EnvelopeDm.Z,
                (byte)intent.Frontage);

            geometry = new StructureSiteGeometry(
                intent.PositionDm,
                new Int2(
                    intent.PositionDm.X + intent.EnvelopeDm.X,
                    intent.PositionDm.Y + intent.EnvelopeDm.Z),
                new Int2(
                    intent.PositionDm.X + rotatedEntrance.X,
                    intent.PositionDm.Y + rotatedEntrance.Y),
                entranceHeightDm,
                intent.Frontage);
            return true;
        }

        public static bool TryResolveInterior(
            StructureIntent intent,
            ArchitectureTheme theme,
            StructureForm form,
            out StructureInteriorEnvelope interior)
        {
            ValidateIdentity(intent, form);

            if (form.IsGenerated)
            {
                ArchitectureCompiler.ValidateGenerated(intent, theme, form);
                if (theme.WallThicknessDm <= 0)
                    throw new ArgumentException(
                        "Architecture theme must provide positive wall thickness.",
                        nameof(theme));

                int x0 = (intent.EnvelopeDm.X - form.WidthDm) / 2;
                int doorCentreX = ResolveGeneratedLocalEntrance(intent, form).X;
                int interiorMinX = x0 + theme.WallThicknessDm;
                int interiorMaxX = x0 + form.WidthDm - theme.WallThicknessDm;
                int halfWidth = Math.Min(
                    doorCentreX - interiorMinX,
                    interiorMaxX - doorCentreX);
                int depth = form.DepthDm - theme.WallThicknessDm;
                if (halfWidth <= 0 || depth <= 0)
                    throw new InvalidOperationException(
                        "Generated architecture has no usable main-floor interior behind its public entrance.");

                interior = new StructureInteriorEnvelope(halfWidth, depth);
                return true;
            }

            if (string.Equals(intent.StyleId, KentridgeDefinition.Id, StringComparison.Ordinal))
            {
                switch (intent.Archetype)
                {
                    case StructureArchetype.Warehouse:
                        interior = new StructureInteriorEnvelope(74, 137);
                        return true;
                    case StructureArchetype.Mansion:
                        interior = new StructureInteriorEnvelope(100, 183);
                        return true;
                    case StructureArchetype.Church:
                        // Bell tower overlays the front nave. This is the guaranteed 20dm-wide
                        // entrance-connected corridor through the tower, not the full nave width.
                        interior = new StructureInteriorEnvelope(10, 42);
                        return true;
                }
            }

            interior = default(StructureInteriorEnvelope);
            return false;
        }

        private static Int2 ResolveGeneratedLocalEntrance(
            StructureIntent intent,
            StructureForm form)
        {
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
            return new Int2(doorX + doorWidth / 2, FrontInsetDm);
        }

        private static bool TryResolveKentridgeBespokeLocalEntrance(
            StructureIntent intent,
            out Int2 entrance,
            out int heightDm)
        {
            if (!string.Equals(intent.StyleId, KentridgeDefinition.Id, StringComparison.Ordinal))
            {
                entrance = default(Int2);
                heightDm = 0;
                return false;
            }

            switch (intent.Archetype)
            {
                case StructureArchetype.Warehouse:
                    entrance = new Int2(94, 18);
                    heightDm = 8;
                    return true;
                case StructureArchetype.Mansion:
                    entrance = new Int2(131, 26);
                    heightDm = 9;
                    return true;
                case StructureArchetype.Church:
                    entrance = new Int2(82, 18);
                    heightDm = 8;
                    return true;
                default:
                    // Well's canonical anchor is an interaction point at (28,11,28), not an entrance.
                    entrance = default(Int2);
                    heightDm = 0;
                    return false;
            }
        }

        private static void ValidateIdentity(StructureIntent intent, StructureForm form)
        {
            if (form.RoleId != intent.RoleId
                || form.Archetype != intent.Archetype
                || form.District != intent.District)
                throw new InvalidOperationException(
                    "Architecture form does not describe the supplied structure intent identity.");
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
