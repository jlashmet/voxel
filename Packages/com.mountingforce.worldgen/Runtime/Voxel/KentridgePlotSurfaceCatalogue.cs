using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Prepares deterministic, level building pads before Kentridge structures are rasterised.
    ///
    /// Plot altitude comes from <see cref="KentridgeVerticalProfile.PlotSurfaceY"/>, sampled at the
    /// public frontage so the yard meets its authored street elevation. The flat core hugs the real
    /// building envelope. Twelve voxel-scale terraces rise away from it, preserving the same 1.2 m
    /// parcel feather while removing the conspicuous 40 cm contour shelves seen in close captures.
    /// </summary>
    public static class KentridgePlotSurfaceCatalogue
    {
        private const int ArchetypeCount = 8;
        private const int FillDepthDm = 12;
        private const int SurfaceThicknessDm = 1;
        private const int ClearAboveDm = 56;
        private const int TerraceStepDm = 1;
        private const int TerraceCount = 12;
        private const int MaxTerraceRiseDm = TerraceStepDm * TerraceCount;
        private const int FootprintHeightDm =
            FillDepthDm + SurfaceThicknessDm + MaxTerraceRiseDm + ClearAboveDm;

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

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            SettlementPlan plan = KentridgeDefinition.Build(seed);
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
                programs[i] = PadProgram((StructureArchetype)i, settings);
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
                Int3 footprintDm = KentridgeDefinition.FootprintDm(archetype);
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
                    MaxPrimitives = (TerraceCount + 1) * 3,
                };

                List<BuildingPlot> plots = byArchetype[id];
                for (int i = 0; i < plots.Count; i++)
                    catalogue.ExplicitPlacements[placementOffset + i] =
                        ResolvePlacement(plots[i], seed, scale);

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

        private static ExplicitPlacement ResolvePlacement(BuildingPlot plot, uint seed, int scale)
        {
            int targetSurface = KentridgeVerticalProfile.PlotSurfaceY(plot, seed, scale);
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

        private static PadRect Expand(PadRect source, int amount, Int3 footprint)
        {
            int x0 = math.max(0, source.X - amount);
            int z0 = math.max(0, source.Z - amount);
            int x1 = math.min(footprint.X, source.X + source.Width + amount);
            int z1 = math.min(footprint.Z, source.Z + source.Depth + amount);
            return new PadRect(x0, z0, math.max(1, x1 - x0), math.max(1, z1 - z0));
        }

        private static int[] PadProgram(StructureArchetype archetype,
                                        VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            PadRect core = PadFor(archetype);
            byte dirt = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte groundCover = settings.Materials.Resolve(MaterialRole.Moss);

            var b = new ProgramBuilder();

            // Work from the outside inward. Every inner terrace carves the previous, higher fill
            // back down, leaving a shallow voxel-scale ramp rather than four visible shelves.
            for (int terrace = TerraceCount; terrace >= 0; terrace--)
            {
                int expandDm = terrace * TerraceStepDm;
                int riseDm = terrace * TerraceStepDm;
                PadRect rect = Expand(core, expandDm, footprint);
                int subsoilHeight = (FillDepthDm + riseDm) * s;
                int topY = subsoilHeight + SurfaceThicknessDm * s;

                b.Carve(rect.X * s, topY, rect.Z * s,
                        rect.Width * s, ClearAboveDm * s, rect.Depth * s);
                b.Box(rect.X * s, 0, rect.Z * s,
                      rect.Width * s, subsoilHeight, rect.Depth * s, dirt);
                b.Box(rect.X * s, subsoilHeight, rect.Z * s,
                      rect.Width * s, SurfaceThicknessDm * s, rect.Depth * s,
                      groundCover);
            }

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
