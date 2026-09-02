using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>Deterministic shed composition over shared architectural component configs.</summary>
    public static class ShedAuthoring
    {
        public static void Author(
            IStructureComponentAuthoring components,
            IStructureAuthoringSession authoring,
            int3 origin,
            in ShedConfig config)
        {
            if (components == null) throw new System.ArgumentNullException(nameof(components));
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

            var door = new StructureOpeningAuthoringRequest
            {
                ShellMin = shellMin,
                Width = config.Width,
                Height = config.Height,
                Depth = config.Depth,
                WallThickness = config.WallThickness,
                Opening = config.Door,
                Count = config.DoorCount,
                Facade = config.DoorFacade,
                GroupOffset = config.DoorGroupOffset,
                Spacing = config.DoorSpacing,
                Palette = config.Palette,
            };
            components.AuthorOpenings(authoring, in door);

            if (config.WindowsEnabled)
            {
                var window = new StructureOpeningAuthoringRequest
                {
                    ShellMin = shellMin,
                    Width = config.Width,
                    Height = config.Height,
                    Depth = config.Depth,
                    WallThickness = config.WallThickness,
                    Opening = config.Window,
                    Count = config.WindowCount,
                    Facade = config.WindowFacade,
                    GroupOffset = config.WindowGroupOffset,
                    Spacing = config.WindowSpacing,
                    Palette = config.Palette,
                };
                components.AuthorOpenings(authoring, in window);
            }

            var roof = new StructureRoofAuthoringRequest
            {
                FootprintMin = shellMin,
                Width = config.Width,
                Depth = config.Depth,
                BaseY = baseY + config.Height,
                Roof = config.Roof,
                Material = config.Palette.Resolve(config.Roof.MaterialRole),
            };
            components.AuthorRoof(authoring, in roof);
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
