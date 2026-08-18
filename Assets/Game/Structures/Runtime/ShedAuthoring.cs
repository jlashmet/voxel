using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedOpeningAuthoring = VoxelEngine.Structures.Runtime.StructureOpeningAuthoring;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;

namespace Game.Structures.Runtime
{
    /// <summary>Deterministic shed composition over shared architectural component configs.</summary>
    public static class ShedAuthoring
    {
        public static void Author(IStructureAuthoringSession authoring, int3 origin, in ShedConfig config)
        {
            if (authoring == null) throw new System.ArgumentNullException(nameof(authoring));
            if (!config.IsWellFormed)
                throw new System.ArgumentException("Shed configuration is invalid.", nameof(config));

            int minX = origin.x + config.Footprint.Primary.Min.x;
            int minZ = origin.z + config.Footprint.Primary.Min.y;
            int baseY = origin.y;
            var shellMin = new int3(minX, baseY, minZ);

            AuthorFoundation(authoring, shellMin, in config);
            authoring.HollowBox(
                shellMin,
                new int3(config.Width, config.Height, config.Depth),
                config.WallThickness,
                config.Palette.Resolve(config.Walls.PrimaryMaterial),
                false,
                false);

            SharedOpeningAuthoring.AuthorRepeated(
                authoring, shellMin, config.Width, config.Height, config.Depth,
                config.WallThickness, in config.Door, config.DoorCount,
                config.DoorFacade, config.DoorGroupOffset, config.DoorSpacing, in config.Palette);

            if (config.WindowsEnabled)
                SharedOpeningAuthoring.AuthorRepeated(
                    authoring, shellMin, config.Width, config.Height, config.Depth,
                    config.WallThickness, in config.Window, config.WindowCount,
                    config.WindowFacade, config.WindowGroupOffset, config.WindowSpacing, in config.Palette);

            SharedRoofAuthoring.Author(
                authoring, shellMin, config.Width, config.Depth, baseY + config.Height,
                in config.Roof, config.Palette.Resolve(config.Roof.MaterialRole));
        }

        private static void AuthorFoundation(
            IStructureAuthoringSession authoring,
            int3 shellMin,
            in ShedConfig config)
        {
            if (config.Footprint.FoundationStyle == StructureFoundationStyle.None) return;
            if (config.Footprint.FoundationStyle != StructureFoundationStyle.Slab)
                throw new System.ArgumentException(
                    "Shed authoring currently supports None or Slab foundations only.", nameof(config));

            authoring.Box(
                new int3(shellMin.x, shellMin.y - config.Footprint.FoundationDepth, shellMin.z),
                new int3(config.Width, config.Footprint.FoundationDepth, config.Depth),
                config.Palette.Resolve(config.Footprint.FoundationMaterial));
        }
    }
}
