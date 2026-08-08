using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Terrain;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A catalogue for the showcase: one cottage definition, placed explicitly a few times near
    /// spawn so the parametric output can be judged by walking up to it.
    ///
    /// Written in code because the authoring text format has no compiler yet — that is US8. This
    /// is the shape a designer will eventually express in a file, not the shape the engine
    /// requires.
    ///
    /// Placement altitudes are sampled here rather than adapted automatically: terrain adaptation
    /// is US3. Until then an explicitly placed feature carries its own ground height, which is
    /// exactly what an author would do for a landmark that has to be in a particular spot.
    /// </summary>
    public static class ShowcaseCatalogue
    {
        public const int CottageId = 0;

        private const byte Stone = ShowcaseWorld.MatStone;
        private const byte Wood = ShowcaseWorld.MatWood;
        private const byte Glass = ShowcaseWorld.MatGlass;

        /// <summary>Cottage footprint in voxels — 9.6 m square, 8 m tall.</summary>
        private static readonly int3 Footprint = new(96, 80, 96);

        public static FeatureCatalogue Build(uint seed, Allocator allocator)
        {
            var program = CottageProgram();
            var placements = PlacementSites(seed);

            var catalogue = CatalogueLoader.Allocate(
                definitions: 1,
                rules: 1,
                parameters: 0,
                anchors: 2,
                slots: 0,
                programLength: program.Length,
                materials: 3,
                explicitPlacements: placements.Count,
                overrides: 0,
                allocator);

            for (var i = 0; i < program.Length; i++) catalogue.Program[i] = program[i];

            catalogue.Anchors[0] = new AnchorSpec
            {
                Name = "door", LocalPosition = new int3(32, 0, 0), Facing = Facing.South,
            };
            catalogue.Anchors[1] = new AnchorSpec
            {
                Name = "hearth", LocalPosition = new int3(32, 4, 32), Facing = Facing.Up,
            };

            catalogue.Materials[0] = Stone;
            catalogue.Materials[1] = Wood;
            catalogue.Materials[2] = Glass;

            catalogue.Definitions[CottageId] = new FeatureDefinition
            {
                Name = "cottage",
                Kind = FeatureKind.Structure,
                BasePlane = BasePlaneRule.LowestGround,
                Footprint = Footprint,
                MaxSlope = 3,
                Precedence = 100,
                ParameterOffset = 0, ParameterCount = 0,
                AnchorOffset = 0, AnchorCount = 2,
                SlotOffset = 0, SlotCount = 0,
                ProgramOffset = 0, ProgramLength = program.Length,
                MaterialOffset = 0, MaterialCount = 3,
                MaxPrimitives = 64,
            };

            for (var i = 0; i < placements.Count; i++)
                catalogue.ExplicitPlacements[i] = placements[i];

            catalogue.Rules[0] = new PlacementRule
            {
                DefinitionId = CottageId,
                CellEdge = FeatureBudget.PlacementCellEdgeVoxels,
                AttemptsPerCell = 0,          // rule-based scattering is US2
                AcceptProbability = 0,
                MinAltitude = 0,
                MaxAltitude = 512,
                MaxSlope = 3,
                MinSpacing = 128,
                ClusterMin = 0,
                ClusterMax = 0,
                ExclusionMask = 0,
                ExplicitOffset = 0,
                ExplicitCount = placements.Count,
            };

            CatalogueLoader.Finalise(ref catalogue);
            return catalogue;
        }

        /// <summary>
        /// Four cottages in front of the spawn point, one per orientation, so rotation can be
        /// checked by walking around them rather than by reading a test result.
        /// </summary>
        private static List<ExplicitPlacement> PlacementSites(uint seed)
        {
            var sites = new List<ExplicitPlacement>();

            // Spawn looks down +z from roughly voxel z = 166 at the centre of region (0,0,0).
            int3[] origins =
            {
                new(150, 0, 230),
                new(280, 0, 230),
                new(150, 0, 360),
                new(280, 0, 360),
            };

            for (var i = 0; i < origins.Length; i++)
            {
                var origin = origins[i];

                // Lowest ground under the footprint, so the foundation has something to bite
                // into on sloping terrain. This is what BasePlaneRule.LowestGround will do
                // automatically once US3 lands.
                int lowest = int.MaxValue;
                for (var z = 0; z <= Footprint.z; z += 16)
                for (var x = 0; x <= Footprint.x; x += 16)
                {
                    int h = TerrainSampler.HeightAt(origin.x + x, origin.z + z, seed);
                    if (h < lowest) lowest = h;
                }

                sites.Add(new ExplicitPlacement
                {
                    // Sunk four voxels so the foundation meets the ground rather than perching.
                    Position = new int3(origin.x, lowest - 4, origin.z),
                    Orientation = (byte)i,
                    OverrideOffset = 0,
                    OverrideCount = 0,
                });
            }

            return sites;
        }

        /// <summary>
        /// The cottage, as opcodes: foundation, a solid block of wall with the interior carved
        /// out of it, a doorway, windows, and a gable roof.
        ///
        /// Carving the interior out of a solid block rather than placing four walls is not just
        /// shorter — four walls invite a one-voxel gap at a corner, and a gap in a wall is the
        /// kind of defect that survives every test and is found by a player.
        /// </summary>
        private static int[] CottageProgram()
        {
            const int width = 64;
            const int depth = 64;
            const int wall = 32;
            const int thickness = 4;
            const int roof = 16;

            var code = new List<int>();

            void Op(ShapeOp op, params int[] operands)
            {
                code.Add((int)op);
                code.Add(0);              // no register operands: dimensions are literal here
                code.AddRange(operands);
            }

            void Box(int x, int y, int z, int sx, int sy, int sz, byte material, PrimitiveMode mode)
                => Op(ShapeOp.EmitBox, x, y, z, sx, sy, sz, material, (int)mode);

            // Foundation.
            Box(0, 0, 0, width, 8, depth, Stone, PrimitiveMode.Fill);

            // Walls, then the room carved out of them.
            Box(0, 8, 0, width, wall, depth, Stone, PrimitiveMode.Fill);
            Box(thickness, 8, thickness, width - 2 * thickness, wall, depth - 2 * thickness,
                0, PrimitiveMode.Carve);

            // Doorway through the south wall.
            Box(width / 2 - 6, 8, 0, 12, 20, thickness, 0, PrimitiveMode.Carve);

            // Windows: carved, then glazed, so the glass sits exactly in the hole.
            Box(10, 22, 0, 10, 10, thickness, 0, PrimitiveMode.Carve);
            Box(10, 22, 0, 10, 10, thickness, Glass, PrimitiveMode.Fill);
            Box(width - 20, 22, 0, 10, 10, thickness, 0, PrimitiveMode.Carve);
            Box(width - 20, 22, 0, 10, 10, thickness, Glass, PrimitiveMode.Fill);

            // Gable roof.
            Op(ShapeOp.EmitPrism, 0, 8 + wall, 0, width, roof, depth,
               (int)PrismProfile.Gable, Wood, (int)PrimitiveMode.Fill);

            Op(ShapeOp.SetAnchor, 0, width / 2, 8, 0, (int)Facing.South);
            Op(ShapeOp.SetAnchor, 1, width / 2, 8, depth / 2, (int)Facing.Up);

            Op(ShapeOp.End);

            return code.ToArray();
        }
    }
}
