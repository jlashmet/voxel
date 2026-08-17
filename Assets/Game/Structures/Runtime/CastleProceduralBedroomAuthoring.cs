using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// First live castle migration onto the generic decoration system. The bed, rug, dresser,
    /// painting, and wall torch are procedural; architectural beams plus the existing fireplace,
    /// sitting cluster, wall hangings, and chandelier remain as legacy secondary detail until
    /// their own scene recipes are available.
    /// </summary>
    public static class CastleProceduralBedroomAuthoring
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

            AuthorCeilingBeams(authoring, in plan, min, size, y);

            if (!CastleBedroomDecorationAdapter.TryResolve(
                    in plan,
                    out _,
                    out _,
                    out _,
                    out DecorationPlacement[] placements))
            {
                throw new System.InvalidOperationException(
                    "Castle bedchamber decoration resolution failed; refusing to author a partial room.");
            }

            if (!DecorationStructureAuthoringEmitter.TryAuthor(authoring, placements))
            {
                throw new System.InvalidOperationException(
                    "Castle bedchamber decoration emission failed; refusing to author a partial room.");
            }

            int cx = min.x + size.x / 2;
            int cz = min.z + size.z / 2;
            const int inner = 8;

            AuthorLegacyFireplace(authoring, min, y, inner, cz);
            AuthorLegacyWallHangings(authoring, min, size, y, inner, cz);
            AuthorLegacySittingCluster(authoring, min, y, inner, cz);
            AuthorLegacyChandelier(authoring, y, cx, cz);
        }

        private static void AuthorCeilingBeams(
            IStructureAuthoringSession authoring,
            in CastlePlan plan,
            int3 min,
            int3 size,
            int y)
        {
            for (int beamZ = min.z + 22; beamZ < min.z + size.z - 18; beamZ += 36)
            {
                authoring.Box(
                    new int3(min.x + 9, y + plan.FloorHeight - 7, beamZ),
                    new int3(size.x - 18, 4, 4),
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
                new int3(min.x + inner, y + 3, cz + 25),
                new int3(9, 28, 36),
                GameMaterialIds.DarkStone);
            authoring.Arch(
                new int3(min.x + inner + 1, y + 5, cz + 33),
                20,
                17,
                8,
                0,
                GameMaterialIds.Empty);
            authoring.Box(
                new int3(min.x + inner + 4, y + 5, cz + 37),
                new int3(4, 7, 12),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(min.x + inner - 2, y + 29, cz + 22),
                new int3(13, 4, 42),
                GameMaterialIds.Wood);
        }

        private static void AuthorLegacyWallHangings(
            IStructureAuthoringSession authoring,
            int3 min,
            int3 size,
            int y,
            int inner,
            int cz)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int hangingZ = cz + side * 48;
                authoring.Box(
                    new int3(min.x + size.x - inner - 2, y + 15, hangingZ - 10),
                    new int3(2, 24, 20),
                    GameMaterialIds.Cloth);
                authoring.Box(
                    new int3(min.x + size.x - inner - 3, y + 37, hangingZ - 13),
                    new int3(3, 3, 26),
                    GameMaterialIds.Gold);
            }
        }

        private static void AuthorLegacySittingCluster(
            IStructureAuthoringSession authoring,
            int3 min,
            int y,
            int inner,
            int cz)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int chairZ = cz + 25 + side * 18;
                int chairX = min.x + inner + 31;
                authoring.Box(
                    new int3(chairX, y + 4, chairZ - 5),
                    new int3(10, 4, 10),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(chairX, y + 8, chairZ - 5),
                    new int3(4, 13, 10),
                    GameMaterialIds.Wood);
                authoring.Box(
                    new int3(chairX + 2, y + 8, chairZ - 4),
                    new int3(7, 3, 8),
                    GameMaterialIds.Cloth);
            }

            authoring.Cylinder(
                min.x + inner + 48,
                y + 3,
                cz + 25,
                7,
                7,
                GameMaterialIds.Wood);
            authoring.Disc(
                min.x + inner + 48,
                y + 10,
                cz + 25,
                9,
                GameMaterialIds.Gold);
        }

        private static void AuthorLegacyChandelier(
            IStructureAuthoringSession authoring,
            int y,
            int cx,
            int cz)
        {
            int bedLampX = cx - 18;
            authoring.Box(
                new int3(bedLampX - 1, y + 32, cz - 1),
                new int3(2, 10, 2),
                GameMaterialIds.Gold);
            authoring.Box(
                new int3(bedLampX - 12, y + 30, cz - 1),
                new int3(24, 2, 2),
                GameMaterialIds.Wood);
            authoring.Box(
                new int3(bedLampX - 1, y + 30, cz - 10),
                new int3(2, 2, 20),
                GameMaterialIds.Wood);

            int2[] bedroomCandles =
            {
                new(-10, 0), new(10, 0), new(-5, -8), new(5, -8),
                new(-5, 8), new(5, 8),
            };
            foreach (int2 candle in bedroomCandles)
            {
                authoring.Box(
                    new int3(bedLampX + candle.x - 2, y + 27, cz + candle.y - 2),
                    new int3(4, 6, 4),
                    GameMaterialIds.Glass);
            }
        }
    }
}
