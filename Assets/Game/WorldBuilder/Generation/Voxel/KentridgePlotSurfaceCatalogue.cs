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
    /// Prepares deterministic, level building pads before Kentridge structures are rasterised.
    ///
    /// Organic Kentridge generated houses use the exact foundation rectangle resolved by the same
    /// architecture form that the shared-house compiler consumes. Their support volume remains a
    /// rectangular Dirt grade, while the visible Moss cap is rounded at the corners so a house pad
    /// meeting organic circulation cannot stamp a metre-scale right-angle grass tongue into the road.
    /// Legacy layouts and bespoke/non-generated pads retain their established rectangular surface.
    /// </summary>
    public static class KentridgePlotSurfaceCatalogue
    {
        private const int ArchetypeCount = 8;
        private const int FillDepthDm = 12;
        private const int SurfaceThicknessDm = 1;
        private const int ClearAboveDm = 56;
        private const int FootprintHeightDm = FillDepthDm + SurfaceThicknessDm + ClearAboveDm;
        private const int SharedHouseFrontInsetDm = 10;
        private const int OrganicCapCornerRadiusDm = 12;

        private readonly struct PadRect
        {
            public readonly int X;
            public readonly int Z;
            public readonly int Width;
            public readonly int Depth;

            public PadRect(int x, int z, int width, int depth)
            {
                X = x;
                Z = z;
                Width = width;
                Depth = depth;
            }
        }

        private readonly struct OrganicPadEntry
        {
            public readonly BuildingPlot Plot;
            public readonly PadRect Pad;
            public readonly int[] Program;

            public OrganicPadEntry(BuildingPlot plot, PadRect pad, int[] program)
            {
                Plot = plot;
                Pad = pad;
                Program = program;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = SettlementVoxelPlan.Resolve(seed, in settings);
            bool organicKentridge = plan.Theme.Id == KentridgeDefinition.Id
                                  && plan.Routes.Count > 0;
            return organicKentridge
                ? BuildOrganicKentridge(plan, seed, settings, allocator)
                : BuildLegacy(plan, seed, settings, allocator);
        }

        private static FeatureCatalogue BuildOrganicKentridge(
            SettlementPlan plan,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            var entries = new List<OrganicPadEntry>(plan.Plots.Count);
            int programLength = 0;

            // Preserve the historical archetype-major placement order. The vertical placement
            // adapter walks the same order when it applies the exact scene elevation profile.
            for (int archetype = 0; archetype < ArchetypeCount; archetype++)
            {
                for (int i = 0; i < plan.Plots.Count; i++)
                {
                    BuildingPlot plot = plan.Plots[i];
                    if ((int)plot.Archetype != archetype
                        || plot.Archetype == StructureArchetype.Well)
                        continue;

                    PadRect pad = OrganicPadFor(plan, plot, seed, out bool generatedHouse);
                    int[] program = PadProgram(pad, settings, generatedHouse);
                    entries.Add(new OrganicPadEntry(plot, pad, program));
                    programLength += program.Length;
                }
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: entries.Count,
                rules: entries.Count,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: entries.Count,
                overrides: 0,
                allocator);

            int programOffset = 0;
            for (int id = 0; id < entries.Count; id++)
            {
                OrganicPadEntry entry = entries[id];
                BuildingPlot plot = entry.Plot;
                Int3 footprintDm = SettlementFootprints.For(plan, plot.Archetype);
                CopyProgram(ref catalogue, programOffset, entry.Program);

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-plot-" + ((KentridgeRole)plot.RoleId).ToString().ToLowerInvariant()),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        footprintDm.X * scale,
                        FootprintHeightDm * scale,
                        footprintDm.Z * scale),
                    MaxSlope = 16,
                    Precedence = 40,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = entry.Program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 3,
                };

                catalogue.ExplicitPlacements[id] = ResolvePlacement(plan, plot, seed, scale);
                catalogue.Rules[id] = ExplicitRule(id, id, 1);
                programOffset += entry.Program.Length;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Organic Kentridge plot surface catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static FeatureCatalogue BuildLegacy(
            SettlementPlan plan,
            uint seed,
            VoxelWorldGenSettings settings,
            Allocator allocator)
        {
            int scale = settings.VoxelsPerDecimetre;
            var byArchetype = new List<BuildingPlot>[ArchetypeCount];
            for (int i = 0; i < ArchetypeCount; i++) byArchetype[i] = new List<BuildingPlot>();

            int placementCount = 0;
            for (int i = 0; i < plan.Plots.Count; i++)
            {
                BuildingPlot plot = plan.Plots[i];
                if (plot.Archetype == StructureArchetype.Well) continue;
                byArchetype[(int)plot.Archetype].Add(plot);
                placementCount++;
            }

            var programs = new int[ArchetypeCount][];
            int programLength = 0;
            for (int i = 0; i < ArchetypeCount; i++)
            {
                programs[i] = PadProgram(PadFor((StructureArchetype)i), settings, false);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(
                definitions: ArchetypeCount,
                rules: ArchetypeCount,
                parameters: 0,
                anchors: 0,
                slots: 0,
                programLength: programLength,
                materials: 0,
                explicitPlacements: placementCount,
                overrides: 0,
                allocator);

            int programOffset = 0;
            int placementOffset = 0;
            for (int id = 0; id < ArchetypeCount; id++)
            {
                StructureArchetype archetype = (StructureArchetype)id;
                Int3 footprintDm = SettlementFootprints.For(plan, archetype);
                int[] program = programs[id];
                CopyProgram(ref catalogue, programOffset, program);

                catalogue.Definitions[id] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes(
                        "kentridge-plot-" + archetype.ToString().ToLowerInvariant()),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(
                        footprintDm.X * scale,
                        FootprintHeightDm * scale,
                        footprintDm.Z * scale),
                    MaxSlope = 16,
                    Precedence = 40,
                    ParameterOffset = 0,
                    ParameterCount = 0,
                    AnchorOffset = 0,
                    AnchorCount = 0,
                    SlotOffset = 0,
                    SlotCount = 0,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaterialOffset = 0,
                    MaterialCount = 0,
                    MaxPrimitives = 3,
                };

                List<BuildingPlot> plots = byArchetype[id];
                for (int i = 0; i < plots.Count; i++)
                    catalogue.ExplicitPlacements[placementOffset + i] =
                        ResolvePlacement(plan, plots[i], seed, scale);

                catalogue.Rules[id] = ExplicitRule(id, placementOffset, plots.Count);
                programOffset += program.Length;
                placementOffset += plots.Count;
            }

            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException(
                    "Kentridge plot surface catalogue failed validation: " + result);
            }

            return catalogue;
        }

        private static PadRect OrganicPadFor(
            SettlementPlan plan, BuildingPlot plot, uint seed, out bool generatedHouse)
        {
            StructureIntent intent = KentridgeDefinition.StructureIntent(plot);
            StructureForm form = ArchitectureCompiler.Resolve(intent, plan.Theme, seed);
            generatedHouse = form.IsGenerated;
            if (!generatedHouse)
                return PadFor(plot.Archetype);

            Int3 envelope = SettlementFootprints.For(plan, plot.Archetype);
            int x = (envelope.X - form.WidthDm) / 2;
            return new PadRect(x, SharedHouseFrontInsetDm, form.WidthDm, form.DepthDm);
        }

        private static ExplicitPlacement ResolvePlacement(
            SettlementPlan plan, BuildingPlot plot, uint seed, int scale)
        {
            int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plan, plot, seed, scale);
            return new ExplicitPlacement
            {
                Position = new int3(
                    plot.PositionDm.X * scale,
                    targetSurface - FillDepthDm * scale,
                    plot.PositionDm.Y * scale),
                Orientation = (byte)plot.Frontage,
                OverrideOffset = 0,
                OverrideCount = 0,
            };
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
                MaxSlope = 16,
                MinSpacing = 0,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = offset,
                ExplicitCount = count,
            };
        }

        private static PadRect PadFor(StructureArchetype archetype)
        {
            switch (archetype)
            {
                case StructureArchetype.Townhouse: return new PadRect(6, 4, 90, 88);
                case StructureArchetype.WideHouse: return new PadRect(6, 4, 116, 100);
                case StructureArchetype.Shop:      return new PadRect(4, 0, 116, 102);
                case StructureArchetype.Inn:       return new PadRect(6, 6, 166, 158);
                case StructureArchetype.Warehouse: return new PadRect(7, 10, 182, 174);
                case StructureArchetype.Mansion:   return new PadRect(12, 0, 244, 236);
                case StructureArchetype.Church:    return new PadRect(12, 8, 140, 148);
                case StructureArchetype.Well:      return new PadRect(0, 0, 56, 56);
                default:                           return new PadRect(4, 4, 96, 96);
            }
        }

        private static int[] PadProgram(
            PadRect core, VoxelWorldGenSettings settings, bool roundVisibleSurface)
        {
            int s = settings.VoxelsPerDecimetre;
            byte dirt = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte groundCover = settings.Materials.Resolve(MaterialRole.Moss);
            int subsoilHeight = FillDepthDm * s;
            int topY = subsoilHeight + SurfaceThicknessDm * s;

            var b = new ProgramBuilder();
            b.Carve(core.X * s, topY, core.Z * s,
                    core.Width * s, ClearAboveDm * s, core.Depth * s);

            if (!roundVisibleSurface)
            {
                b.Box(core.X * s, 0, core.Z * s,
                      core.Width * s, subsoilHeight, core.Depth * s, dirt);
                b.Box(core.X * s, subsoilHeight, core.Z * s,
                      core.Width * s, SurfaceThicknessDm * s, core.Depth * s,
                      groundCover);
                return b.Finish();
            }

            // Keep the exact historical support elevation and rectangular foundation grade. Only
            // material ownership at the visible surface changes: the full top voxel is Dirt first,
            // then a rounded material-only pass restores Moss away from each rectangular corner.
            // PaintSurface creates no occupancy, so structural support and clearance are unchanged.
            b.Box(core.X * s, 0, core.Z * s,
                  core.Width * s, topY, core.Depth * s, dirt);

            int surfaceY = topY - 1;
            int radius = Math.Min(
                OrganicCapCornerRadiusDm * s,
                (Math.Min(core.Width * s, core.Depth * s) - 1) / 2);
            int paintMinY = Math.Max(0, surfaceY - radius);
            int paintHeight = (surfaceY - paintMinY) + radius + 1;
            b.RoundedSurface(
                core.X * s, paintMinY, core.Z * s,
                core.Width * s, paintHeight, core.Depth * s,
                radius, groundCover);
            return b.Finish();
        }

        private static void CopyProgram(ref FeatureCatalogue catalogue, int offset, int[] program)
        {
            for (int i = 0; i < program.Length; i++) catalogue.Program[offset + i] = program[i];
        }

        private sealed class ProgramBuilder
        {
            private readonly List<int> _code = new List<int>();

            public void Box(int x, int y, int z, int sx, int sy, int sz, byte material,
                            PrimitiveMode mode = PrimitiveMode.Fill) =>
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, 0, 0, (int)mode);

            public void RoundedSurface(
                int x, int y, int z, int sx, int sy, int sz, int radius, byte material) =>
                Op(ShapeOp.EmitRoundedBox,
                   x, y, z, sx, sy, sz, radius,
                   material, 0, 0,
                   (int)PrimitiveMode.PaintSurface);

            public void Carve(int x, int y, int z, int sx, int sy, int sz) =>
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
