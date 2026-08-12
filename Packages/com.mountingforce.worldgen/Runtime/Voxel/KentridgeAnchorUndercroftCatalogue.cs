using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Occupies the otherwise bare downhill support faces beneath large named anchors that are not
    /// already covered by KentridgeHillsideArchitectureCatalogue. Sites are derived from stable role
    /// plots, then split into bays across the plot's downhill edge; no world-space coordinates are
    /// authored here.
    /// </summary>
    public static class KentridgeAnchorUndercroftCatalogue
    {
        private const int DefinitionCount = 2;
        private const int HospitalityDefinition = 0;
        private const int WorkingDefinition = 1;
        private const int EnvelopeDm = 84;
        private const int BodyWidthDm = 76;
        private const int BodyDepthDm = 32;
        private const int BodySideInsetDm = 4;
        private const int BodyFrontInsetDm = 12;
        private const int BayGapDm = 8;
        private const int EdgeMarginDm = 8;
        private const int DownhillEdgeInsetDm = BodyFrontInsetDm;
        private const int FeatureHeightDm = 40;

        private enum UndercroftStyle : byte
        {
            Hospitality,
            Working,
        }

        private readonly struct RoleSpec
        {
            public readonly KentridgeRole Role;
            public readonly UndercroftStyle Style;
            public readonly int BayCount;
            public readonly int EmbedBelowShelfDm;

            public RoleSpec(
                KentridgeRole role,
                UndercroftStyle style,
                int bayCount,
                int embedBelowShelfDm)
            {
                Role = role;
                Style = style;
                BayCount = bayCount;
                EmbedBelowShelfDm = embedBelowShelfDm;
            }
        }

        private readonly struct Site
        {
            public readonly int DefinitionId;
            public readonly Int2 PositionDm;
            public readonly BuildingPlot Plot;
            public readonly int EmbedBelowShelfDm;

            public Site(
                int definitionId,
                Int2 positionDm,
                BuildingPlot plot,
                int embedBelowShelfDm)
            {
                DefinitionId = definitionId;
                PositionDm = positionDm;
                Plot = plot;
                EmbedBelowShelfDm = embedBelowShelfDm;
            }
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
            List<Site> sites = BuildSites(plan);
            int[] hospitalityProgram = HospitalityProgram(settings);
            int[] workingProgram = WorkingProgram(settings);
            int programLength = hospitalityProgram.Length + workingProgram.Length;

            int hospitalityCount = 0;
            for (int i = 0; i < sites.Count; i++)
                if (sites[i].DefinitionId == HospitalityDefinition) hospitalityCount++;
            int workingCount = sites.Count - hospitalityCount;

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: sites.Count,
                overrides: 0,
                allocator);

            CopyProgram(ref catalogue, 0, hospitalityProgram);
            CopyProgram(ref catalogue, hospitalityProgram.Length, workingProgram);

            int s = settings.VoxelsPerDecimetre;
            catalogue.Definitions[HospitalityDefinition] = Definition(
                "kentridge-anchor-pub-undercroft",
                0,
                hospitalityProgram.Length,
                s);
            catalogue.Definitions[WorkingDefinition] = Definition(
                "kentridge-anchor-warehouse-undercroft",
                hospitalityProgram.Length,
                workingProgram.Length,
                s);

            int placement = 0;
            WriteSites(sites, HospitalityDefinition, seed, s, ref catalogue, ref placement);
            int workingOffset = placement;
            WriteSites(sites, WorkingDefinition, seed, s, ref catalogue, ref placement);

            catalogue.Rules[HospitalityDefinition] = ExplicitRule(
                HospitalityDefinition, 0, hospitalityCount);
            catalogue.Rules[WorkingDefinition] = ExplicitRule(
                WorkingDefinition, workingOffset, workingCount);

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge anchor undercroft catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static List<Site> BuildSites(SettlementPlan plan)
        {
            RoleSpec[] specs =
            {
                // The upper inn already has a dedicated hillside annex/gallery. The lower Pub and
                // Working warehouse are the two large anchor plots whose downhill supports remained
                // visually bare in the overview passes.
                new RoleSpec(KentridgeRole.Pub, UndercroftStyle.Hospitality, 2, 27),
                new RoleSpec(KentridgeRole.Warehouse, UndercroftStyle.Working, 2, 31),
            };

            var sites = new List<Site>(4);
            for (int i = 0; i < specs.Length; i++)
            {
                RoleSpec spec = specs[i];
                BuildingPlot plot = FindPlot(plan, spec.Role);
                Int3 footprint = KentridgeDefinition.FootprintDm(plot.Archetype);
                int available = footprint.X
                              - EdgeMarginDm * 2
                              - BayGapDm * (spec.BayCount - 1);
                int bayWidth = available / spec.BayCount;
                int bayPitch = bayWidth + BayGapDm;
                if (bayWidth < BodyWidthDm)
                    throw new InvalidOperationException(
                        "Kentridge anchor plot is too narrow for undercroft bays: " + spec.Role);

                int z = plot.PositionDm.Y + footprint.Z - DownhillEdgeInsetDm;
                int firstX = plot.PositionDm.X + EdgeMarginDm;
                int definitionId = spec.Style == UndercroftStyle.Hospitality
                    ? HospitalityDefinition
                    : WorkingDefinition;

                for (int bay = 0; bay < spec.BayCount; bay++)
                {
                    int x = firstX + bay * bayPitch;
                    sites.Add(new Site(
                        definitionId,
                        new Int2(x, z),
                        plot,
                        spec.EmbedBelowShelfDm));
                }
            }

            if (sites.Count != 4)
                throw new InvalidOperationException(
                    "Kentridge anchor undercroft placement count changed unexpectedly.");
            return sites;
        }

        private static BuildingPlot FindPlot(SettlementPlan plan, KentridgeRole role)
        {
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.RoleId == (int)role) return plot;
            }

            throw new InvalidOperationException("Missing Kentridge anchor role: " + role);
        }

        private static FeatureDefinition Definition(
            string name,
            int programOffset,
            int programLength,
            int scale)
        {
            return new FeatureDefinition
            {
                Name = new FixedString64Bytes(name),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(
                    EnvelopeDm * scale,
                    FeatureHeightDm * scale,
                    EnvelopeDm * scale),
                MaxSlope = 32,
                // Above anonymous fabric/galleries, below pedestrian access, civic bridge and every
                // gameplay structure. These bays can mask bare support but never a stable role.
                Precedence = 93,
                ParameterOffset = 0,
                ParameterCount = 0,
                AnchorOffset = 0,
                AnchorCount = 0,
                SlotOffset = 0,
                SlotCount = 0,
                ProgramOffset = programOffset,
                ProgramLength = programLength,
                MaterialOffset = 0,
                MaterialCount = 0,
                MaxPrimitives = 48,
            };
        }

        private static void WriteSites(
            List<Site> sites,
            int definitionId,
            uint seed,
            int scale,
            ref FeatureCatalogue catalogue,
            ref int placement)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                Site site = sites[i];
                if (site.DefinitionId != definitionId) continue;

                int shelfSurface = KentridgeVerticalProfile.PlotSurfaceY(
                    site.Plot, seed, scale);
                catalogue.ExplicitPlacements[placement++] = new ExplicitPlacement
                {
                    Position = new int3(
                        site.PositionDm.X * scale,
                        shelfSurface - site.EmbedBelowShelfDm * scale,
                        site.PositionDm.Y * scale),
                    Orientation = 0,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
            }
        }

        private static PlacementRule ExplicitRule(int definitionId, int offset, int count)
        {
            return new PlacementRule
            {
                DefinitionId = definitionId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 1024,
                MaxSlope = 32,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static int[] HospitalityProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte wall = settings.Materials.Resolve(MaterialRole.Masonry);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte warm = settings.Materials.Resolve(MaterialRole.WarmWindow);
            byte roof = settings.Materials.Resolve(MaterialRole.RoofTile);
            var b = new ProgramBuilder();

            int x = BodySideInsetDm * s;
            int z = BodyFrontInsetDm * s;
            int w = BodyWidthDm * s;
            int d = BodyDepthDm * s;
            int foundation = 4 * s;
            int wallH = 21 * s;
            int t = 4 * s;

            b.Box(x, 0, z, w, foundation, d, stone);
            b.Box(x, foundation, z, w, wallH, d, wall);
            b.Carve(x + t, foundation, z + t,
                w - 2 * t, wallH, d - 2 * t);

            AddWindowZ(b, x + 9 * s, foundation + 8 * s, z,
                13 * s, 9 * s, t + s, warm);
            AddWindowZ(b, x + w - 22 * s, foundation + 8 * s, z,
                13 * s, 9 * s, t + s, warm);
            int doorW = 12 * s;
            int doorX = x + w / 2 - doorW / 2;
            b.Carve(doorX, foundation, z, doorW, 18 * s, t + s);

            b.Box(x, foundation, z - s, 4 * s, wallH, 3 * s, timber);
            b.Box(x + w - 4 * s, foundation, z - s, 4 * s, wallH, 3 * s, timber);
            b.Box(x, foundation + wallH - 4 * s, z - s,
                w, 4 * s, 3 * s, timber);

            // A shallow service balcony ties the lower room row into the Pub shelf above.
            b.Box(x + 4 * s, foundation + wallH - 2 * s, z - 8 * s,
                w - 8 * s, 3 * s, 12 * s, timber);
            b.Prism(x - 3 * s, foundation + wallH, z - 3 * s,
                w + 6 * s, 10 * s, d + 6 * s,
                PrismProfile.Shed, roof);
            return b.Finish();
        }

        private static int[] WorkingProgram(VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte stone = settings.Materials.Resolve(MaterialRole.DarkMasonry);
            byte timber = settings.Materials.Resolve(MaterialRole.Timber);
            byte glass = settings.Materials.Resolve(KentridgeDefinition.Theme.Window);
            byte roof = settings.Materials.Resolve(MaterialRole.Slate);
            var b = new ProgramBuilder();

            int x = BodySideInsetDm * s;
            int z = BodyFrontInsetDm * s;
            int w = BodyWidthDm * s;
            int d = BodyDepthDm * s;
            int foundation = 5 * s;
            int wallH = 21 * s;
            int t = 5 * s;

            b.Box(x, 0, z, w, foundation, d, stone);
            b.Box(x, foundation, z, w, wallH, d, timber);
            b.Carve(x + t, foundation, z + t,
                w - 2 * t, wallH, d - 2 * t);

            int cargoW = 28 * s;
            int cargoX = x + w / 2 - cargoW / 2;
            b.Carve(cargoX, foundation, z, cargoW, 18 * s, t + s);
            AddWindowZ(b, x + 8 * s, foundation + 12 * s, z,
                12 * s, 7 * s, t + s, glass);
            AddWindowZ(b, x + w - 20 * s, foundation + 12 * s, z,
                12 * s, 7 * s, t + s, glass);

            for (int i = 0; i < 4; i++)
            {
                int postX = x + math.min(w - 4 * s, i * 24 * s);
                b.Box(postX, foundation, z - s,
                    4 * s, wallH, 4 * s, stone);
            }

            b.Box(x, foundation + wallH - 3 * s, z - s,
                w, 3 * s, 4 * s, stone);
            b.Prism(x - 3 * s, foundation + wallH, z - 3 * s,
                w + 6 * s, 9 * s, d + 6 * s,
                PrismProfile.Shed, roof);
            return b.Finish();
        }

        private static void AddWindowZ(
            ProgramBuilder b,
            int x,
            int y,
            int z,
            int width,
            int height,
            int depth,
            byte material)
        {
            b.Carve(x, y, z, width, height, depth);
            b.Box(x, y, z, width, height, depth, material);
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++)
                catalogue.Program[offset + i] = program[i];
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                    material, (int)mode);
            }

            public void Carve(int x, int y, int z, int sx, int sy, int sz)
            {
                Box(x, y, z, sx, sy, sz, 0, PrimitiveMode.Carve);
            }

            public void Prism(
                int x, int y, int z,
                int sx, int sy, int sz,
                PrismProfile profile,
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitPrism, x, y, z, sx, sy, sz,
                    (int)profile, material, (int)PrimitiveMode.Fill);
            }

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
