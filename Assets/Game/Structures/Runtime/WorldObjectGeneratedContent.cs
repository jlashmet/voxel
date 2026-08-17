using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum WorldObjectGeometryEmissionMode : byte
    {
        AllVoxel = 0,
        StaticOnly = 1,
        None = 2,
    }

    /// <summary>
    /// Dense, deterministic interactable content profiles for generated structures. These profiles deliberately
    /// favor gameplay content volume over bespoke art polish; style-specific decoration can replace or embellish
    /// the baseline geometry without changing stable world-object identity.
    /// </summary>
    public static class WorldObjectGeneratedContent
    {
        public static void AuthorCastle(IStructureAuthoringSession geometry, WorldObjectAuthoringSession objects,
            in CastlePlan plan, WorldObjectGeometryEmissionMode emissionMode = WorldObjectGeometryEmissionMode.AllVoxel)
        {
            if (geometry == null) throw new System.ArgumentNullException(nameof(geometry));
            if (objects == null) throw new System.ArgumentNullException(nameof(objects));

            int baseY = plan.Centre.y + plan.PlateauHeight;
            int keepMinX = plan.Centre.x - plan.KeepHalfX;
            int keepMinZ = plan.Centre.z - plan.KeepHalfZ;
            int gateZ = plan.Centre.z - plan.BaileyHalfZ;

            WorldObjectMechanismPresets.AddGatehouse(objects, 0x1000u,
                B(plan.Centre.x - 18, baseY + 8, gateZ + 10, 5, 9, 4), new int3(0, 0, 1),
                B(plan.Centre.x - 14, baseY + 2, gateZ - 2, 28, 32, 4), new int3(0, 0, 1),
                B(plan.Centre.x - 18, baseY, gateZ - 34, 36, 4, 34),
                B(plan.Centre.x + 12, baseY + 7, gateZ + 10, 9, 10, 8));

            WorldObjectMechanismPresets.AddLeverDoor(objects, 0x1100u,
                B(keepMinX + 8, baseY + 7, keepMinZ - 2, 4, 8, 3), new int3(0, 0, -1),
                B(plan.Centre.x - 8, baseY + 2, keepMinZ - 1, 16, 28, 3), new int3(0, 0, -1));
            WorldObjectMechanismPresets.AddLockControl(objects, 0x1120u,
                B(keepMinX + 10, baseY + 8, plan.Centre.z, 4, 7, 3), new int3(1, 0, 0),
                B(plan.Centre.x - 1, baseY + 2, plan.Centre.z - 10, 3, 26, 20), new int3(1, 0, 0));

            int secretY = baseY + math.max(1, plan.FloorHeight);
            WorldObjectMechanismPresets.AddSecretRoom(objects, 0x1200u,
                B(keepMinX + 4, secretY + 9, plan.Centre.z + 12, 3, 5, 3), new int3(1, 0, 0),
                B(keepMinX + 1, secretY + 2, plan.Centre.z + 4, 4, 24, 18), new int3(1, 0, 0));
            objects.Place(0x1210u, WorldObjectKind.Chest,
                B(keepMinX + 12, secretY + 2, plan.Centre.z + 8, 14, 10, 9), new int3(0, 0, -1),
                defaultState: WorldObjectStateFlags.Locked);
            objects.Place(0x1211u, WorldObjectKind.WeaponRack,
                B(keepMinX + 7, secretY + 2, plan.Centre.z + 22, 20, 18, 4), new int3(0, 0, -1));

            DecorationBounds elevator = B(plan.Centre.x + plan.KeepHalfX - 20, baseY + 2,
                plan.Centre.z + plan.KeepHalfZ - 20, 14, math.max(30, plan.FloorHeight * math.max(2, plan.Floors)), 14);
            DecorationBounds[] calls = new DecorationBounds[math.max(2, math.min(4, plan.Floors))];
            for (int i = 0; i < calls.Length; i++)
                calls[i] = B(elevator.Min.x - 5, baseY + 7 + i * plan.FloorHeight, elevator.Min.z, 4, 6, 3);
            WorldObjectMechanismPresets.AddElevatorCallNetwork(objects, 0x1300u, elevator, new int3(-1, 0, 0), calls);

            objects.Place(0x1400u, WorldObjectKind.Bell,
                B(plan.Centre.x - 5, baseY + 18, plan.Centre.z - 5, 10, 12, 10), new int3(0, 1, 0));
            objects.Place(0x1401u, WorldObjectKind.Bed,
                B(keepMinX + 18, secretY + 2, keepMinZ + 12, 18, 9, 30), new int3(0, 0, 1));
            objects.Place(0x1402u, WorldObjectKind.Dresser,
                B(keepMinX + 42, secretY + 2, keepMinZ + 8, 14, 20, 8), new int3(0, 0, 1));
            objects.Place(0x1403u, WorldObjectKind.Bookshelf,
                B(keepMinX + 8, secretY + 2, keepMinZ + 34, 24, 22, 6), new int3(0, 0, 1));
            objects.Place(0x1404u, WorldObjectKind.Fireplace,
                B(plan.Centre.x - 15, baseY + 2, keepMinZ + 4, 30, 24, 8), new int3(0, 0, 1));

            int dungeonY = baseY - 24;
            WorldObjectMechanismPresets.AddPressurePlateTrap(objects, 0x1500u,
                B(plan.Centre.x - 8, dungeonY, plan.Centre.z - 28, 16, 2, 14), new int3(0, 1, 0),
                B(plan.Centre.x - 8, dungeonY, plan.Centre.z - 28, 16, 16, 14), new int3(0, 1, 0),
                WorldObjectKind.SpikeTrap);
            WorldObjectMechanismPresets.AddPressurePlateTrap(objects, 0x1510u,
                B(plan.Centre.x + 22, dungeonY, plan.Centre.z - 8, 12, 2, 12), new int3(0, 1, 0),
                B(plan.Centre.x + 34, dungeonY + 4, plan.Centre.z - 8, 5, 12, 12), new int3(-1, 0, 0),
                WorldObjectKind.DartTrap);
            objects.Place(0x1520u, WorldObjectKind.FallingBlockTrap,
                B(plan.Centre.x - 18, dungeonY + 12, plan.Centre.z + 24, 18, 16, 18), new int3(0, -1, 0));
            objects.Place(0x1521u, WorldObjectKind.Crusher,
                B(plan.Centre.x + 10, dungeonY, plan.Centre.z + 28, 16, 28, 14), new int3(0, 1, 0));
            objects.Place(0x1522u, WorldObjectKind.BreakableWall,
                B(plan.Centre.x - 34, dungeonY, plan.Centre.z + 8, 5, 24, 20), new int3(1, 0, 0));

            var lights = new DecorationBounds[6];
            for (int i = 0; i < lights.Length; i++)
            {
                int x = keepMinX + 12 + (i % 2) * math.max(12, plan.KeepHalfX * 2 - 28);
                int z = keepMinZ + 14 + (i / 2) * 24;
                lights[i] = B(x, baseY + 14, z, 4, 9, 4);
            }
            WorldObjectMechanismPresets.AddPoweredLights(objects, 0x1600u,
                B(keepMinX + 7, baseY + 8, keepMinZ + 7, 4, 6, 3), new int3(0, 0, 1),
                lights, new int3(0, 0, 1));

            EmitAll(geometry, objects.BuildObjects(), null, emissionMode);
        }

        public static void AuthorMineCave(IStructureAuthoringSession geometry, WorldObjectAuthoringSession objects,
            DecorationBounds chamber, WorldObjectGeometryEmissionMode emissionMode = WorldObjectGeometryEmissionMode.AllVoxel)
        {
            if (geometry == null) throw new System.ArgumentNullException(nameof(geometry));
            if (objects == null) throw new System.ArgumentNullException(nameof(objects));
            int3 p = chamber.Min;
            int3 s = chamber.Size;
            int y = p.y + 1;

            objects.Place(0x2100u, WorldObjectKind.MineCart,
                B(p.x + s.x / 4, y, p.z + s.z / 2, 16, 10, 22), new int3(0, 0, 1));
            objects.Place(0x2101u, WorldObjectKind.MineCart,
                B(p.x + s.x / 2, y, p.z + s.z / 2, 16, 10, 22), new int3(0, 0, 1));
            objects.Place(0x2102u, WorldObjectKind.Ladder,
                B(p.x + 4, y, p.z + 4, 7, math.max(18, s.y - 4), 2), new int3(0, 0, 1));
            objects.Place(0x2103u, WorldObjectKind.Rope,
                B(p.x + s.x - 8, y, p.z + 8, 3, math.max(16, s.y - 6), 3), new int3(0, 1, 0));
            objects.Place(0x2104u, WorldObjectKind.Crate,
                B(p.x + 14, y, p.z + 16, 10, 10, 10), new int3(0, 0, 1));
            objects.Place(0x2105u, WorldObjectKind.Barrel,
                B(p.x + 27, y, p.z + 17, 9, 13, 9), new int3(0, 0, 1));

            WorldObjectMechanismPresets.AddPressurePlateTrap(objects, 0x2200u,
                B(p.x + s.x / 2 - 7, y, p.z + s.z / 3, 14, 2, 12), new int3(0, 1, 0),
                B(p.x + s.x / 2 - 7, y, p.z + s.z / 3, 14, 15, 12), new int3(0, 1, 0),
                WorldObjectKind.SpikeTrap);
            WorldObjectMechanismPresets.AddLeverDoor(objects, 0x2210u,
                B(p.x + s.x - 14, y + 8, p.z + s.z / 2, 4, 8, 3), new int3(-1, 0, 0),
                B(p.x + s.x - 6, y, p.z + s.z / 2 - 10, 5, 24, 20), new int3(-1, 0, 0));

            var lamps = new[]
            {
                B(p.x + 10, y + 16, p.z + 10, 4, 8, 4),
                B(p.x + s.x / 2, y + 16, p.z + 10, 4, 8, 4),
                B(p.x + s.x - 14, y + 16, p.z + 10, 4, 8, 4),
            };
            WorldObjectMechanismPresets.AddPoweredLights(objects, 0x2300u,
                B(p.x + 5, y + 7, p.z + 5, 4, 6, 3), new int3(0, 0, 1), lamps, new int3(0, 0, 1),
                WorldObjectKind.Lantern);

            EmitAll(geometry, objects.BuildObjects(), null, emissionMode);
        }

        public static void EmitAll(IStructureAuthoringSession geometry, WorldObjectDescriptor[] descriptors,
            WorldObjectStateStore stateStore = null,
            WorldObjectGeometryEmissionMode emissionMode = WorldObjectGeometryEmissionMode.AllVoxel)
        {
            if (geometry == null || descriptors == null || emissionMode == WorldObjectGeometryEmissionMode.None) return;
            for (int i = 0; i < descriptors.Length; i++)
            {
                if (emissionMode == WorldObjectGeometryEmissionMode.StaticOnly &&
                    WorldObjectPresentationPlanner.RequiresDynamicProxy(descriptors[i].Kind))
                    continue;
                WorldObjectResolvedState state = WorldObjectStateResolver.Resolve(in descriptors[i], stateStore);
                WorldObjectGeometryEmitter.Emit(geometry, in state);
            }
        }

        private static DecorationBounds B(int x, int y, int z, int sx, int sy, int sz) => new DecorationBounds
        {
            Min = new int3(x, y, z),
            MaxExclusive = new int3(x + math.max(1, sx), y + math.max(1, sy), z + math.max(1, sz)),
        };
    }
}
