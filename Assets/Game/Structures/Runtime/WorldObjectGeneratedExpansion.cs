using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// High-density secondary content for exercising the full world-object vocabulary in generated worlds.
    /// These placements are intentionally generic and can later be replaced by style-specific scene resolvers
    /// without changing the runtime behavior contracts.
    /// </summary>
    public static class WorldObjectGeneratedExpansion
    {
        public static void AuthorCastle(IStructureAuthoringSession geometry, WorldObjectAuthoringSession objects,
            in CastlePlan plan)
        {
            int y = plan.Centre.y + plan.PlateauHeight;
            int x = plan.Centre.x + plan.KeepHalfX + 18;
            int z = plan.Centre.z;

            // Training/utility annex: traversal and mechanism vocabulary not otherwise guaranteed by the castle profile.
            objects.Place(0x3100u, WorldObjectKind.Gate, B(x, y + 2, z - 52, 22, 24, 4), new int3(0, 0, 1));
            objects.Place(0x3101u, WorldObjectKind.MovingPlatform, B(x + 28, y + 4, z - 45, 18, 3, 18), new int3(1, 0, 0));
            objects.Place(0x3102u, WorldObjectKind.Zipline, B(x + 5, y + 28, z - 20, 44, 3, 3), new int3(1, 0, 0));
            objects.Place(0x3103u, WorldObjectKind.Teleporter, B(x + 52, y + 2, z - 28, 12, 3, 12), new int3(0, 1, 0));

            objects.Place(0x3120u, WorldObjectKind.PullChain, B(x + 3, y + 14, z + 2, 3, 16, 3), new int3(0, -1, 0));
            objects.Place(0x3121u, WorldObjectKind.Valve, B(x + 15, y + 8, z + 2, 8, 8, 4), new int3(0, 0, -1));
            objects.Place(0x3122u, WorldObjectKind.Generator, B(x + 30, y + 2, z + 2, 16, 14, 12), new int3(0, 0, -1));
            objects.Place(0x3123u, WorldObjectKind.FuseBox, B(x + 50, y + 8, z + 2, 8, 12, 4), new int3(0, 0, -1));
            objects.Connect(0x3122u, WorldObjectSignal.Powered, 0x3103u, WorldObjectAction.PowerOn);
            objects.Connect(0x3122u, WorldObjectSignal.Unpowered, 0x3103u, WorldObjectAction.PowerOff);

            // Usable/storage furniture cluster.
            objects.Place(0x3140u, WorldObjectKind.Cabinet, B(x, y + 2, z + 25, 12, 20, 7), new int3(0, 0, -1));
            objects.Place(0x3141u, WorldObjectKind.Chair, B(x + 16, y + 2, z + 25, 8, 12, 8), new int3(0, 0, -1));
            objects.Place(0x3142u, WorldObjectKind.Bench, B(x + 28, y + 2, z + 25, 18, 10, 8), new int3(0, 0, -1));
            objects.Place(0x3143u, WorldObjectKind.Altar, B(x + 50, y + 2, z + 25, 18, 14, 10), new int3(0, 0, -1));

            // Fire/light variants.
            objects.Place(0x3160u, WorldObjectKind.Torch, B(x, y + 12, z + 48, 4, 9, 4), new int3(0, 0, -1));
            objects.Place(0x3161u, WorldObjectKind.Brazier, B(x + 14, y + 2, z + 48, 10, 12, 10), new int3(0, 1, 0));

            // Secret/destruction variants plus timed generic trap.
            objects.Place(0x3180u, WorldObjectKind.Trap, B(x + 32, y + 1, z + 48, 12, 3, 12), new int3(0, 1, 0), parameter0: 20);
            objects.Place(0x3181u, WorldObjectKind.RotatingWall, B(x + 50, y + 2, z + 45, 5, 22, 18), new int3(1, 0, 0));
            WorldObjectMechanismPresets.AddTimedResettingTrap(objects, 0x3190u,
                B(x + 70, y + 8, z + 48, 5, 6, 3), new int3(0, 0, -1),
                B(x + 78, y + 2, z + 45, 14, 18, 14), new int3(0, 1, 0), WorldObjectKind.Crusher, 24);

            // Utility/navigation anchors.
            objects.Place(0x31A0u, WorldObjectKind.Cart, B(x, y + 2, z + 72, 18, 12, 25), new int3(0, 0, 1));
            objects.Place(0x31A1u, WorldObjectKind.Checkpoint, B(x + 28, y + 2, z + 72, 10, 16, 10), new int3(0, 0, 1));
            objects.Place(0x31A2u, WorldObjectKind.SpawnPoint, B(x + 48, y + 1, z + 72, 8, 2, 8), new int3(0, 1, 0));

            WorldObjectGeneratedContent.EmitAll(geometry, objects.BuildObjects());
        }

        public static void AuthorMineCave(IStructureAuthoringSession geometry, WorldObjectAuthoringSession objects,
            DecorationBounds chamber)
        {
            int3 p = chamber.Min;
            int3 s = chamber.Size;
            int y = p.y + 1;
            int x = p.x + math.max(4, s.x / 3);
            int z = p.z + math.max(4, s.z / 2);

            objects.Place(0x3200u, WorldObjectKind.MovingPlatform,
                B(x, y + 3, z, 16, 3, 16), new int3(1, 0, 0));
            objects.Place(0x3201u, WorldObjectKind.Zipline,
                B(p.x + 4, y + math.max(16, s.y - 8), p.z + 4, math.max(20, s.x - 8), 3, 3), new int3(1, 0, 0));
            objects.Place(0x3202u, WorldObjectKind.Cabinet,
                B(p.x + 38, y, p.z + 14, 11, 18, 7), new int3(0, 0, 1));
            objects.Place(0x3203u, WorldObjectKind.FuseBox,
                B(p.x + 52, y + 8, p.z + 14, 7, 10, 4), new int3(0, 0, 1));
            objects.Place(0x3204u, WorldObjectKind.Valve,
                B(p.x + 62, y + 7, p.z + 14, 8, 8, 4), new int3(0, 0, 1));
            objects.Place(0x3205u, WorldObjectKind.Checkpoint,
                B(p.x + 8, y, p.z + s.z - 16, 10, 16, 10), new int3(0, 0, -1));
            objects.Place(0x3206u, WorldObjectKind.SpawnPoint,
                B(p.x + 22, y, p.z + s.z - 14, 8, 2, 8), new int3(0, 1, 0));

            WorldObjectGeneratedContent.EmitAll(geometry, objects.BuildObjects());
        }

        private static DecorationBounds B(int x, int y, int z, int sx, int sy, int sz) => new DecorationBounds
        {
            Min = new int3(x, y, z),
            MaxExclusive = new int3(x + math.max(1, sx), y + math.max(1, sy), z + math.max(1, sz)),
        };
    }
}
