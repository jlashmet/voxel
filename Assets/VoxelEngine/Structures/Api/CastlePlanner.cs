using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace VoxelEngine.Structures.Api
{
    /// <summary>
    /// Produces deterministic castle plans from authoring inputs.
    ///
    /// Planning owns seeded choices about what the castle is. Runtime builders own only the
    /// realization of an already-decided plan into voxel storage. Keeping that boundary explicit
    /// lets layout grammar evolve without coupling it to write budgets, streaming, or brush code.
    /// </summary>
    public static class CastlePlanner
    {
        /// <summary>
        /// Creates the current castle family for <paramref name="seed"/>.
        ///
        /// This first planning boundary deliberately preserves CastleBuilder.Plan's historical
        /// random draw sequence exactly. Existing seeds therefore produce exactly the same plan
        /// while callers migrate to the planner. Structural/topological variation can be added
        /// here after the builder has been reduced to plan realization.
        /// </summary>
        public static CastlePlan Create(int3 centre, uint seed)
        {
            var rng = new Random(seed | 1u);

            // Roughly 44-56 m across the bailey. This is about twice the footprint area of the
            // former 30-40 m plan, but not twice every linear dimension: doing that would
            // quadruple the sculpted site and exhaust the brick pool before interiors existed.
            int baileyX = rng.NextInt(220, 280);
            int baileyZ = rng.NextInt(220, 280);

            // A circular crag must contain the rectangular bailey's corners, not merely its
            // longest half-axis.
            int plateauRadius = (int)math.ceil(math.sqrt(baileyX * baileyX + baileyZ * baileyZ))
                                + rng.NextInt(18, 32);
            int plateauHeight = rng.NextInt(26, 44);
            int cliffDrop = rng.NextInt(26, 44);
            int wallHeight = rng.NextInt(82, 108);
            int wallThickness = rng.NextInt(18, 25);
            int towerRadius = rng.NextInt(30, 39);
            int towerHeight = rng.NextInt(125, 160);
            int gateTowerRadius = rng.NextInt(28, 36);
            int gateTowerHeight = rng.NextInt(135, 172);
            int keepHalfX = rng.NextInt(92, 121);
            int keepHalfZ = rng.NextInt(78, 101);

            // Preserve the historical draw sequence. Keep height now comes from the floor stack,
            // but consuming this obsolete draw keeps every later seeded choice stable.
            rng.NextInt(190, 240);

            const int floorHeight = 46;
            int floors = rng.NextInt(5, 7);

            return new CastlePlan
            {
                Centre = centre,
                Seed = seed,

                PlateauRadius = plateauRadius,
                PlateauHeight = plateauHeight,
                CliffDrop = cliffDrop,

                BaileyHalfX = baileyX,
                BaileyHalfZ = baileyZ,

                WallHeight = wallHeight,
                WallThickness = wallThickness,

                TowerRadius = towerRadius,
                TowerHeight = towerHeight,

                GateTowerRadius = gateTowerRadius,
                GateTowerHeight = gateTowerHeight,

                KeepHalfX = keepHalfX,
                KeepHalfZ = keepHalfZ,
                KeepHeight = floors * floorHeight,

                FloorHeight = floorHeight,
                Floors = floors,
            };
        }
    }
}
