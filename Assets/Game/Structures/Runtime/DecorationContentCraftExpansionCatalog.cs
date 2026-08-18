using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Expansion recipes for stable content IDs 43-84. Keeping later packs outside the bootstrap
    /// catalog makes continued growth toward hundreds of archetypes reviewable and merge-friendly.
    /// </summary>
    public static class DecorationContentCraftExpansionCatalog
    {
        public static DecorationContentRecipe Recipe(DecorationContentKind kind)
        {
            switch (kind)
            {
                // Carpentry / wheelwright / general workshop: 43-60.
                case DecorationContentKind.CarpenterBench:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(26, 10, 14), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.SawHorse:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Bench, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking() | DecorationInteractionFlags.Movable,
                        new int3(18, 9, 7), new int3(3, 0, 3), 2, 1);
                case DecorationContentKind.LumberStack:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                        new int3(30, 10, 12), new int3(2, 0, 2), 4, 2);
                case DecorationContentKind.PlankRack:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(32, 22, 8), new int3(3, 0, 4), 4, 1);
                case DecorationContentKind.ToolChest:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Coffin,
                        DecorationPropFamily.Chest, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, ContainerBlocking() | DecorationInteractionFlags.Movable,
                        new int3(18, 10, 11), new int3(2, 0, 2), 2, 1);
                case DecorationContentKind.ChiselBoard:
                    return WallTools(DecorationContentCategory.Carpentry, kind, 18, 13);
                case DecorationContentKind.PlaneRack:
                    return WallShelf(DecorationContentCategory.Carpentry, kind, 20, 12);
                case DecorationContentKind.ClampRack:
                    return WallTools(DecorationContentCategory.Carpentry, kind, 22, 14);
                case DecorationContentKind.WoodScrapBasket:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(10, 9, 10), new int3(1, 0, 1), 2, 2);
                case DecorationContentKind.Lathe:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.WheelMachine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(28, 13, 11), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.WheelwrightJig:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Machine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 18, 16), new int3(4, 0, 4), 2, 2);
                case DecorationContentKind.WheelStack:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(16, 16, 10), new int3(2, 0, 2), 2, 1);
                case DecorationContentKind.RepairTrestle:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Bench, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 11, 9), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.MeasuringBoard:
                    return WallShelf(DecorationContentCategory.Carpentry, kind, 26, 9);
                case DecorationContentKind.GluePotStation:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(14, 9, 12), new int3(3, 0, 3), 2, 1);
                case DecorationContentKind.MalletShelf:
                    return WallTools(DecorationContentCategory.Carpentry, kind, 18, 11);
                case DecorationContentKind.DowelBin:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(9, 11, 9), new int3(1, 0, 1), 1, 1);
                case DecorationContentKind.ShavingPile:
                    return R(DecorationContentCategory.Carpentry, kind, DecorationContentShape.Stack,
                        DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.ProceduralMesh, DecorationInteractionFlags.None,
                        new int3(14, 4, 12), int3.zero, 3, 2);

                // Textile / leather / pottery crafts: 61-84.
                case DecorationContentKind.Loom:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Machine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(26, 24, 12), new int3(5, 0, 5), 3, 1);
                case DecorationContentKind.SpinningWheel:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.WheelMachine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        Blocking() | DecorationInteractionFlags.Movable,
                        new int3(14, 16, 10), new int3(3, 0, 3), 2, 1);
                case DecorationContentKind.YarnBasket:
                    return PortableStack(kind, 10, 9, 10);
                case DecorationContentKind.SpindleRack:
                    return WallTools(DecorationContentCategory.Craft, kind, 18, 12);
                case DecorationContentKind.DyeVat:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(14, 11, 14), new int3(3, 0, 3), 2, 2);
                case DecorationContentKind.DryingLine:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Hanging,
                        DecorationPropFamily.Banner, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                        DecorationRenderBackend.ProceduralMesh, DecorationInteractionFlags.Destructible,
                        new int3(28, 12, 2), new int3(2, 2, 1), 4, 0);
                case DecorationContentKind.FoldedClothStack:
                    return PortableStack(kind, 14, 8, 10);
                case DecorationContentKind.BoltRack:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 20, 8), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.CuttingTable:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(28, 10, 16), new int3(4, 0, 4), 3, 2);
                case DecorationContentKind.DressForm:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Pedestal,
                        DecorationPropFamily.Chair, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(8, 20, 8), new int3(2, 0, 2), 1, 1);
                case DecorationContentKind.LeatherStretchingFrame:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 22, 6), new int3(3, 0, 3), 3, 0);
                case DecorationContentKind.HideRack:
                    return WallTools(DecorationContentCategory.Craft, kind, 24, 18);
                case DecorationContentKind.TanningTub:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(18, 9, 18), new int3(4, 0, 4), 2, 2);
                case DecorationContentKind.BootmakerBench:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.WorkSurface,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(20, 9, 12), new int3(3, 0, 3), 2, 1);
                case DecorationContentKind.PotteryWheel:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.WheelMachine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(12, 10, 12), new int3(3, 0, 3), 1, 1);
                case DecorationContentKind.Kiln:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Hearth,
                        DecorationPropFamily.Fireplace, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.VoxelStamp,
                        Blocking() | DecorationInteractionFlags.EmitsLight | DecorationInteractionFlags.EmitsParticles,
                        new int3(20, 20, 14), new int3(5, 0, 7), 2, 1);
                case DecorationContentKind.ClayBin:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Tub,
                        DecorationPropFamily.Barrel, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Container,
                        new int3(12, 9, 12), new int3(2, 0, 2), 2, 2);
                case DecorationContentKind.DryingShelf:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(24, 20, 8), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.AmphoraRack:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Rack,
                        DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(26, 22, 9), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.GlazeJarRack:
                    return WallShelf(DecorationContentCategory.Craft, kind, 20, 13);
                case DecorationContentKind.BasketWeavingFrame:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Machine,
                        DecorationPropFamily.Table, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly, Blocking(), new int3(18, 14, 14), new int3(3, 0, 3), 2, 2);
                case DecorationContentKind.WickerStack:
                    return PortableStack(kind, 14, 11, 12);
                case DecorationContentKind.SewingStool:
                    return R(DecorationContentCategory.Craft, kind, DecorationContentShape.Pedestal,
                        DecorationPropFamily.Chair, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                        DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(7, 7, 7), new int3(1, 0, 1), 1, 1);
                case DecorationContentKind.LeatherToolBoard:
                    return WallTools(DecorationContentCategory.Craft, kind, 20, 14);
                default:
                    return default;
            }
        }

        private static DecorationContentRecipe PortableStack(
            DecorationContentKind kind, int width, int height, int depth) =>
            R(DecorationContentCategory.Craft, kind, DecorationContentShape.Stack,
                DecorationPropFamily.Crate, DecorationSocketKind.Floor, DecorationMountMode.Floor,
                DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                new int3(width, height, depth), new int3(1, 0, 1), 2, 2);

        private static DecorationContentRecipe WallTools(
            DecorationContentCategory category, DecorationContentKind kind, int width, int height) =>
            R(category, kind, DecorationContentShape.WallRack,
                DecorationPropFamily.WeaponRack, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                new int3(width, height, 2), new int3(2, 2, 1), 2, 0);

        private static DecorationContentRecipe WallShelf(
            DecorationContentCategory category, DecorationContentKind kind, int width, int height) =>
            R(category, kind, DecorationContentShape.WallRack,
                DecorationPropFamily.Shelf, DecorationSocketKind.Wall, DecorationMountMode.Wall,
                DecorationRenderBackend.BoxAssembly, DecorationInteractionFlags.Destructible,
                new int3(width, height, 3), new int3(2, 2, 1), 2, 0);

        private static DecorationInteractionFlags Blocking() =>
            DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible;

        private static DecorationInteractionFlags ContainerBlocking() =>
            Blocking() | DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable;

        private static DecorationContentRecipe R(
            DecorationContentCategory category,
            DecorationContentKind kind,
            DecorationContentShape shape,
            DecorationPropFamily proxyFamily,
            DecorationSocketKind sockets,
            DecorationMountMode mount,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            int3 clearance,
            byte widthJitter,
            byte depthJitter) => new DecorationContentRecipe
        {
            Category = category,
            Kind = kind,
            Shape = shape,
            ProxyFamily = proxyFamily,
            AcceptedSockets = sockets,
            MountMode = mount,
            Backend = backend,
            Interaction = interaction,
            BaseSize = size,
            Clearance = clearance,
            WidthJitterSteps = widthJitter,
            DepthJitterSteps = depthJitter,
        };
    }
}
