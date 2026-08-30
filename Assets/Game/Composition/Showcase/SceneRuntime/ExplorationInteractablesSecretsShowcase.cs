using Game.Structures.Api;
using Game.Structures.Runtime;
using Unity.Mathematics;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Composition policy for the compact interaction cluster beside the Primary-scene hub. The generic WorldObject
    /// runtime owns behavior/persistence; this class owns only semantic local keys, layout, and source-to-target wiring.
    /// </summary>
    public static class ExplorationInteractablesSecretsShowcase
    {
        public const uint ParentId = 0x45585343u; // EXSC

        public const uint ProximitySensorKey = 1;
        public const uint SlidingDoorKey = 2;
        public const uint PressurePlateKey = 3;
        public const uint PressureDoorKey = 4;
        public const uint BridgeLeverKey = 5;
        public const uint BridgeKey = 6;
        public const uint SecretWallKey = 7;
        public const uint RubbleLeftKey = 8;
        public const uint RubbleRightKey = 9;
        public const uint SecretMarkerKey = 10;

        // Bounded fixed cluster beside the origin hub; no terrain scan or runtime allocation loop is required.
        public static readonly int3 Origin = new int3(28, 3, -10);

        public static void Author(WorldObjectAuthoringSession authoring, int3 origin)
        {
            // An invisible pressure-source descriptor is deliberately reused as a semantic proximity volume: it has
            // exactly the authoritative Enter/Exit activation contract needed here without creating a parallel trigger API.
            authoring.Place(ProximitySensorKey, WorldObjectKind.PressurePlate,
                B(origin + new int3(0, 0, 0), new int3(8, 5, 8)), new int3(0, 0, 1),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(SlidingDoorKey, WorldObjectKind.Gate,
                B(origin + new int3(0, 0, 8), new int3(8, 12, 2)), new int3(0, 0, 1), parameter0: 14);
            authoring.Connect(ProximitySensorKey, WorldObjectSignal.Activated, SlidingDoorKey, WorldObjectAction.Open);
            authoring.Connect(ProximitySensorKey, WorldObjectSignal.Deactivated, SlidingDoorKey, WorldObjectAction.Close);

            int3 pressure = origin + new int3(16, 0, 0);
            authoring.Place(PressurePlateKey, WorldObjectKind.PressurePlate,
                B(pressure, new int3(7, 1, 7)), new int3(0, 0, 1));
            authoring.Place(PressureDoorKey, WorldObjectKind.Door,
                B(pressure + new int3(0, 0, 8), new int3(7, 12, 2)), new int3(0, 0, 1));
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Activated, PressureDoorKey, WorldObjectAction.Open);
            authoring.Connect(PressurePlateKey, WorldObjectSignal.Deactivated, PressureDoorKey, WorldObjectAction.Close);

            int3 bridge = origin + new int3(32, 0, 0);
            authoring.Place(BridgeLeverKey, WorldObjectKind.Lever,
                B(bridge, new int3(3, 6, 3)), new int3(0, 0, 1));
            authoring.Place(BridgeKey, WorldObjectKind.Drawbridge,
                B(bridge + new int3(6, 0, 0), new int3(14, 2, 7)), new int3(1, 0, 0),
                defaultState: WorldObjectStateFlags.Open);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Activated, BridgeKey, WorldObjectAction.Close);
            authoring.Connect(BridgeLeverKey, WorldObjectSignal.Deactivated, BridgeKey, WorldObjectAction.Open);

            int3 secret = origin + new int3(0, 0, 24);
            authoring.Place(SecretWallKey, WorldObjectKind.BreakableWall,
                B(secret, new int3(10, 12, 2)), new int3(0, 0, 1), defaultState: WorldObjectStateFlags.None);
            authoring.Place(RubbleLeftKey, WorldObjectKind.Crate,
                B(secret + new int3(1, 0, 4), new int3(3, 2, 3)), new int3(0, 0, 1),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(RubbleRightKey, WorldObjectKind.Crate,
                B(secret + new int3(6, 0, 5), new int3(3, 2, 3)), new int3(0, 0, 1),
                defaultState: WorldObjectStateFlags.Hidden);
            authoring.Place(SecretMarkerKey, WorldObjectKind.Torch,
                B(secret + new int3(4, 3, 8), new int3(2, 5, 2)), new int3(0, 0, 1),
                defaultState: WorldObjectStateFlags.Hidden | WorldObjectStateFlags.Active);
            authoring.Connect(SecretWallKey, WorldObjectSignal.Destroyed, RubbleLeftKey, WorldObjectAction.Reveal);
            authoring.Connect(SecretWallKey, WorldObjectSignal.Destroyed, RubbleRightKey, WorldObjectAction.Reveal);
            authoring.Connect(SecretWallKey, WorldObjectSignal.Destroyed, SecretMarkerKey, WorldObjectAction.Reveal);
        }

        public static WorldObjectId Id(uint worldSeed, uint localKey) => WorldObjectIds.Create(worldSeed, ParentId, localKey);

        private static DecorationBounds B(int3 min, int3 size) => new DecorationBounds
        {
            Min = min,
            MaxExclusive = min + size,
        };
    }
}