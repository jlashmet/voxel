using System;
using System.Collections.Generic;
using MountingForce.WorldGen.Content.Kentridge;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static class KentridgeUrbanCourtCatalogue
    {
        public const int CourtEdgeInsetDm = 4;
        public const int SurfaceThicknessDm = 2;
        public const byte CourtPrecedence = 85;

        private readonly struct CourtBuild
        {
            public readonly string Id;
            public readonly KentridgeUrbanBlock Block;
            public readonly Int2 MinDm;
            public readonly Int2 MaxDm;
            public CourtBuild(KentridgeUrbanBlock block)
            {
                Id = block.Id + "-court";
                Block = block;
                MinDm = new Int2(block.InteriorMinDm.X + CourtEdgeInsetDm, block.InteriorMinDm.Y + CourtEdgeInsetDm);
                MaxDm = new Int2(block.InteriorMaxDm.X - CourtEdgeInsetDm, block.InteriorMaxDm.Y - CourtEdgeInsetDm);
            }
            public int WidthDm => MaxDm.X - MinDm.X;
            public int DepthDm => MaxDm.Y - MinDm.Y;
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings, Allocator allocator)
        {
            KentridgeUrbanMassingPlan plan = KentridgeUrbanOrganizer.Build(seed);
            var builds = new CourtBuild[plan.Blocks.Count];
            var programs = new int[plan.Blocks.Count][];
            int programLength = 0;
            byte stone = settings.Materials.Resolve(MaterialRole.FoundationStone);
            int s = settings.VoxelsPerDecimetre;
            for (int i = 0; i < plan.Blocks.Count; i++)
            {
                builds[i] = new CourtBuild(plan.Blocks[i]);
                if (builds[i].WidthDm <= 0 || builds[i].DepthDm <= 0)
                    throw new InvalidOperationException("Kentridge court inset consumed protected void: " + builds[i].Id);
                programs[i] = CourtProgram(builds[i], stone, s);
                programLength += programs[i].Length;
            }

            FeatureCatalogue catalogue = FeatureCatalogueBuilder.Allocate(builds.Length, builds.Length, 0, 0, 0, programLength, 0, builds.Length, 0, allocator);
            int programOffset = 0;
            for (int i = 0; i < builds.Length; i++)
            {
                CourtBuild build = builds[i];
                int[] program = programs[i];
                for (int p = 0; p < program.Length; p++) catalogue.Program[programOffset + p] = program[p];
                catalogue.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-" + build.Id),
                    Kind = FeatureKind.Infrastructure,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = new int3(build.WidthDm * s, SurfaceThicknessDm * s, build.DepthDm * s),
                    MaxSlope = 32,
                    Precedence = CourtPrecedence,
                    ProgramOffset = programOffset,
                    ProgramLength = program.Length,
                    MaxPrimitives = 2,
                };
                int shelfY = KentridgeVerticalProfile.SurfaceYAtDm(build.Block.ElevationSampleDm.X, build.Block.ElevationSampleDm.Y, seed, s);
                catalogue.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = new int3(build.MinDm.X * s, shelfY - SurfaceThicknessDm * s, build.MinDm.Y * s),
                    Orientation = 0,
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
                    MaxSlope = 32,
                    ExplicitOffset = i,
                    ExplicitCount = 1,
                };
                programOffset += program.Length;
            }
            CatalogueLoadResult result = FeatureCatalogueBuilder.Finalise(ref catalogue);
            if (result != CatalogueLoadResult.Ok)
            {
                catalogue.Dispose();
                throw new InvalidOperationException("Kentridge urban court catalogue failed validation: " + result);
            }
            return catalogue;
        }

        private static int[] CourtProgram(CourtBuild build, byte material, int scale)
        {
            var code = new List<int>(16);
            Op(code, ShapeOp.EmitBox, 0, 0, 0, build.WidthDm * scale, SurfaceThicknessDm * scale, build.DepthDm * scale, material, 0, 0, (int)PrimitiveMode.Fill);
            Op(code, ShapeOp.End);
            return code.ToArray();
        }

        private static void Op(List<int> code, ShapeOp op, params int[] operands)
        {
            code.Add((int)op);
            code.Add(0);
            code.AddRange(operands);
        }
    }
}
