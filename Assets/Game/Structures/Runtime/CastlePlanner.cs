using Game.Structures.Api;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Game-owned deterministic castle-family policy. The engine does not know what a castle is;
    /// this planner chooses the dimensions and game-space layout that structure authoring will
    /// later realize through generic voxel-authoring capabilities.
    /// </summary>
    public static class CastlePlanner
    {
        public static CastlePlan Plan(int3 centre, uint seed)
        {
            var rng = new Random(seed | 1u);

            int baileyX = rng.NextInt(220, 280);
            int baileyZ = rng.NextInt(220, 280);

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

            // Preserve the historical random stream so unrelated dimensions do not change while
            // ownership moves from the engine layer into game content.
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

        /// <summary>
        /// Estimates expensive-write equivalents before authoring starts. This is content policy,
        /// not a storage implementation detail: it scales with the chosen castle dimensions and
        /// protects the game from accidentally authoring a pathological landmark.
        /// </summary>
        public static long EstimateWrites(in CastlePlan plan)
        {
            double plateauArea = math.PI_DBL * plan.PlateauRadius * plan.PlateauRadius;
            double siteCap = plateauArea * 3.0;

            double cliffArea = math.PI_DBL *
                ((plan.PlateauRadius + plan.CliffDrop) * (double)(plan.PlateauRadius + plan.CliffDrop)
                 - plan.PlateauRadius * (double)plan.PlateauRadius);
            double cliffCap = cliffArea * 4.0;

            double perimeter = 4.0 * (plan.BaileyHalfX + plan.BaileyHalfZ);
            double walls = perimeter * 240.0;
            double towers = 6.0 * math.PI_DBL * plan.TowerRadius * plan.TowerRadius * 30.0;
            double keep = plan.KeepHalfX * (double)plan.KeepHalfZ * plan.Floors * 4.0;
            double courtyard = plateauArea * 0.2;
            double underground = 1_500_000.0;

            return (long)(siteCap + cliffCap + walls + towers + keep + courtyard + underground);
        }
    }
}
