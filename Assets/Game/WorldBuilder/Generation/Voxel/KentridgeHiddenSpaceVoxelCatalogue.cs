using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Architecture;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Physical realization of architecture-planned hidden spaces. Each cavity is emitted in the same
    /// local site coordinate system and orientation as its host building, after the host catalogue.
    /// The shared-wall doorway is carved and then refilled with the host wall material, so gameplay
    /// destruction removes a real voxel barrier rather than toggling a hidden GameObject.
    /// </summary>
    public static class KentridgeHiddenSpaceVoxelCatalogue
    {
        public const byte HiddenSpacePrecedence = 106;
        private const int FoundationSinkDm = 5;
        private const int RoofThicknessDm = 3;

        private sealed class Compiled
        {
            public KentridgeHiddenSpaceGeometry Geometry;
            public BuildingPlot Plot;
            public int[] Program;
        }

        public static FeatureCatalogue Build(
            SettlementPlan plan,
            VoxelWorldGenSettings settings,
            IReadOnlyList<KentridgeHiddenSpaceGeometry> geometries,
            Allocator allocator)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (geometries == null) throw new ArgumentNullException(nameof(geometries));
            if (geometries.Count == 0)
                throw new ArgumentException(
                    "Hidden-space voxel catalogue requires at least one realized geometry.",
                    nameof(geometries));

            var plots = new Dictionary<int, BuildingPlot>();
            for (var i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (!plots.TryAdd(plot.RoleId, plot))
                    throw new InvalidOperationException(
                        "Settlement plan contains duplicate structure role id '" + plot.RoleId + "'.");
            }

            var compiled = new Compiled[geometries.Count];
            int programLength = 0;
            for (var i = 0; i < geometries.Count; i++)
            {
                KentridgeHiddenSpaceGeometry geometry = geometries[i]
                    ?? throw new InvalidOperationException(
                        "Hidden-space geometry collection contains null at index " + i + ".");
                BuildingPlot plot;
                if (!plots.TryGetValue(geometry.Realization.RoleId, out plot))
                    throw new InvalidOperationException(
                        "Hidden-space realization targets unknown Kentridge role '" +
                        geometry.Realization.RoleId + "'.");

                int[] program = BuildProgram(plan.Theme, settings, geometry.Realization);
                compiled[i] = new Compiled
                {
                    Geometry = geometry,
                    Plot = plot,
                    Program = program,
                };
                programLength += program.Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: compiled.Length,
                rules: compiled.Length,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: compiled.Length,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int s = settings.VoxelsPerDecimetre;
            for (var i = 0; i < compiled.Length; i++)
            {
                Compiled item = compiled[i];
                for (var p = 0; p < item.Program.Length; p++)
                    catalogue.Program[programOffset + p] = item.Program[p];

                Int3 footprintDm = SettlementFootprints.For(plan, item.Plot.Archetype);
                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-hidden-" + item.Plot.RoleId + "-" + i),
                    Kind = FeatureKind.Structure,
                    BasePlane = BasePlaneRule.LowestGround,
                    Footprint = new int3(
                        footprintDm.X * s,
                        footprintDm.Y * s,
                        footprintDm.Z * s),
                    MaxSlope = 3,
                    Precedence = HiddenSpacePrecedence,
                    ProgramOffset = programOffset,
                    ProgramLength = item.Program.Length,
                    MaxPrimitives = 8,
                };

                int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plan,
                    item.Plot,
                    plan.Seed,
                    s);
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(
                        item.Plot.PositionDm.X * s,
                        targetSurface - FoundationSinkDm * s,
                        item.Plot.PositionDm.Y * s),
                    Orientation = (byte)item.Plot.Frontage,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };

                catalogue.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0,
                    AcceptProbability = 0,
                    MinAltitude = 0,
                    MaxAltitude = 1024,
                    MaxSlope = 3,
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };
                programOffset += item.Program.Length;
            }

            CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (load != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge hidden-space catalogue failed validation: " + load);
            }
            return catalogue;
        }

        private static int[] BuildProgram(
            ArchitectureTheme theme,
            VoxelWorldGenSettings settings,
            SiteHiddenSpaceRealization realization)
        {
            int s = settings.VoxelsPerDecimetre;
            int wallThickness = theme.WallThicknessDm * s;
            HiddenSpaceBoundsDm room = realization.LocalBoundsDm;
            HiddenSpaceEntranceRealization entrance = realization.Entrance;

            int x = room.MinX * s;
            int y = room.MinY * s;
            int z = room.MinZ * s;
            int width = room.SizeX * s;
            int height = room.SizeY * s;
            int depth = room.SizeZ * s;
            int foundationHeight = theme.FoundationHeightDm * s;

            byte foundation = settings.Materials.Resolve(theme.Foundation);
            byte wall = settings.Materials.Resolve(theme.Wall);
            byte roof = settings.Materials.Resolve(theme.Roof);
            var b = new ProgramBuilder();

            // Closed room shell and floor/foundation. Its inner air volume is physically present from
            // generation time; it is merely unreachable while the false-wall voxels remain intact.
            b.Box(x, 0, z, width, foundationHeight, depth, foundation);
            b.Box(x, y, z, width, height, depth, wall);
            b.Carve(
                x + wallThickness,
                y,
                z + wallThickness,
                width - 2 * wallThickness,
                height,
                depth - 2 * wallThickness);
            b.Box(
                x - 2 * s,
                y + height,
                z - 2 * s,
                width + 4 * s,
                RoofThicknessDm * s,
                depth + 4 * s,
                roof);

            // Cut the host/shared wall to establish a true after-opening traversal aperture, then put
            // the exact same wall material back into that aperture. Runtime voxel removal therefore
            // reveals the opening without needing any hidden-object state change.
            HiddenSpaceBoundsDm falseWall = entrance.LocalBoundsDm;
            int fx = falseWall.MinX * s;
            int fy = falseWall.MinY * s;
            int fz = falseWall.MinZ * s;
            int fw = falseWall.SizeX * s;
            int fh = falseWall.SizeY * s;
            int fd = falseWall.SizeZ * s;
            b.Carve(fx, fy, fz, fw, fh, fd);
            b.Box(fx, fy, fz, fw, fh, fd, wall);

            return b.Finish();
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(
                int x, int y, int z,
                int sx, int sy, int sz,
                byte material,
                PrimitiveMode mode = PrimitiveMode.Fill)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);
            }

            public void Carve(
                int x, int y, int z,
                int sx, int sy, int sz) =>
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);

            public int[] Finish()
            {
                Op(ShapeOp.End);
                return _code.ToArray();
            }

            private void Op(ShapeOp op, params int[] operands)
            {
                _code.Add((int)op);
                _code.Add(0);
                _code.AddRange(operands);
            }
        }
    }
}
