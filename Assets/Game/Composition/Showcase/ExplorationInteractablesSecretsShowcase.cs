using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-specific composition for the interactables showcase. Shared WorldObject behavior owns all source
    /// interactions, signals, actions and mechanism transitions; this class owns only deterministic layout/wiring.
    /// </summary>
    public static class ExplorationInteractablesSecretsShowcase
    {
        public const uint ParentId = 0x45585343u; // EXSC

        // Standalone/non-secret vocabulary.
        public const uint NormalDoorKey = 1;
        public const uint LockedDoorKey = 2;
        public const uint PressurePlateKey = 3;
        public const uint PressureDoorKey = 4;
        public const uint PortcullisPlateKey = 5;
        public const uint PortcullisKey = 6;
        public const uint TrapdoorKey = 7;
        public const uint ElevatorKey = 8;
        public const uint BridgeLeverKey = 9;
        public const uint BridgeKey = 10;
        public const uint VisibleButtonKey = 11;
        public const uint ButtonGateKey = 12;

        // Required secret compositions.
        public const uint HiddenBookshelfButtonKey = 20;
        public const uint BookshelfPanelKey = 21;
        public const uint BookshelfSecretMarkerKey = 22;
        public const uint ElevatedSecretKey = 23;
        public const uint SecretRouteLeverKey = 24;
        public const uint SecretRouteGateKey = 25;
        public const uint SecretRouteMarkerKey = 26;

        public static readonly int3 Origin = new int3(0, 1, 0);
        public static readonly int3 Extents = new int3(72, 18, 56);

        public static void Author(WorldObjectAuthoringSession authoring, int3 origin)
        {
            // Row 1: direct/local mechanisms plus a reversible pressure-plate route.
            authoring.Place(NormalDoorKey, WorldObjectKind.Door,
                B(origin + new int3(2, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Place(LockedDoorKey, WorldObjectKind.Door,
                B(origin + new int3(14, 0, 8), new int3(7, 11, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Locked);
            authoring.Place(TrapdoorKey, WorldObjectKind.Trapdoor,
                B(origin + new int3(26, 0, 4), new int3(8, 2, 8)), Fwd());

            authoring.Place(PressurePlateKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(40, 0, 1), new int3(7, 1, 7)), Fwd());
            authoring.Place(PressureDoorKey, WorldObjectKind.Door,
                B(origin + new int3(40, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Activated, PressureDoorKey, WorldObjectAction.Open);
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Deactivated, PressureDoorKey, WorldObjectAction.Close);

            authoring.Place(PortcullisPlateKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(54, 0, 1), new int3(7, 1, 7)), Fwd());
            authoring.Place(PortcullisKey, WorldObjectKind.Portcullis,
                B(origin + new int3(54, 0, 8), new int3(7, 11, 2)), Fwd());
            authoring.Connect(PortcullisPlateKey, WorldObjectSignal.Activated, PortcullisKey, WorldObjectAction.Open);
            authoring.Connect(PortcullisPlateKey, WorldObjectSignal.Deactivated, PortcullisKey, WorldObjectAction.Close);

            // Row 2: moving mechanisms and a visible one-shot button using the same generic connection contract.
            authoring.Place(ElevatorKey, WorldObjectKind.Elevator,
                B(origin + new int3(2, 0, 20), new int3(8, 2, 8)), Fwd(),
                defaultState: WorldObjectStateFlags.Powered, parameter0: 2, parameter1: 10);

            int3 bridge = origin + new int3(18, 0, 20);
            authoring.Place(BridgeLeverKey, WorldObjectKind.Lever,
                B(bridge, new int3(3, 6, 3)), Fwd());
            authoring.Place(BridgeKey, WorldObjectKind.Drawbridge,
                B(bridge + new int3(5, 0, 0), new int3(14, 2, 7)), new int3(1, 0, 0),
                defaultState: WorldObjectStateFlags.Open);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Activated, BridgeKey, WorldObjectAction.Close);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Deactivated, BridgeKey, WorldObjectAction.Open);

            authoring.Place(VisibleButtonKey, WorldObjectKind.Button,
                B(origin + new int3(43, 2, 20), new int3(2, 3, 2)), Fwd());
            authoring.Place(ButtonGateKey, WorldObjectKind.Gate,
                B(origin + new int3(49, 0, 20), new int3(7, 11, 2)), Fwd());
            authoring.Connect(VisibleButtonKey, WorldObjectSignal.Activated, ButtonGateKey, WorldObjectAction.Open);

            // Secret 1: concealed/disguised button reveals and opens a moving false-wall panel.
            int3 bookshelf = origin + new int3(2, 0, 38);
            authoring.Place(HiddenBookshelfButtonKey, WorldObjectKind.Button,
                B(bookshelf + new int3(1, 4, 0), new int3(2, 2, 1)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(BookshelfPanelKey, WorldObjectKind.RotatingWall,
                B(bookshelf + new int3(5, 0, 0), new int3(10, 11, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(BookshelfSecretMarkerKey, WorldObjectKind.Torch,
                B(bookshelf + new int3(9, 3, 6), new int3(2, 5, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Hidden | WorldObjectStateFlags.Active);
            authoring.Connect(HiddenBookshelfButtonKey, WorldObjectSignal.Activated,
                BookshelfPanelKey, WorldObjectAction.Reveal);
            authoring.Connect(HiddenBookshelfButtonKey, WorldObjectSignal.Activated,
                BookshelfPanelKey, WorldObjectAction.Open);
            authoring.Connect(HiddenBookshelfButtonKey, WorldObjectSignal.Activated,
                BookshelfSecretMarkerKey, WorldObjectAction.Reveal);

            // Secret 2: the lift is the only authored mechanism reaching this elevated reward volume.
            authoring.Place(ElevatedSecretKey, WorldObjectKind.Chest,
                B(origin + new int3(4, 11, 22), new int3(5, 4, 4)), Fwd());

            // Secret 3: a lever remotely opens/closes a separate route gate; marker sits beyond the barrier.
            int3 remote = origin + new int3(36, 0, 38);
            authoring.Place(SecretRouteLeverKey, WorldObjectKind.Lever,
                B(remote, new int3(3, 6, 3)), Fwd());
            authoring.Place(SecretRouteGateKey, WorldObjectKind.Gate,
                B(remote + new int3(12, 0, 0), new int3(8, 11, 2)), Fwd());
            authoring.Place(SecretRouteMarkerKey, WorldObjectKind.Torch,
                B(remote + new int3(24, 3, 1), new int3(2, 5, 2)), Fwd(),
                defaultState: WorldObjectStateFlags.Active);
            authoring.Connect(SecretRouteLeverKey, WorldObjectSignal.Activated,
                SecretRouteGateKey, WorldObjectAction.Open);
            authoring.Connect(SecretRouteLeverKey, WorldObjectSignal.Deactivated,
                SecretRouteGateKey, WorldObjectAction.Close);
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
