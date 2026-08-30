using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Composition policy for the dedicated interaction showcase. The generic WorldObject runtime owns behavior,
    /// timers, signals and persistence; this class owns only stable local keys, compact bay layout and wiring.
    /// </summary>
    public static class ExplorationInteractablesSecretsShowcase
    {
        public const uint ParentId = 0x45585343u; // EXSC

        public const uint NormalDoorKey = 1;
        public const uint LockedDoorKey = 2;
        public const uint TimedPlateKey = 3;
        public const uint TimedGateKey = 4;
        public const uint PortcullisPlateKey = 5;
        public const uint PortcullisKey = 6;
        public const uint TrapdoorKey = 7;
        public const uint ElevatorKey = 8;
        public const uint BridgeLeverKey = 9;
        public const uint BridgeKey = 10;
        public const uint PressurePlateKey = 11;
        public const uint PressureDoorKey = 12;
        public const uint ProximitySensorKey = 13;
        public const uint SlidingDoorKey = 14;

        public const uint SecretWallKey = 20;
        public const uint SecretNookCrateKey = 21;
        public const uint SecretMarkerKey = 22;
        public const uint SecretLeverKey = 23;
        public const uint LeverRevealKey = 24;
        public const uint ConcealmentPlateKey = 25;
        public const uint ConcealedDoorKey = 26;

        public const int TimedGateTicks = 3;

        // Four readable rows of compact bays; all descriptors stay inside 72 x 16 x 54 voxels.
        public static readonly int3 Origin = new int3(0, 1, 0);

        public static void Author(WorldObjectAuthoringSession authoring, int3 origin)
        {
            // Row 1: direct vocabulary — normal door, locked door, timed gate, and portcullis.
            authoring.Place(NormalDoorKey, WorldObjectKind.Door,
                B(origin + new int3(2, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Place(LockedDoorKey, WorldObjectKind.Door,
                B(origin + new int3(16, 0, 8), new int3(7, 11, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Locked);

            authoring.Place(TimedPlateKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(30, 0, 1), new int3(7, 1, 7)), Fwd(), parameter0: TimedGateTicks);
            authoring.Place(TimedGateKey, WorldObjectKind.Gate,
                B(origin + new int3(30, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Connect(TimedPlateKey, WorldObjectSignal.Activated, TimedGateKey, WorldObjectAction.Open);
            authoring.Connect(TimedPlateKey, WorldObjectSignal.Deactivated, TimedGateKey, WorldObjectAction.Close);

            authoring.Place(PortcullisPlateKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(44, 0, 1), new int3(7, 1, 7)), Fwd());
            authoring.Place(PortcullisKey, WorldObjectKind.Portcullis,
                B(origin + new int3(44, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Connect(PortcullisPlateKey, WorldObjectSignal.Activated, PortcullisKey, WorldObjectAction.Open);
            authoring.Connect(PortcullisPlateKey, WorldObjectSignal.Deactivated, PortcullisKey, WorldObjectAction.Close);

            // Row 2: vertical/moving geometry plus a clearly readable pressure-door pair and proximity barrier.
            authoring.Place(TrapdoorKey, WorldObjectKind.Trapdoor,
                B(origin + new int3(2, 0, 18), new int3(8, 2, 8)), Fwd());
            authoring.Place(ElevatorKey, WorldObjectKind.Elevator,
                B(origin + new int3(16, 0, 18), new int3(8, 2, 8)), Fwd(),
                defaultState: WorldObjectStateFlags.Powered, parameter0: 2, parameter1: 8);

            int3 bridge = origin + new int3(30, 0, 18);
            authoring.Place(BridgeLeverKey, WorldObjectKind.Lever,
                B(bridge, new int3(3, 6, 3)), Fwd());
            authoring.Place(BridgeKey, WorldObjectKind.Drawbridge,
                B(bridge + new int3(5, 0, 0), new int3(14, 2, 7)), new int3(1, 0, 0),
                defaultState: WorldObjectStateFlags.Open);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Activated, BridgeKey, WorldObjectAction.Close);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Deactivated, BridgeKey, WorldObjectAction.Open);

            authoring.Place(PressurePlateKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(54, 0, 18), new int3(7, 1, 7)), Fwd());
            authoring.Place(PressureDoorKey, WorldObjectKind.Door,
                B(origin + new int3(54, 0, 26), new int3(7, 11, 2)), Fwd());
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Activated, PressureDoorKey, WorldObjectAction.Open);
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Deactivated, PressureDoorKey, WorldObjectAction.Close);

            // Invisible source reuses PressurePlate Enter/Exit semantics as a generic proximity volume.
            authoring.Place(ProximitySensorKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(64, 0, 18), new int3(7, 5, 8)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(SlidingDoorKey, WorldObjectKind.Gate,
                B(origin + new int3(64, 0, 26), new int3(7, 11, 2)), Fwd());
            authoring.Connect(ProximitySensorKey, WorldObjectSignal.Activated, SlidingDoorKey, WorldObjectAction.Open);
            authoring.Connect(ProximitySensorKey, WorldObjectSignal.Deactivated, SlidingDoorKey, WorldObjectAction.Close);

            // Row 3, secret 1: breakable wall reveals a lit loot nook.
            int3 wallSecret = origin + new int3(2, 0, 38);
            authoring.Place(SecretWallKey, WorldObjectKind.BreakableWall,
                B(wallSecret, new int3(10, 11, 2)), Fwd(), defaultState: WorldObjectStateFlags.None);
            authoring.Place(SecretNookCrateKey, WorldObjectKind.Crate,
                B(wallSecret + new int3(2, 0, 5), new int3(4, 4, 4)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(SecretMarkerKey, WorldObjectKind.Torch,
                B(wallSecret + new int3(7, 3, 6), new int3(2, 5, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden | WorldObjectStateFlags.Active);
            authoring.Connect(SecretWallKey, WorldObjectSignal.Destroyed, SecretNookCrateKey, WorldObjectAction.Reveal);
            authoring.Connect(SecretWallKey, WorldObjectSignal.Destroyed, SecretMarkerKey, WorldObjectAction.Reveal);

            // Secret 2: a lever reveals an otherwise hidden reward object.
            int3 leverSecret = origin + new int3(28, 0, 38);
            authoring.Place(SecretLeverKey, WorldObjectKind.Lever,
                B(leverSecret, new int3(3, 6, 3)), Fwd());
            authoring.Place(LeverRevealKey, WorldObjectKind.Chest,
                B(leverSecret + new int3(8, 0, 3), new int3(6, 5, 4)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Connect(SecretLeverKey, WorldObjectSignal.Activated, LeverRevealKey, WorldObjectAction.Reveal);
            authoring.Connect(SecretLeverKey, WorldObjectSignal.Deactivated, LeverRevealKey, WorldObjectAction.Hide);

            // Secret 3: satisfying a concealed pressure condition reveals a secret passage door.
            int3 concealed = origin + new int3(52, 0, 38);
            authoring.Place(ConcealmentPlateKey, WorldObjectKind.PressurePlate,
                B(concealed, new int3(7, 1, 7)), Fwd());
            authoring.Place(ConcealedDoorKey, WorldObjectKind.SecretDoor,
                B(concealed + new int3(0, 0, 8), new int3(9, 11, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Connect(ConcealmentPlateKey, WorldObjectSignal.Activated, ConcealedDoorKey, WorldObjectAction.Reveal);
            authoring.Connect(ConcealmentPlateKey, WorldObjectSignal.Deactivated, ConcealedDoorKey, WorldObjectAction.Hide);
        }

        public static WorldObjectId Id(uint worldSeed, uint localKey) => WorldObjectIds.Create(worldSeed, ParentId, localKey);

        private static int3 Fwd() => new int3(0, 0, 1);

        private static DecorationBounds B(int3 min, int3 size) => new DecorationBounds
        {
            Min = min,
            MaxExclusive = min + size,
        };
    }
}
