using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Temporary coarse renderer for KentridgeUrbanMassingPlan.
    ///
    /// This layer intentionally knows almost nothing about building grammar. It converts each semantic
    /// frontage run into simple 2/3-storey roof masses so CI can validate city-scale density, skyline,
    /// and axial voids. A richer building-grammar backend can replace this class without changing the
    /// urban organisation contract.
    /// </summary>
    public static class KentridgeUrbanMassingCatalogue
    {
        private const int DefinitionCount = 2;
        private const int TwoStoreyDefinition = 0;
        private const int ThreeStoreyDefinition = 1;

        private const int ModuleWidthDm = 58;
        private const int ModuleDepthDm = 52;
        private const int ModulePitchDm = 80;
        private const int SideMarginDm = 3;
        private const int FoundationDm = 6;
        private const int FloorDm = 34;
        private const int RoofDm = 24;

        private readonly struct MassSite
        {
            public readonly int DefinitionId;
            public readonly Int2 PositionDm;
            public readonly Int2 ElevationSampleDm;
            public readonly int EmbedBelowShelfDm;
            public readonly FrontageDirection Frontage;

            public MassSite(
                int definitionId,
                Int2 positionDm,
                Int2 elevationSampleDm,
                int embedBelowShelfDm,
                FrontageDirection frontage)
            {
                DefinitionId = definitionId;
                PositionDm = positionDm;
                ElevationSampleDm = elevationSampleDm;
                EmbedBelowShelfDm = embedBelowShelfDm;
                Frontage = frontage;
            }
        }

        public static FeatureCatalogue Build(
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(seed);
            List<MassSite> twoStorey = new List<MassSite>();
            List<MassSite> threeStorey = new List<MassSite>();

            for (int i = 0; i < plan.FrontageRuns.Count; i++)
                ExpandRun(plan.FrontageRuns[i], seed, i, twoStorey, threeStorey);

            int[] twoProgram = MassProgram(2, settings);
            int[] threeProgram = MassProgram(3, settings);
            int placementCount = twoStorey.Count + threeStorey.Count;

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
                definitions: DefinitionCount,
                rules: DefinitionCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: twoProgram.Length + threeProgram.Length,
                materials: 0,
                explicitPlacements: placementCount,
                overrides: 0,
                allocator);

            CopyProgram(ref catalogue, 0, twoProgram);
            CopyProgram(ref catalogue, twoProgram.Length, threeProgram);

            int s = settings.VoxelsPerDecimetre;
            catalogue.Definitions[TwoStoreyDefinition] = Definition(
                "kentridge-urban-mass-two-storey",
                programOffset: 0,
                programLength: twoProgram.Length,
                heightDm: MassHeightDm(2),
                scale: s);
            catalogue.Definitions[ThreeStoreyDefinition] = Definition(
                "kentridge-urban-mass-three-storey",
                programOffset: twoProgram.Length,
                programLength: threeProgram.Length,
                heightDm: MassHeightDm(3),
                scale: s);

            int placement = 0;
            WriteSites(twoStorey, ref catalogue, ref placement, seed, s);
            int threeOffset = placement;
            WriteSites(threeStorey, ref catalogue, ref placement, seed, s);

            catalogue.Rules[TwoStoreyDefinition] = ExplicitRule(
                TwoStoreyDefinition, 0, twoStorey.Count);
            catalogue.Rules[ThreeStoreyDefinition] = ExplicitRule(
                ThreeStoreyDefinition, threeOffset, threeStorey.Count);

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge urban massing catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static void ExpandRun(
            KentridgeFrontageRun run,
            uint seed,
            int runIndex,
            List<MassSite> twoStorey,
            List<MassSite> threeStorey)
        {
            if (!run.IsHorizontal)
                throw new InvalidOperationException(
                    "The first Kentridge massing visual adapter currently expects horizontal runs: "
                    + run.Id);

            int targetOccupiedDm = run.LengthDm * run.CoveragePercent / 100;
            int count = Math.Max(1, (targetOccupiedDm + ModulePitchDm - 1) / ModulePitchDm);
            int startX = Math.Min(run.StartDm.X, run.EndDm.X);
            int endX = Math.Max(run.StartDm.X, run.EndDm.X);
            int z = run.StartDm.Y;

            for (int i = 0; i < count; i++)
            {
                int centreX = startX + (endX - startX) * (2 * i + 1) / (2 * count);
                int x = centreX - ModuleWidthDm / 2;
                int storeys = SelectStoreys(run, seed, runIndex, i);
                int definitionId = storeys >= 3 ? ThreeStoreyDefinition : TwoStoreyDefinition;

                var site = new MassSite(
                    definitionId,
                    new Int2(x, z),
                    run.ElevationSampleDm,
                    run.EmbedBelowShelfDm,
                    run.Frontage);

                if (definitionId == ThreeStoreyDefinition) threeStorey.Add(site);
                else twoStorey.Add(site);
            }
        }

        private static int SelectStoreys(
            KentridgeFrontageRun run,
            uint seed,
            int runIndex,
            int siteIndex)
        {
            if (run.MinStoreys == run.MaxStoreys) return run.MinStoreys;

            uint h = seed
                   ^ ((uint)(runIndex + 1) * 0x9E3779B9u)
                   ^ ((uint)(siteIndex + 1) * 0x85EBCA6Bu);
            h ^= h >> 16;
            h *= 0x7FEB352Du;
            h ^= h >> 15;
            h *= 0x846CA68Bu;
            h ^= h >> 16;

            int span = run.MaxStoreys - run.MinStoreys + 1;
            return run.MinStoreys + (int)(h % (uint)span);
        }

        private static FeatureDefinition Definition(
            string name,
            int programOffset,
            int programLength,
            int heightDm,
            int scale)
        {
            return new FeatureDefinition
            {
                Name = new FixedString64Bytes(name),
                Kind = FeatureKind.Infrastructure,
                BasePlane = BasePlaneRule.FixedAltitude,
                FixedAltitude = 0,
                Footprint = new int3(
                    (ModuleWidthDm + SideMarginDm * 2) * scale,
                    heightDm * scale,
                    (ModuleDepthDm + SideMarginDm * 2) * scale),
                MaxSlope = 32,
                // Macro mass sits above terrain/circulation and below detailed hillside fabric
                // (90+) and stable gameplay buildings (100+).
                Precedence = 86,
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
                MaxPrimitives = 4,
            };
        }

        private static void WriteSites(
            List<MassSite> sites,
            ref FeatureCatalogue catalogue,
            ref int placement,
            uint seed,
            int scale)
        {
            for (int i = 0; i < sites.Count; i++)
            {
                MassSite site = sites[i];
                int shelfSurface = KentridgeVerticalProfile.SurfaceYAtDm(
                    site.ElevationSampleDm.X,
                    site.ElevationSampleDm.Y,
                    seed,
                    scale);

                catalogue.ExplicitPlacements[placement++] = new ExplicitPlacement
                {
                    Position = new int3(
                        site.PositionDm.X * scale,
                        shelfSurface - site.EmbedBelowShelfDm * scale,
                        site.PositionDm.Y * scale),
                    Orientation = (byte)site.Frontage,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                };
            }
        }

        private static int MassHeightDm(int storeys) =>
            FoundationDm + storeys * FloorDm + RoofDm + 2;

        private static int[] MassProgram(int storeys, VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            byte foundation = settings.Materials.Resolve(MaterialRole.FoundationStone);
            byte wall = settings.Materials.Resolve(MaterialRole.Masonry);
            byte roof = settings.Materials.Resolve(MaterialRole.RoofTile);
            var b = new ProgramBuilder();

            int x = SideMarginDm * s;
            int z = SideMarginDm * s;
            int width = ModuleWidthDm * s;
            int depth = ModuleDepthDm * s;
            int foundationH = FoundationDm * s;
            int wallH = storeys * FloorDm * s;

            // Deliberately coarse: these are silhouette proxies, not final generated buildings.
            b.Box(x, 0, z, width, foundationH, depth, foundation);
            b.Box(x, foundationH, z, width, wallH, depth, wall);
            b.Prism(
                x,
                foundationH + wallH,
                z,
                width,
                RoofDm * s,
                depth,
                PrismProfile.Gable,
                roof);
            return b.Finish();
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

        private static void CopyProgram(
            ref FeatureCatalogue catalogue,
            int offset,
            int[] program)
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
                byte material)
            {
                if (sx <= 0 || sy <= 0 || sz <= 0) return;
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz,
                   material, 0, 0, (int)PrimitiveMode.Fill);
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
