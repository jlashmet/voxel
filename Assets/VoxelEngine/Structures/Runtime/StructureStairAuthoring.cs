using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Structures.Runtime
{
    /// <summary>Shared integer straight-stair emission along a cardinal direction.</summary>
    public static class StructureStairAuthoring
    {
        public static void Author(
            IStructureAuthoringSession authoring,
            int3 bottomCentre,
            Facing ascentDirection,
            in StairConfig config,
            in StructureMaterialPalette palette)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed) throw new System.ArgumentException("Stair configuration is invalid.", nameof(config));
            if (!StructureCardinalTransform.IsCardinal(ascentDirection))
                throw new System.ArgumentOutOfRangeException(nameof(ascentDirection));
            if (config.Layout != StructureStairLayout.Straight)
                throw new System.NotSupportedException("StructureStairAuthoring currently emits straight stair layouts only.");

            byte material = palette.Resolve(config.MaterialRole);
            int2 dir = ascentDirection == Facing.North ? new int2(0, 1)
                : ascentDirection == Facing.East ? new int2(1, 0)
                : ascentDirection == Facing.South ? new int2(0, -1)
                : new int2(-1, 0);
            int2 side = new int2(-dir.y, dir.x);

            for (int step = 0; step < config.StepCount; step++)
            {
                int distance = step * config.StepRun;
                int height = (step + 1) * config.StepRise;
                int3 min = new int3(
                    bottomCentre.x + dir.x * distance - side.x * config.Width / 2,
                    bottomCentre.y,
                    bottomCentre.z + dir.y * distance - side.y * config.Width / 2);
                int3 size = dir.x == 0
                    ? new int3(config.Width, height, config.StepRun)
                    : new int3(config.StepRun, height, config.Width);
                if (dir.x < 0) min.x -= config.StepRun - 1;
                if (dir.y < 0) min.z -= config.StepRun - 1;
                authoring.Box(min, size, material);
            }

            if (config.Landing.Enabled)
            {
                int landingWidth = config.Landing.Width > 0 ? config.Landing.Width : config.Width;
                int distance = config.StepCount * config.StepRun;
                int3 min = new int3(
                    bottomCentre.x + dir.x * distance - side.x * landingWidth / 2,
                    bottomCentre.y,
                    bottomCentre.z + dir.y * distance - side.y * landingWidth / 2);
                int3 size = dir.x == 0
                    ? new int3(landingWidth, config.TotalRise, config.Landing.Length)
                    : new int3(config.Landing.Length, config.TotalRise, landingWidth);
                if (dir.x < 0) min.x -= config.Landing.Length - 1;
                if (dir.y < 0) min.z -= config.Landing.Length - 1;
                authoring.Box(min, size, palette.Resolve(config.Landing.MaterialRole));
            }
        }
    }
}
