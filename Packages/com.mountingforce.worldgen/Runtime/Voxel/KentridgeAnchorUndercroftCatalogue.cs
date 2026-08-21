using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Terrain.Api;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Occupies the downhill support faces beneath the two largest lower anchors with architecture
    /// whose height is derived from the actual shelf-to-natural-terrain drop. This prevents a shallow
    /// decorative undercroft from leaving the lower half of a tall terrace as a bare brown pedestal.
    /// </summary>
    public static class KentridgeAnchorUndercroftCatalogue
    {
        private const int DefinitionCount = 2;
        private const int HospitalityDefinition = 0;
        private const int WorkingDefinition = 1;
        private const int EnvelopeDm = 84;
        private const int BodyWidthDm = 76;
        private const int BodyDepthDm = 22;
        private const int BodySideInsetDm = 4;
        private const int BodyFrontInsetDm = 12;
        private const int BayGapDm = 8;
        private const int EdgeMarginDm = 8;
        private const int DownhillEdgeInsetDm = BodyFrontInsetDm;
        private const int RoofAllowanceDm = 12;
        private const int MinSupportHeightDm = 28;
        private const int MaxSupportHeightDm = 78;
        private const int NaturalSampleBeyondEdgeDm = 18;

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

            public RoleSpec(KentridgeRole role, UndercroftStyle style, int bayCount)
            {
                Role = role;
                Style = style;
                BayCount = bayCount;
            }
        }

        private readonly struct Site
        {
            public readonly int DefinitionId;
            public readonly Int2 PositionDm;
            public readonly BuildingPlot Plot;

            public Site(int definitionId, Int2 positionDm, BuildingPlot plot)
            {
                DefinitionId = definitionId;
                PositionDm = positionDm;
                Plot = plot;
            }
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            BuildingPlot pubPlot = FindPlot(plan, KentridgeRole.Pub);
            BuildingPlot warehousePlot = FindPlot(plan, KentridgeRole.Warehouse);
            int s = settings.VoxelsPerDecimetre;

            int hospitalityHeightDm = SupportHeightDm(plan, pubPlot, seed, s);
            int workingHeightDm = SupportHeightDm(plan, warehousePlot, seed, s);
            List<Site> sites = BuildSites(plan, pubPlot, warehousePlot);
            int[] hospitalityProgram = HospitalityProgram(settings, hospitalityHeightDm);
            int[] workingProgram = WorkingProgram(settings, workingHeightDm);
            int programLength = hospitalityProgram.Length + workingProgram.Length;

            int hospitalityCount = 0;
            for (int i = 0; i < sites.Count; i++)
                if (sites[i].DefinitionId == HospitalityDefinition) hospitalityCount++;
            int workingCount = sites.Count - hospitalityCount;

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
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

            catalogue.Definitions[HospitalityDefinition] = Definition(
                "kentridge-anchor-pub-undercroft",
                0,
                hospitalityProgram.Length,
                hospitalityHeightDm,
                s);
            catalogue.Definitions[WorkingDefinition] = Definition(
                "kentridge-anchor-warehouse-undercroft",
                hospitalityProgram.Length,
                workingProgram.Length,
                workingHeightDm,
                s);

            int placement = 0;
            WriteSites(plan, sites, HospitalityDefinition, hospitalityHeightDm,
                seed, s, ref catalogue, ref placement);
            int workingOffset = placement;
            WriteSites(plan, sites, WorkingDefinition, workingHeightDm,
                seed, s, ref catalogue, ref placement);

            catalogue.Rules[HospitalityDefinition] = ExplicitRule(
                HospitalityDefinition, 0, hospitalityCount);
            catalogue.Rules[WorkingDefinition] = ExplicitRule(
                WorkingDefinition, workingOffset, workingCount);

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge anchor undercroft catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static List<Site> BuildSites(
            SettlementPlan plan,
            BuildingPlot pubPlot,
            BuildingPlot warehousePlot)
        {
            RoleSpec[] specs =
            {
                new RoleSpec(KentridgeRole.Pub, UndercroftStyle.Hospitality, 2),
                new RoleSpec(KentridgeRole.Warehouse, UndercroftStyle.Working, 2),
            };
            BuildingPlot[] plots = { pubPlot, warehousePlot };

            var sites = new List<Site>(4);
            for (int i = 0; i < specs.Length; i++)
            {
                RoleSpec spec = specs[i];
                BuildingPlot plot = plots[i];
                Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
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
                    sites.Add(new Site(
                        definitionId,
                        new Int2(firstX + bay * bayPitch, z),
                        plot));
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

        private static int SupportHeightDm(SettlementPlan plan, BuildingPlot plot, uint seed, int scale)
        {
            Int3 footprint = SettlementFootprints.For(plan, plot.Archetype);
            int centreXDm = plot.PositionDm.X + footprint.X / 2;
            int downhillZDm = plot.PositionDm.Y + footprint.Z + NaturalSampleBeyondEdgeDm;
            int shelfSurface = KentridgeVerticalProfile.PlotSurfaceY(plan, plot, seed, scale);
            int naturalSurface = TerrainQuery.HeightAt(
                centreXDm * scale,
                downhillZDm * scale,
                seed);

            int dropVoxels = math.max(0, shelfSurface - naturalSurface);
            int dropDm = (dropVoxels + scale - 1) / scale;

            // Sink several decimetres into natural ground so the facade visibly grows from terrain
            // rather than hovering at the sampled boundary. The cap prevents pathological terrain
            // noise from creating a tower below an otherwise ordinary anchor.
            return math.clamp(dropDm + 5, MinSupportHeightDm, MaxSupportHeightDm);
        }

        private static FeatureDefinition Definition(
            string name,
            int programOffset,
            int programLength,
            int supportHeightDm,
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
                    (supportHeightDm + RoofAllowanceDm) * scale,
                    EnvelopeDm * scale),
                MaxSlope = 32,
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
                MaxPrimitives = 72,
            };
        }

        private static void WriteSites(
            SettlementPlan plan,
            List<Site> sites,
            int definitionId,
            int supportHeightDm,
            uint seed,
            int scale,
            ref FeatureCatalogue catalogue,
            ref int placement)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                Site site = sites[i];
                if (site.DefinitionId != definitionId) continue;

                int shelfSurface = KentridgeVerticalProfile.PlotSurfaceY(plan,
                    site.Plot, seed, scale);
                catalogue.ExplicitPlacements[placement++] = new ExplicitPlacement
                {
                    Position = new int3(
                        site.PositionDm.X * scale,
                        shelfSurface - supportHeightDm * scale,
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

        private static int[] HospitalityProgram(
            VoxelWorldGenSettings settings,
            int supportHeightDm)
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
            int foundation = 5 * s;
            int bodyH = supportHeightDm * s;
            int wallH = bodyH - foundation;
            int t = 4 * s;

            b.Box(x, 0, z, w, foundation, d, stone);
            b.Box(x, foundation, z, w, wallH, d, wall);
            b.Carve(x + t, foundation, z + t,
                w - 2 * t, wallH, d - 2 * t);

            // Each ~2.2 m band becomes a believable lower service floor. The top level aligns with
            // the Pub shelf, turning the entire exposed face into occupied architecture.
            int levelStep = 22 * s;
            for (int levelY = foundation; levelY + 17 * s < bodyH; levelY += levelStep)
            {
                AddWindowZ(b, x + 9 * s, levelY + 7 * s, z,
                    13 * s, 9 * s, t + s, warm);
                AddWindowZ(b, x + w - 22 * s, levelY + 7 * s, z,
                    13 * s, 9 * s, t + s, warm);
                b.Box(x, levelY + levelStep - 3 * s, z - s,
                    w, 3 * s, 3 * s, timber);
            }

            int doorW = 12 * s;
            int doorX = x + w / 2 - doorW / 2;
            b.Carve(doorX, foundation, z, doorW, 18 * s, t + s);
            b.Box(x, foundation, z - s, 4 * s, wallH, 3 * s, timber);
            b.Box(x + w - 4 * s, foundation, z - s, 4 * s, wallH, 3 * s, timber);

            b.Box(x + 4 * s, bodyH - 3 * s, z - 8 * s,
                w - 8 * s, 3 * s, 12 * s, timber);
            b.Prism(x - 3 * s, bodyH, z - 3 * s,
                w + 6 * s, 10 * s, d + 6 * s,
                PrismProfile.Shed, roof);
            return b.Finish();
        }

        private static int[] WorkingProgram(
            VoxelWorldGenSettings settings,
            int supportHeightDm)
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
            int foundation = 6 * s;
            int bodyH = supportHeightDm * s;
            int wallH = bodyH - foundation;
            int t = 5 * s;

            b.Box(x, 0, z, w, foundation, d, stone);
            b.Box(x, foundation, z, w, wallH, d, timber);
            b.Carve(x + t, foundation, z + t,
                w - 2 * t, wallH, d - 2 * t);

            int cargoW = 28 * s;
            int cargoX = x + w / 2 - cargoW / 2;
            b.Carve(cargoX, foundation, z, cargoW, 20 * s, t + s);

            int levelStep = 24 * s;
            for (int levelY = foundation; levelY + 18 * s < bodyH; levelY += levelStep)
            {
                AddWindowZ(b, x + 8 * s, levelY + 10 * s, z,
                    12 * s, 7 * s, t + s, glass);
                AddWindowZ(b, x + w - 20 * s, levelY + 10 * s, z,
                    12 * s, 7 * s, t + s, glass);
                b.Box(x, levelY + levelStep - 3 * s, z - s,
                    w, 3 * s, 4 * s, stone);
            }

            for (int i = 0; i < 4; i++)
            {
                int postX = x + math.min(w - 4 * s, i * 24 * s);
                b.Box(postX, foundation, z - s,
                    4 * s, wallH, 4 * s, stone);
            }

            b.Prism(x - 3 * s, bodyH, z - 3 * s,
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
                    material, 0, 0, (int)mode);
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
                    (int)profile, material, 0, 0, (int)PrimitiveMode.Fill);
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
