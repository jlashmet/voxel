using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using Random = Unity.Mathematics.Random;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Great-hall migration onto the generic dining scene. The central table/benches/chairs are
    /// procedural; the existing fireplace, throne, chandelier, hangings, table settings, lamps,
    /// beams, and seeded wall-side details remain until their own scene families are migrated.
    /// </summary>
    public static class CastleProceduralGreatHallAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int y)
        {
            if (authoring == null)
                throw new System.ArgumentNullException(nameof(authoring));

            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            const int inner = 8;

            AuthorCeilingBeams(authoring, in plan, min, size, y);

            if (!CastleDiningDecorationAdapter.TryResolve(
                    in plan,
                    out _,
                    out DecorationContext context,
                    out _,
                    out DecorationPlacement[] placements))
            {
                throw new System.InvalidOperationException(
                    "Castle great-hall dining resolution failed; refusing to author partial dining furniture.");
            }

            if (!DiningDecorationAuthoringEmitter.TryAuthor(authoring, placements, in context))
            {
                throw new System.InvalidOperationException(
                    "Castle great-hall dining emission failed; refusing to author partial dining furniture.");
            }

            AuthorLegacyFireplace(authoring, min, y, inner, cz);
            AuthorLegacyThrone(authoring, y, cx, cz);
            AuthorLegacyChandelier(authoring, y, cx, cz);
            AuthorLegacyWallHangings(authoring, min, size, y, inner, cx);
            AuthorLegacyPlaceSettings(authoring, y, cx, cz);
            AuthorLegacyWallLamps(authoring, min, y, inner, cz);
            AuthorLegacySeededSideDetails(authoring, in plan, min, size, y, inner);
        }

        private static void AuthorCeilingBeams(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int y)
        {
            for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 34)
            {
                authoring.Box(
                    new int3(min.x + 9, y + plan.FloorHeight - 8, beamZ),
                    new int3(size.x - 18, 5, 5),
                    GameMaterialIds.Wood);
            }
        }

        private static void AuthorLegacyFireplace(
            IStructureAuthoringSession authoring,
            int3 min,
            int y,
            int inner,
            int cz)
        {
            authoring.Box(
                new int3(min.x + inner, y + 1, cz - 24),
                new int3(10, 40, 48),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(min.x + inner + 2, y + 3, cz - 14),
                new int3(6, 16, 28),
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(min.x + inner + 5, y + 3, cz - 10),
                new int3(4, 6, 20),
                GameMaterialIds.Gold);
        }

        private static void AuthorLegacyThrone(
            IStructureAuthoringSession authoring,
            int y,
            int cx,
            int cz)
        {
            authoring.Box(
                new int3(cx + 57, y + 1, cz - 18),
                new int3(14, 4, 36),
                GameMaterialIds.DarkStone);
            authoring.Box(
                new int3(cx + 60, y + 5, cz - 9),
                new int3(8, 11, 18),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(cx + 62, y + 10, cz - 7),
                new int3(5, 10, 14),
                GameMaterialIds.Cloth);
        }

        private static void AuthorLegacyChandelier(
            IStructureAuthoringSession authoring,
            int y,
            int cx,
            int cz)
        {
            authoring.Box(
                new int3(cx - 1, y + 33, cz - 1),
                new int3(2, 9, 2),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(cx - 13, y + 30, cz - 1),
                new int3(26, 3, 2),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(cx - 1, y + 30, cz - 13),
                new int3(2, 3, 26),
                GameMaterialIds.Wood);

            int2[] candleOffsets =
            {
                new(-12, 0), new(12, 0), new(0, -12), new(0, 12),
            };
            foreach (int2 candle in candleOffsets)
            {
                authoring.Box(
                    new int3(cx + candle.x - 2, y + 27, cz + candle.y - 2),
                    new int3(4, 6, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(cx + candle.x - 1, y + 26, cz + candle.y - 1),
                    new int3(2, 2, 2),
                    GameMaterialIds.Gold);
            }
        }

        private static void AuthorLegacyWallHangings(
            IStructureAuthoringSession authoring,
            int3 min,
            int3 size,
            int y,
            int inner,
            int cx)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int hangingX = cx + side * 48;
                authoring.Box(
                    new int3(hangingX - 10, y + 13, min.z + size.z - inner - 1),
                    new int3(20, 25, 2),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(hangingX - 13, y + 36, min.z + size.z - inner - 2),
                    new int3(26, 3, 3),
                    GameMaterialIds.Gold);
            }
        }

        private static void AuthorLegacyPlaceSettings(
            IStructureAuthoringSession authoring,
            int y,
            int cx,
            int cz)
        {
            for (int setting = -3; setting <= 3; setting++)
            {
                int settingX = cx + setting * 11;
                authoring.Disc(settingX, y + 11, cz, 3, GameMaterialIds.Gold);
                if ((setting & 1) == 0)
                {
                    authoring.Box(
                        new int3(settingX - 1, y + 12, cz - 1),
                        new int3(2, 5, 2),
                        GameMaterialIds.Glass);
                }
            }
        }

        private static void AuthorLegacyWallLamps(
            IStructureAuthoringSession authoring,
            int3 min,
            int y,
            int inner,
            int cz)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int lampZ = cz + side * 38;
                authoring.Box(
                    new int3(min.x + inner + 10, y + 16, lampZ - 2),
                    new int3(4, 8, 4),
                    GameMaterialIds.Glass);
                authoring.Box(
                    new int3(min.x + inner + 8, y + 14, lampZ - 1),
                    new int3(3, 3, 3),
                    GameMaterialIds.Gold);
            }
        }

        private static void AuthorLegacySeededSideDetails(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int y,
            int inner)
        {
            var rng = new Random(plan.Seed ^ 13u);
            for (int i = 0; i < rng.NextInt(2, 5); i++)
            {
                bool leftWall = rng.NextBool();
                int px = leftWall ? min.x + inner + 22 : min.x + size.x - inner - 30;
                int pz = rng.NextInt(min.z + inner + 8, min.z + size.z - inner - 12);
                int radius = rng.NextInt(4, 7);
                authoring.Cylinder(
                    px,
                    y + 3,
                    pz,
                    radius,
                    rng.NextInt(8, 14),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(px - radius, y + 7, pz - radius - 1),
                    new int3(radius * 2, 2, radius * 2 + 2),
                    GameMaterialIds.Gold);
            }
        }
    }
}
