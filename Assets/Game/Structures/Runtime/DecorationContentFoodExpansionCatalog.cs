using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>Stable content IDs 85-114: kitchens, bakeries, breweries, wineries, and pantries.</summary>
    public static class DecorationContentFoodExpansionCatalog
    {
        public static DecorationContentRecipe Recipe(DecorationContentKind kind)
        {
            switch (kind)
            {
                case DecorationContentKind.PrepTable:
                    return WorkSurface(kind, 26, 10, 15);
                case DecorationContentKind.ButcherBlock:
                    return WorkSurface(kind, 20, 12, 14);
                case DecorationContentKind.HangingPotRack:
                    return HangingRack(kind, 24, 10);
                case DecorationContentKind.PanRack:
                    return WallRack(kind, 20, 14);
                case DecorationContentKind.CauldronStand:
                    return R(kind, DecorationContentShape.Pedestal, DecorationPropFamily.Table,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(14, 11, 14), new int3(3, 0, 3), 2, 2);
                case DecorationContentKind.BreadOven:
                    return R(kind, DecorationContentShape.Hearth, DecorationPropFamily.Fireplace,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.VoxelStamp,
                        Blocking() | DecorationInteractionFlags.EmitsLight | DecorationInteractionFlags.EmitsParticles,
                        new int3(24, 20, 16), new int3(5, 0, 7), 3, 1);
                case DecorationContentKind.RoastingSpit:
                    return R(kind, DecorationContentShape.Machine, DecorationPropFamily.Table,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(24, 12, 12), new int3(4, 0, 4), 3, 1);
                case DecorationContentKind.WashSink:
                    return R(kind, DecorationContentShape.Trough, DecorationPropFamily.Table,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(20, 11, 10), new int3(3, 0, 4), 2, 1);
                case DecorationContentKind.WaterBarrel:
                    return R(kind, DecorationContentShape.Tub, DecorationPropFamily.Barrel,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(11, 14, 11), new int3(2, 0, 2), 1, 1);
                case DecorationContentKind.FlourBin:
                    return Container(kind, 14, 12, 12);
                case DecorationContentKind.GrainSackStack:
                    return PortableStack(kind, 16, 10, 14);
                case DecorationContentKind.SpiceShelf:
                    return WallShelf(kind, 20, 13);
                case DecorationContentKind.HerbDryingRack:
                    return HangingRack(kind, 24, 14);
                case DecorationContentKind.MeatHookRail:
                    return R(kind, DecorationContentShape.Hanging, DecorationPropFamily.WeaponRack,
                        DecorationSocketKind.Wall, DecorationMountMode.Wall, DecorationRenderBackend.ProceduralMesh,
                        DecorationInteractionFlags.Destructible, new int3(24, 14, 3), new int3(2, 2, 1), 3, 0);
                case DecorationContentKind.CheeseShelf:
                    return WallShelf(kind, 24, 14);
                case DecorationContentKind.BreadCoolingRack:
                    return R(kind, DecorationContentShape.Rack, DecorationPropFamily.Shelf,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(24, 22, 8), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.PantryCabinet:
                    return R(kind, DecorationContentShape.Rack, DecorationPropFamily.Bookcase,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.BoxAssembly,
                        Blocking() | DecorationInteractionFlags.Container | DecorationInteractionFlags.Lootable,
                        new int3(22, 24, 8), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.VegetableBasket:
                    return PortableStack(kind, 11, 8, 11);
                case DecorationContentKind.FishCrate:
                    return R(kind, DecorationContentShape.Stack, DecorationPropFamily.Crate,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Container |
                        DecorationInteractionFlags.Lootable | DecorationInteractionFlags.Movable,
                        new int3(14, 8, 11), new int3(1, 0, 1), 2, 2);
                case DecorationContentKind.BreweryVat:
                    return R(kind, DecorationContentShape.Tub, DecorationPropFamily.Barrel,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(22, 24, 22), new int3(5, 0, 5), 3, 3);
                case DecorationContentKind.MashTun:
                    return R(kind, DecorationContentShape.Tub, DecorationPropFamily.Barrel,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(20, 20, 20), new int3(5, 0, 5), 3, 3);
                case DecorationContentKind.Fermenter:
                    return R(kind, DecorationContentShape.Tub, DecorationPropFamily.Barrel,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(18, 22, 18), new int3(4, 0, 4), 2, 2);
                case DecorationContentKind.WinePress:
                    return R(kind, DecorationContentShape.Machine, DecorationPropFamily.Table,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(24, 24, 18), new int3(5, 0, 5), 3, 2);
                case DecorationContentKind.BottleRack:
                    return R(kind, DecorationContentShape.Rack, DecorationPropFamily.Shelf,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(24, 22, 8), new int3(3, 0, 4), 3, 1);
                case DecorationContentKind.CaskStand:
                    return R(kind, DecorationContentShape.Rack, DecorationPropFamily.Shelf,
                        DecorationSocketKind.Wall, DecorationMountMode.FloorAgainstWall, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(28, 18, 11), new int3(4, 0, 4), 4, 2);
                case DecorationContentKind.PieRack:
                    return WallShelf(kind, 20, 14);
                case DecorationContentKind.SausageRack:
                    return HangingRack(kind, 22, 14);
                case DecorationContentKind.FoodPrepShelf:
                    return WallShelf(kind, 24, 12);
                case DecorationContentKind.KettleStand:
                    return R(kind, DecorationContentShape.Pedestal, DecorationPropFamily.Table,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                        new int3(10, 10, 10), new int3(2, 0, 2), 1, 1);
                case DecorationContentKind.CellarCaskStack:
                    return R(kind, DecorationContentShape.Stack, DecorationPropFamily.Barrel,
                        DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                        Blocking(), new int3(26, 18, 16), new int3(3, 0, 3), 4, 2);
                default:
                    return default;
            }
        }

        private static DecorationContentRecipe WorkSurface(DecorationContentKind kind, int x, int y, int z) =>
            R(kind, DecorationContentShape.WorkSurface, DecorationPropFamily.Table,
                DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                Blocking(), new int3(x, y, z), new int3(4, 0, 4), 3, 2);

        private static DecorationContentRecipe WallShelf(DecorationContentKind kind, int width, int height) =>
            R(kind, DecorationContentShape.WallRack, DecorationPropFamily.Shelf,
                DecorationSocketKind.Wall, DecorationMountMode.Wall, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible, new int3(width, height, 3), new int3(2, 2, 1), 2, 0);

        private static DecorationContentRecipe WallRack(DecorationContentKind kind, int width, int height) =>
            R(kind, DecorationContentShape.WallRack, DecorationPropFamily.WeaponRack,
                DecorationSocketKind.Wall, DecorationMountMode.Wall, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible, new int3(width, height, 3), new int3(2, 2, 1), 2, 0);

        private static DecorationContentRecipe HangingRack(DecorationContentKind kind, int width, int height) =>
            R(kind, DecorationContentShape.Hanging, DecorationPropFamily.Banner,
                DecorationSocketKind.Ceiling, DecorationMountMode.Ceiling, DecorationRenderBackend.ProceduralMesh,
                DecorationInteractionFlags.Destructible, new int3(width, height, 5), new int3(2, 2, 2), 3, 1);

        private static DecorationContentRecipe PortableStack(DecorationContentKind kind, int x, int y, int z) =>
            R(kind, DecorationContentShape.Stack, DecorationPropFamily.Crate,
                DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                new int3(x, y, z), new int3(1, 0, 1), 2, 2);

        private static DecorationContentRecipe Container(DecorationContentKind kind, int x, int y, int z) =>
            R(kind, DecorationContentShape.Counter, DecorationPropFamily.Chest,
                DecorationSocketKind.Floor, DecorationMountMode.Floor, DecorationRenderBackend.BoxAssembly,
                DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Container |
                DecorationInteractionFlags.Lootable | DecorationInteractionFlags.Movable,
                new int3(x, y, z), new int3(2, 0, 2), 2, 2);

        private static DecorationInteractionFlags Blocking() =>
            DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible;

        private static DecorationContentRecipe R(
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
            Category = DecorationContentCategory.FoodProduction,
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
