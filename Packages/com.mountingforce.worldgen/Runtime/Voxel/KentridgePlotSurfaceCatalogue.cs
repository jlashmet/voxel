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
    /// The grading rectangle is deliberately smaller than the semantic placement footprint: it
    /// hugs the actual building envelope plus a small yard margin, so generation does not stamp a
    /// giant rectangular terrace around every house. Subsoil is dirt and the exposed top is moss /
    /// grass-like ground cover. The market well is excluded because the plaza owns that ground.
    /// </summary>
    public static class KentridgePlotSurfaceCatalogue
    {
        private const int ArchetypeCount = 8;
        private const int FillDepthDm = 12;
        private const int SurfaceThicknessDm = 1;
        private const int ClearAboveDm = 56;
        private const int FootprintHeightDm = FillDepthDm + SurfaceThicknessDm + ClearAboveDm;

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
                    MaxPrimitives = 3,
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

        private static int[] PadProgram(StructureArchetype archetype,
                                        VoxelWorldGenSettings settings)
        {
            int s = settings.VoxelsPerDecimetre;
            PadRect pad = PadFor(archetype);
            int subsoilHeight = FillDepthDm * s;
            int topHeight = SurfaceThicknessDm * s;
            int clearY = subsoilHeight + topHeight;
            int clearHeight = ClearAboveDm * s;
            byte dirt = settings.Materials.Resolve(MaterialRole.RoadSurface);
            byte groundCover = settings.Materials.Resolve(MaterialRole.Moss);

            var b = new ProgramBuilder();
            b.Carve(pad.X * s, clearY, pad.Z * s,
                    pad.Width * s, clearHeight, pad.Depth * s);
            b.Box(pad.X * s, 0, pad.Z * s,
                  pad.Width * s, subsoilHeight, pad.Depth * s, dirt);
            b.Box(pad.X * s, subsoilHeight, pad.Z * s,
                  pad.Width * s, topHeight, pad.Depth * s, groundCover);
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
