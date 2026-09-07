using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Read-only production query over the world-object vocabulary. It enumerates only kinds that
    /// have a registered <see cref="WorldObjectContentCatalog"/> preset and supplies a neutral
    /// baseline authoring size for tools/validation that need one concrete instance.
    /// </summary>
    public static class WorldObjectCatalogQuery
    {
        public static WorldObjectKind[] Kinds()
        {
            Array values = Enum.GetValues(typeof(WorldObjectKind));
            var result = new List<WorldObjectKind>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                var kind = (WorldObjectKind)values.GetValue(i);
                if (kind == WorldObjectKind.Unknown)
                    continue;
                if (WorldObjectContentCatalog.Get(kind).Kind != WorldObjectKind.Unknown)
                    result.Add(kind);
            }
            return result.ToArray();
        }

        public static int3 BaselineSize(WorldObjectKind kind)
        {
            if (WorldObjectContentCatalog.Get(kind).Kind == WorldObjectKind.Unknown)
                return int3.zero;

            switch (kind)
            {
                case WorldObjectKind.Door:
                case WorldObjectKind.SecretDoor:
                case WorldObjectKind.BreakableWall:
                case WorldObjectKind.RotatingWall:
                    return new int3(12, 24, 4);
                case WorldObjectKind.Trapdoor:
                    // A closed hatch lies in the floor; its thickness is the vertical extent.
                    // Keep the same dimensions as the door baseline, but put its long axis on Z.
                    return new int3(12, 4, 24);
                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                    return new int3(24, 28, 5);
                case WorldObjectKind.Drawbridge:
                case WorldObjectKind.MovingPlatform:
                    return new int3(28, 4, 20);
                case WorldObjectKind.Elevator:
                    return new int3(18, 28, 18);
                case WorldObjectKind.Lever:
                case WorldObjectKind.Switch:
                case WorldObjectKind.Button:
                case WorldObjectKind.PullChain:
                case WorldObjectKind.Winch:
                case WorldObjectKind.Valve:
                case WorldObjectKind.FuseBox:
                    return new int3(5, 8, 4);
                case WorldObjectKind.PressurePlate:
                    return new int3(12, 2, 12);
                case WorldObjectKind.Chest:
                case WorldObjectKind.Dresser:
                case WorldObjectKind.Cabinet:
                case WorldObjectKind.Crate:
                case WorldObjectKind.Barrel:
                    return new int3(12, 10, 10);
                case WorldObjectKind.Bed:
                    return new int3(18, 8, 30);
                case WorldObjectKind.Chair:
                    return new int3(8, 12, 8);
                case WorldObjectKind.Bench:
                    return new int3(18, 10, 8);
                case WorldObjectKind.Torch:
                case WorldObjectKind.Lantern:
                    return new int3(4, 9, 4);
                case WorldObjectKind.Brazier:
                case WorldObjectKind.Fireplace:
                    return new int3(16, 16, 10);
                case WorldObjectKind.Trap:
                case WorldObjectKind.SpikeTrap:
                case WorldObjectKind.DartTrap:
                    return new int3(16, 8, 16);
                case WorldObjectKind.FallingBlockTrap:
                case WorldObjectKind.Crusher:
                    return new int3(18, 24, 18);
                case WorldObjectKind.WeaponRack:
                case WorldObjectKind.Bookshelf:
                    return new int3(18, 20, 6);
                case WorldObjectKind.Altar:
                    return new int3(16, 12, 10);
                case WorldObjectKind.Bell:
                    return new int3(10, 14, 10);
                case WorldObjectKind.Generator:
                    return new int3(18, 14, 14);
                case WorldObjectKind.MineCart:
                case WorldObjectKind.Cart:
                    return new int3(16, 10, 22);
                case WorldObjectKind.Ladder:
                    return new int3(7, 24, 3);
                case WorldObjectKind.Rope:
                    return new int3(3, 24, 3);
                case WorldObjectKind.Zipline:
                    return new int3(30, 4, 4);
                case WorldObjectKind.Teleporter:
                    return new int3(16, 4, 16);
                case WorldObjectKind.Checkpoint:
                case WorldObjectKind.SpawnPoint:
                    return new int3(10, 4, 10);
                default:
                    return new int3(10, 10, 10);
            }
        }
    }
}
