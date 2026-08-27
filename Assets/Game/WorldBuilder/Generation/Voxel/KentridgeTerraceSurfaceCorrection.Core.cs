using System;
using Unity.Collections;
using Unity.Mathematics;

using VoxelEngine.Structures.Api;

namespace MountingForce.WorldGen.Voxel
{
    public static partial class KentridgeTerraceSurfaceCorrectionCatalogue
    {
        private const int VerticalPaddingDm = 16;
        private const int StandardMaxPrimitives = 3;
        private const int MarketTransitionMaxPrimitives = 40;
        private const int UpperTransitionMaxPrimitives = 40;

        private readonly struct Patch
        {
            public readonly string Id;
            public readonly int XDm, ZDm, WidthDm, DepthDm, ShoulderDm;
            public readonly int AnchorXDm, AnchorZDm;
            public readonly bool UrbanCore;

            public Patch(string id, int x, int z, int w, int d, int shoulder,
                         int anchorX, int anchorZ, bool urban)
            {
                Id = id; XDm = x; ZDm = z; WidthDm = w; DepthDm = d;
                ShoulderDm = shoulder; AnchorXDm = anchorX; AnchorZDm = anchorZ;
                UrbanCore = urban;
            }
        }

        public static FeatureCatalogue Build(uint seed, VoxelWorldGenSettings settings,
                                             Allocator allocator)
        {
            Patch[] patches =
            {
                new("lower-residential-main",620,900,800,190,36,1170,950,false),
                new("lower-residential-east",1460,850,240,200,36,1530,945,false),
                new("lower-middle",980,650,460,210,54,1222,760,false),
                new("working-yard",1490,570,260,250,54,1530,700,false),
                new("market-main",680,440,620,260,72,1170,520,true),
                new("market-rebecca",1240,350,180,150,72,1318,478,true),
                new("upper-shoulder",900,240,310,200,72,1118,340,true),
                new("civic-summit",920,40,470,200,72,1170,150,true),
                new("noble-ridge",1490,90,340,320,72,1530,250,true),
            };

            int scale = settings.VoxelsPerDecimetre;
            var programs = new int[patches.Length][];
            var positions = new int3[patches.Length];
            var footprints = new int3[patches.Length];
            int programLength = 0;
            for (int i = 0; i < patches.Length; i++)
            {
                ResolveBounds(patches[i], seed, scale, out positions[i], out footprints[i]);
                programs[i] = Program(patches[i], footprints[i], settings);
                programLength += programs[i].Length;
            }

            FeatureCatalogue c = FeatureCatalogueBuilder.Allocate(
                patches.Length, patches.Length, 0, 0, 0, programLength, 0,
                patches.Length, 0, allocator);
            int programOffset = 0;
            for (int i = 0; i < patches.Length; i++)
            {
                for (int p = 0; p < programs[i].Length; p++)
                    c.Program[programOffset + p] = programs[i][p];
                c.Definitions[i] = new FeatureDefinition
                {
                    Name = new FixedString64Bytes("kentridge-terrace-surface-" + patches[i].Id),
                    Kind = FeatureKind.Landform,
                    BasePlane = BasePlaneRule.FixedAltitude,
                    FixedAltitude = 0,
                    Footprint = footprints[i],
                    MaxSlope = 32,
                    Precedence = 16,
                    ProgramOffset = programOffset,
                    ProgramLength = programs[i].Length,
                    MaxPrimitives = patches[i].Id == "market-main"
                        ? MarketTransitionMaxPrimitives
                        : patches[i].Id == "upper-shoulder"
                            ? UpperTransitionMaxPrimitives
                            : StandardMaxPrimitives,
                };
                c.ExplicitPlacements[i] = new ExplicitPlacement
                {
                    Position = positions[i], Orientation = 0,
                    OverrideOffset = 0, OverrideCount = 0,
                };
                c.Rules[i] = new PlacementRule
                {
                    DefinitionId = i,
                    CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                    AttemptsPerCell = 0, AcceptProbability = 0,
                    MinAltitude = 0, MaxAltitude = 1024, MaxSlope = 32,
                    MinSpacing = 0, ClusterMin = 0, ClusterMax = 0,
                    ExclusionMask = 0, ExplicitOffset = i, ExplicitCount = 1,
                };
                programOffset += programs[i].Length;
            }

            CatalogueLoadResult load = FeatureCatalogueBuilder.Finalise(ref c);
            if (load != CatalogueLoadResult.Ok)
            {
                c.Dispose();
                throw new InvalidOperationException("Terrace surface correction failed: " + load);
            }
            return c;
        }
    }
}
