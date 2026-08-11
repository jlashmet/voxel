using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

namespace MountingForce.WorldGen.Voxel
{
    /// <summary>
    /// Prepares deterministic, level building pads before Kentridge structures are rasterised.
    ///
    /// The target altitude intentionally matches KentridgeVoxelCatalogue's existing placement
    /// rule: the lowest sampled point under the semantic footprint. Keeping one altitude rule
    /// means adding terrain preparation cannot move buildings, doors, or gameplay anchors.
    ///
    /// Pads are landforms, not structures, so their dirt remains on the smooth rendering path.
    /// The market well is excluded because the market-square pass already owns that ground.
    /// </summary>
    public static class KentridgePlotSurfaceCatalogue
    {
        private const int ArchetypeCount = 8;
        private const int FillDepthDm = 12;
        private const int SurfaceThicknessDm = 1;
        private const int ClearAboveDm = 56;
        private const int FootprintHeightDm = FillDepthDm + SurfaceThicknessDm + ClearAboveDm;

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

            FeatureCatalogue catalogue = CatalogueLoader.Allocate(
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
                    MaxPrimitives = 2,
                };

                List<BuildingPlot> plots = byArchetype[id];
                for (int i = 0; i < plots.Count; i++)
                    catalogue.ExplicitPlacements[placementOffset + i] =
                        ResolvePlacement(plots[i], seed, scale);

                catalogue.Rules[id] = ExplicitRule(id, placementOffset, plots.Count);
                programOffset += program.Length;
                placementOffset += plots.Count;
            }

            CatalogueLoadResult result = CatalogueLoader.Finalise(ref catalogue);
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
            Int3 footprintDm = KentridgeDefinition.FootprintDm(plot.Archetype);
            int3 footprint = new int3(
                footprintDm.X * scale,
                footprintDm.Y * scale,
                footprintDm.Z * scale);
            int ox = plot.PositionDm.X * scale;
            int oz = plot.PositionDm.Y * scale;
            int lowest = int.MaxValue;
            int sampleStep = math.max(8, 16 * scale);

            // Keep this sampling rule byte-for-byte equivalent to the building placement rule.
            // Plot preparation and structure placement must agree without consulting each other.
            for (int z = 0; z <= footprint.z; z += sampleStep)
            for (int x = 0; x <= footprint.x; x += sampleStep)
            {
                int h = TerrainSampler.HeightAt(ox + x, oz + z, seed);
                if (h < lowest) lowest = h;
            }

            return new ExplicitPlacement
            {
                Position = new int3(
                    ox,
                    lowest - FillDepthDm * scale,
                    oz),
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

        private static int[] PadProgram(StructureArchetype archetype,
                                        VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            Int3 footprint = KentridgeDefinition.FootprintDm(archetype);
            int fillHeight = (FillDepthDm + SurfaceThicknessDm) * s;
            int clearHeight = ClearAboveDm * s;
            byte dirt = settings.Materials.Resolve(MaterialRole.RoadSurface);

            var b = new ProgramBuilder();
            b.Carve(0, fillHeight, 0,
                    footprint.X * s, clearHeight, footprint.Z * s);
            b.Box(0, 0, 0,
                  footprint.X * s, fillHeight, footprint.Z * s, dirt);
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
                Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);

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
