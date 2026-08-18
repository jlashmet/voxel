using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public struct WorldObjectPreset
    {
        public WorldObjectKind Kind;
        public WorldObjectCapabilities Capabilities;
        public WorldObjectStateFlags DefaultState;
        public int Parameter0;
        public int Parameter1;
        public int Parameter2;
        public int Parameter3;
    }

    /// <summary>Gameplay-first defaults for generated interactables. Geometry/presentation stays with structure and decoration authoring.</summary>
    public static class WorldObjectContentCatalog
    {
        private const WorldObjectCapabilities Stateful = WorldObjectCapabilities.Interactable |
            WorldObjectCapabilities.Stateful | WorldObjectCapabilities.Persistent;
        private const WorldObjectCapabilities SignalSource = Stateful | WorldObjectCapabilities.SignalSource;
        private const WorldObjectCapabilities SignalTarget = Stateful | WorldObjectCapabilities.SignalTarget;

        public static WorldObjectPreset Get(WorldObjectKind kind)
        {
            switch (kind)
            {
                case WorldObjectKind.Door:
                    return P(kind, SignalTarget | WorldObjectCapabilities.BlocksNavigation | WorldObjectCapabilities.Lockable |
                        WorldObjectCapabilities.Destructible);
                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                    return P(kind, SignalTarget | WorldObjectCapabilities.BlocksNavigation | WorldObjectCapabilities.Lockable |
                        WorldObjectCapabilities.Destructible, p0: 24); // travel voxels
                case WorldObjectKind.Drawbridge:
                    return P(kind, SignalTarget | WorldObjectCapabilities.BlocksNavigation | WorldObjectCapabilities.Rideable |
                        WorldObjectCapabilities.Destructible, p0: 90); // degrees
                case WorldObjectKind.Elevator:
                    return P(kind, SignalTarget | WorldObjectCapabilities.Movable | WorldObjectCapabilities.Rideable |
                        WorldObjectCapabilities.PowerConsumer, WorldObjectStateFlags.Powered, p0: 2); // default stop count
                case WorldObjectKind.MovingPlatform:
                    return P(kind, SignalTarget | WorldObjectCapabilities.Movable | WorldObjectCapabilities.Rideable);

                case WorldObjectKind.Lever:
                case WorldObjectKind.Switch:
                case WorldObjectKind.Button:
                case WorldObjectKind.PullChain:
                    return P(kind, SignalSource);
                case WorldObjectKind.PressurePlate:
                    return P(kind, SignalSource | WorldObjectCapabilities.Hazard);
                case WorldObjectKind.Winch:
                case WorldObjectKind.Valve:
                    return P(kind, SignalSource | WorldObjectCapabilities.Usable);

                case WorldObjectKind.Chest:
                case WorldObjectKind.Dresser:
                case WorldObjectKind.Cabinet:
                    return P(kind, Stateful | WorldObjectCapabilities.Container | WorldObjectCapabilities.Lootable |
                        WorldObjectCapabilities.Lockable | WorldObjectCapabilities.Destructible);
                case WorldObjectKind.Crate:
                case WorldObjectKind.Barrel:
                    return P(kind, Stateful | WorldObjectCapabilities.Container | WorldObjectCapabilities.Lootable |
                        WorldObjectCapabilities.Destructible | WorldObjectCapabilities.Movable);
                case WorldObjectKind.WeaponRack:
                case WorldObjectKind.Bookshelf:
                    return P(kind, Stateful | WorldObjectCapabilities.Lootable | WorldObjectCapabilities.Destructible);

                case WorldObjectKind.Bed:
                    return P(kind, Stateful | WorldObjectCapabilities.Usable | WorldObjectCapabilities.Destructible);
                case WorldObjectKind.Chair:
                case WorldObjectKind.Bench:
                    return P(kind, Stateful | WorldObjectCapabilities.Usable | WorldObjectCapabilities.Movable |
                        WorldObjectCapabilities.Destructible);
                case WorldObjectKind.Altar:
                    return P(kind, SignalSource | WorldObjectCapabilities.Usable | WorldObjectCapabilities.Destructible);
                case WorldObjectKind.Bell:
                    return P(kind, SignalSource | WorldObjectCapabilities.Usable);

                case WorldObjectKind.Torch:
                case WorldObjectKind.Lantern:
                    return P(kind, SignalTarget | WorldObjectCapabilities.EmitsLight | WorldObjectCapabilities.EmitsParticles |
                        WorldObjectCapabilities.Destructible, WorldObjectStateFlags.Active);
                case WorldObjectKind.Brazier:
                case WorldObjectKind.Fireplace:
                    return P(kind, SignalTarget | WorldObjectCapabilities.EmitsLight | WorldObjectCapabilities.EmitsParticles |
                        WorldObjectCapabilities.Destructible, WorldObjectStateFlags.Active);

                case WorldObjectKind.Trap:
                case WorldObjectKind.SpikeTrap:
                case WorldObjectKind.DartTrap:
                case WorldObjectKind.FallingBlockTrap:
                case WorldObjectKind.Crusher:
                    return P(kind, SignalTarget | WorldObjectCapabilities.Hazard | WorldObjectCapabilities.Hidden,
                        WorldObjectStateFlags.Hidden);

                case WorldObjectKind.SecretDoor:
                case WorldObjectKind.RotatingWall:
                    return P(kind, SignalTarget | WorldObjectCapabilities.BlocksNavigation | WorldObjectCapabilities.Hidden,
                        WorldObjectStateFlags.Hidden);
                case WorldObjectKind.BreakableWall:
                    return P(kind, Stateful | WorldObjectCapabilities.BlocksNavigation | WorldObjectCapabilities.Destructible |
                        WorldObjectCapabilities.Hidden, WorldObjectStateFlags.Hidden);

                case WorldObjectKind.Generator:
                    return P(kind, SignalSource | WorldObjectCapabilities.PowerSource, WorldObjectStateFlags.Active | WorldObjectStateFlags.Powered);
                case WorldObjectKind.FuseBox:
                    return P(kind, SignalSource | WorldObjectCapabilities.PowerSource | WorldObjectCapabilities.Destructible,
                        WorldObjectStateFlags.Powered);

                case WorldObjectKind.MineCart:
                case WorldObjectKind.Cart:
                    return P(kind, Stateful | WorldObjectCapabilities.Movable | WorldObjectCapabilities.Rideable |
                        WorldObjectCapabilities.Container);
                case WorldObjectKind.Ladder:
                case WorldObjectKind.Rope:
                case WorldObjectKind.Zipline:
                    return P(kind, WorldObjectCapabilities.Usable | WorldObjectCapabilities.Climbable);
                case WorldObjectKind.Teleporter:
                    return P(kind, SignalTarget | WorldObjectCapabilities.Usable | WorldObjectCapabilities.PowerConsumer,
                        WorldObjectStateFlags.Powered);
                case WorldObjectKind.Checkpoint:
                    return P(kind, Stateful | WorldObjectCapabilities.Usable);
                case WorldObjectKind.SpawnPoint:
                    return P(kind, WorldObjectCapabilities.None);
                default:
                    return default;
            }
        }

        private static WorldObjectPreset P(WorldObjectKind kind, WorldObjectCapabilities capabilities,
            WorldObjectStateFlags state = WorldObjectStateFlags.None, int p0 = 0, int p1 = 0, int p2 = 0, int p3 = 0)
        {
            return new WorldObjectPreset
            {
                Kind = kind,
                Capabilities = capabilities,
                DefaultState = state,
                Parameter0 = p0,
                Parameter1 = p1,
                Parameter2 = p2,
                Parameter3 = p3,
            };
        }
    }
}
