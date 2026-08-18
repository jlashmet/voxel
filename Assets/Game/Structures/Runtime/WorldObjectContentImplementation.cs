using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum WorldObjectInteraction : byte
    {
        Primary = 0,
        Secondary = 1,
        Enter = 2,
        Exit = 3,
        Attack = 4,
    }

    public struct WorldObjectInteractionResult
    {
        public WorldObjectStateDelta Delta;
        public WorldObjectSignal Signal;
        public bool Changed;
    }

    /// <summary>
    /// Concrete gameplay behavior for generated world objects. The catalog declares capabilities;
    /// this layer defines what a player/world interaction actually does for each registered kind.
    /// </summary>
    public static class WorldObjectBehavior
    {
        public static bool TryInteract(in WorldObjectResolvedState current, WorldObjectInteraction interaction,
            out WorldObjectInteractionResult result)
        {
            result = default;
            if (!current.Descriptor.IsWellFormed || current.IsDestroyed) return false;

            switch (current.Descriptor.Kind)
            {
                case WorldObjectKind.Door:
                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                case WorldObjectKind.Drawbridge:
                    if (interaction == WorldObjectInteraction.Attack)
                        return Destroy(in current, out result);
                    if (interaction == WorldObjectInteraction.Secondary)
                        return Apply(in current, current.IsLocked ? WorldObjectAction.Unlock : WorldObjectAction.Lock, 0, out result);
                    return Apply(in current, current.IsOpen ? WorldObjectAction.Close : WorldObjectAction.Open, 0, out result);

                case WorldObjectKind.Elevator:
                {
                    if (!current.IsPowered || interaction == WorldObjectInteraction.Attack) return false;
                    int stopCount = math.max(2, current.Descriptor.Parameter0);
                    int next = (current.RuntimeValue0 + 1) % stopCount;
                    return Apply(in current, WorldObjectAction.MoveToStop, next, out result);
                }
                case WorldObjectKind.MovingPlatform:
                    return Apply(in current,
                        (current.State & WorldObjectStateFlags.Active) != 0 ? WorldObjectAction.Deactivate : WorldObjectAction.Activate,
                        0, out result);

                case WorldObjectKind.Lever:
                case WorldObjectKind.Switch:
                case WorldObjectKind.PullChain:
                case WorldObjectKind.Winch:
                case WorldObjectKind.Valve:
                    return Apply(in current, WorldObjectAction.Toggle, 0, out result);
                case WorldObjectKind.Button:
                    return Apply(in current, interaction == WorldObjectInteraction.Exit ? WorldObjectAction.Deactivate : WorldObjectAction.Activate, 0, out result);
                case WorldObjectKind.PressurePlate:
                    if (interaction == WorldObjectInteraction.Enter)
                        return Apply(in current, WorldObjectAction.Trigger, 0, out result);
                    if (interaction == WorldObjectInteraction.Exit)
                        return Apply(in current, WorldObjectAction.Reset, 0, out result);
                    return false;
                case WorldObjectKind.Generator:
                    return Apply(in current, current.IsPowered ? WorldObjectAction.PowerOff : WorldObjectAction.PowerOn, 0, out result);
                case WorldObjectKind.FuseBox:
                    if (interaction == WorldObjectInteraction.Attack) return Destroy(in current, out result);
                    return Apply(in current, current.IsPowered ? WorldObjectAction.PowerOff : WorldObjectAction.PowerOn, 0, out result);

                case WorldObjectKind.Chest:
                case WorldObjectKind.Dresser:
                case WorldObjectKind.Cabinet:
                    if (interaction == WorldObjectInteraction.Attack) return Destroy(in current, out result);
                    if (interaction == WorldObjectInteraction.Secondary)
                        return Apply(in current, current.IsLocked ? WorldObjectAction.Unlock : WorldObjectAction.Lock, 0, out result);
                    if (!current.IsOpen)
                        return Apply(in current, WorldObjectAction.Open, 0, out result);
                    return Loot(in current, out result);

                case WorldObjectKind.Crate:
                case WorldObjectKind.Barrel:
                    if (interaction == WorldObjectInteraction.Attack) return Destroy(in current, out result);
                    return Loot(in current, out result);
                case WorldObjectKind.WeaponRack:
                case WorldObjectKind.Bookshelf:
                    if (interaction == WorldObjectInteraction.Attack) return Destroy(in current, out result);
                    return Loot(in current, out result);

                case WorldObjectKind.Bed:
                case WorldObjectKind.Chair:
                case WorldObjectKind.Bench:
                case WorldObjectKind.Altar:
                case WorldObjectKind.Bell:
                    if (interaction == WorldObjectInteraction.Attack &&
                        (current.Descriptor.Capabilities & WorldObjectCapabilities.Destructible) != 0)
                        return Destroy(in current, out result);
                    return Apply(in current, WorldObjectAction.Toggle, 0, out result);

                case WorldObjectKind.Torch:
                case WorldObjectKind.Lantern:
                case WorldObjectKind.Brazier:
                case WorldObjectKind.Fireplace:
                    if (interaction == WorldObjectInteraction.Attack) return Destroy(in current, out result);
                    return Apply(in current,
                        (current.State & WorldObjectStateFlags.Active) != 0 ? WorldObjectAction.Deactivate : WorldObjectAction.Activate,
                        0, out result);

                case WorldObjectKind.Trap:
                case WorldObjectKind.SpikeTrap:
                case WorldObjectKind.DartTrap:
                case WorldObjectKind.FallingBlockTrap:
                case WorldObjectKind.Crusher:
                    if (interaction == WorldObjectInteraction.Enter || interaction == WorldObjectInteraction.Primary)
                        return Apply(in current, WorldObjectAction.Trigger, 0, out result);
                    if (interaction == WorldObjectInteraction.Secondary || interaction == WorldObjectInteraction.Exit)
                        return Apply(in current, WorldObjectAction.Reset, 0, out result);
                    return false;

                case WorldObjectKind.SecretDoor:
                case WorldObjectKind.RotatingWall:
                    if ((current.State & WorldObjectStateFlags.Hidden) != 0)
                        return Apply(in current, WorldObjectAction.Reveal, 0, out result);
                    return Apply(in current, current.IsOpen ? WorldObjectAction.Close : WorldObjectAction.Open, 0, out result);
                case WorldObjectKind.BreakableWall:
                    if (interaction != WorldObjectInteraction.Attack && interaction != WorldObjectInteraction.Primary) return false;
                    return Destroy(in current, out result);

                case WorldObjectKind.MineCart:
                case WorldObjectKind.Cart:
                    if (interaction == WorldObjectInteraction.Attack) return false;
                    return Apply(in current, WorldObjectAction.Toggle, 0, out result);

                case WorldObjectKind.Ladder:
                case WorldObjectKind.Rope:
                case WorldObjectKind.Zipline:
                    return Apply(in current, WorldObjectAction.Toggle, 0, out result);

                case WorldObjectKind.Teleporter:
                    if (!current.IsPowered) return false;
                    return Apply(in current, WorldObjectAction.Activate, 0, out result);
                case WorldObjectKind.Checkpoint:
                    return Apply(in current, WorldObjectAction.Activate, 0, out result);
                case WorldObjectKind.SpawnPoint:
                    return false;
                default:
                    return false;
            }
        }

        private static bool Apply(in WorldObjectResolvedState current, WorldObjectAction action, int argument,
            out WorldObjectInteractionResult result)
        {
            result = default;
            if (!WorldObjectActions.TryApply(in current, action, argument, out WorldObjectStateDelta delta, out WorldObjectSignal signal))
                return false;
            result = new WorldObjectInteractionResult { Delta = delta, Signal = signal, Changed = true };
            return true;
        }

        private static bool Loot(in WorldObjectResolvedState current, out WorldObjectInteractionResult result)
        {
            result = default;
            if ((current.Descriptor.Capabilities & WorldObjectCapabilities.Lootable) == 0 ||
                (current.State & WorldObjectStateFlags.Looted) != 0 || current.IsLocked)
                return false;

            var delta = Delta(in current);
            delta.State |= WorldObjectStateFlags.Looted;
            result = new WorldObjectInteractionResult { Delta = delta, Signal = WorldObjectSignal.Looted, Changed = true };
            return true;
        }

        private static bool Destroy(in WorldObjectResolvedState current, out WorldObjectInteractionResult result)
        {
            result = default;
            if ((current.Descriptor.Capabilities & WorldObjectCapabilities.Destructible) == 0) return false;
            var delta = Delta(in current);
            delta.State |= WorldObjectStateFlags.Destroyed;
            delta.State &= ~(WorldObjectStateFlags.Active | WorldObjectStateFlags.Open | WorldObjectStateFlags.Moving);
            result = new WorldObjectInteractionResult { Delta = delta, Signal = WorldObjectSignal.Destroyed, Changed = true };
            return true;
        }

        private static WorldObjectStateDelta Delta(in WorldObjectResolvedState current) => new WorldObjectStateDelta
        {
            Id = current.Descriptor.Id,
            State = current.State,
            RuntimeValue0 = current.RuntimeValue0,
            RuntimeValue1 = current.RuntimeValue1,
        };
    }

    /// <summary>
    /// Deterministic, low-cost baseline geometry for every registered world-object kind. Geometry is deliberately
    /// simple and gameplay-readable; richer style-specific decoration can layer on top without changing identity.
    /// </summary>
    public static class WorldObjectGeometryEmitter
    {
        public static bool Emit(IStructureAuthoringSession authoring, in WorldObjectResolvedState state)
        {
            if (authoring == null || !state.Descriptor.IsWellFormed || state.IsDestroyed) return false;

            DecorationBounds b = state.Descriptor.Bounds;
            int3 min = b.Min;
            int3 size = math.max(new int3(1), b.Size);
            int3 centre = min + size / 2;

            switch (state.Descriptor.Kind)
            {
                case WorldObjectKind.Door: Door(authoring, min, size, state.IsOpen); break;
                case WorldObjectKind.Gate: Gate(authoring, min, size, state.IsOpen, false); break;
                case WorldObjectKind.Portcullis: Gate(authoring, min, size, state.IsOpen, true); break;
                case WorldObjectKind.Drawbridge: Drawbridge(authoring, min, size, state.IsOpen); break;
                case WorldObjectKind.Elevator: Elevator(authoring, min, size, state.RuntimeValue0); break;
                case WorldObjectKind.MovingPlatform: Platform(authoring, min, size); break;

                case WorldObjectKind.Lever: Lever(authoring, min, size, (state.State & WorldObjectStateFlags.Active) != 0); break;
                case WorldObjectKind.Switch: Switch(authoring, min, size, (state.State & WorldObjectStateFlags.Active) != 0); break;
                case WorldObjectKind.Button: Button(authoring, min, size, (state.State & WorldObjectStateFlags.Active) != 0); break;
                case WorldObjectKind.PressurePlate: Plate(authoring, min, size, (state.State & WorldObjectStateFlags.Triggered) != 0); break;
                case WorldObjectKind.PullChain: PullChain(authoring, centre, size); break;
                case WorldObjectKind.Winch: Winch(authoring, min, size); break;
                case WorldObjectKind.Valve: Valve(authoring, centre, size); break;
                case WorldObjectKind.Generator: Generator(authoring, min, size, state.IsPowered); break;
                case WorldObjectKind.FuseBox: FuseBox(authoring, min, size, state.IsPowered); break;

                case WorldObjectKind.Chest: Chest(authoring, min, size, state.IsOpen); break;
                case WorldObjectKind.Dresser: Dresser(authoring, min, size); break;
                case WorldObjectKind.Cabinet: Cabinet(authoring, min, size); break;
                case WorldObjectKind.Crate: Crate(authoring, min, size); break;
                case WorldObjectKind.Barrel: Barrel(authoring, centre, size); break;
                case WorldObjectKind.WeaponRack: Rack(authoring, min, size, true); break;
                case WorldObjectKind.Bookshelf: Rack(authoring, min, size, false); break;
                case WorldObjectKind.Bed: Bed(authoring, min, size); break;
                case WorldObjectKind.Chair: Chair(authoring, min, size); break;
                case WorldObjectKind.Bench: Bench(authoring, min, size); break;
                case WorldObjectKind.Altar: Altar(authoring, min, size); break;
                case WorldObjectKind.Bell: Bell(authoring, centre, size); break;

                case WorldObjectKind.Torch: Torch(authoring, centre, size); break;
                case WorldObjectKind.Lantern: Lantern(authoring, min, size); break;
                case WorldObjectKind.Brazier: Brazier(authoring, centre, size); break;
                case WorldObjectKind.Fireplace: Fireplace(authoring, min, size); break;

                case WorldObjectKind.Trap: Plate(authoring, min, size, (state.State & WorldObjectStateFlags.Triggered) != 0); break;
                case WorldObjectKind.SpikeTrap: SpikeTrap(authoring, min, size, (state.State & WorldObjectStateFlags.Triggered) != 0); break;
                case WorldObjectKind.DartTrap: DartTrap(authoring, min, size); break;
                case WorldObjectKind.FallingBlockTrap: FallingBlock(authoring, min, size); break;
                case WorldObjectKind.Crusher: Crusher(authoring, min, size); break;
                case WorldObjectKind.SecretDoor: Door(authoring, min, size, state.IsOpen); break;
                case WorldObjectKind.RotatingWall: RotatingWall(authoring, min, size, state.IsOpen); break;
                case WorldObjectKind.BreakableWall: Wall(authoring, min, size); break;

                case WorldObjectKind.MineCart: Cart(authoring, min, size, true); break;
                case WorldObjectKind.Cart: Cart(authoring, min, size, false); break;
                case WorldObjectKind.Ladder: Ladder(authoring, min, size); break;
                case WorldObjectKind.Rope: Rope(authoring, centre, size); break;
                case WorldObjectKind.Zipline: Zipline(authoring, min, size); break;
                case WorldObjectKind.Teleporter: Teleporter(authoring, centre, size, state.IsPowered); break;
                case WorldObjectKind.Checkpoint: Checkpoint(authoring, min, size); break;
                case WorldObjectKind.SpawnPoint: SpawnPoint(authoring, centre, size); break;
                default: return false;
            }
            return true;
        }

        private static void Box(IStructureAuthoringSession a, int3 p, int3 s, byte m)
        {
            a.Box(p, math.max(new int3(1), s), m);
        }

        private static void Door(IStructureAuthoringSession a, int3 p, int3 s, bool open)
        {
            if (open) Box(a, p, new int3(math.max(1, s.x / 6), s.y, s.z), GameMaterialIds.Wood);
            else Box(a, p, s, GameMaterialIds.Wood);
            Box(a, p + new int3(math.max(0, s.x - 2), s.y / 2, math.max(0, s.z - 1)), new int3(2, 2, 1), GameMaterialIds.Gold);
        }

        private static void Gate(IStructureAuthoringSession a, int3 p, int3 s, bool open, bool bars)
        {
            if (open) p.y += math.max(1, s.y - 2);
            if (!bars) { Box(a, p, s, GameMaterialIds.Wood); return; }
            int step = math.max(2, s.x / 5);
            for (int x = 0; x < s.x; x += step) Box(a, p + new int3(x, 0, 0), new int3(1, s.y, s.z), GameMaterialIds.DarkStone);
        }

        private static void Drawbridge(IStructureAuthoringSession a, int3 p, int3 s, bool open)
        {
            Box(a, open ? p + new int3(0, 0, math.max(0, s.z - 2)) : p, open ? new int3(s.x, s.y, 2) : s, GameMaterialIds.Wood);
        }

        private static void Elevator(IStructureAuthoringSession a, int3 p, int3 s, int stop)
        {
            int y = p.y + math.max(0, stop) * math.max(1, s.y / 2);
            Box(a, new int3(p.x, y, p.z), new int3(s.x, math.max(1, s.y / 5), s.z), GameMaterialIds.DarkStone);
            Box(a, new int3(p.x, y + math.max(1, s.y / 5), p.z), new int3(1, math.max(2, s.y / 2), s.z), GameMaterialIds.Wood);
        }

        private static void Platform(IStructureAuthoringSession a, int3 p, int3 s) => Box(a, p, new int3(s.x, math.max(1, s.y / 4), s.z), GameMaterialIds.DarkStone);
        private static void Lever(IStructureAuthoringSession a, int3 p, int3 s, bool active) { Box(a, p, new int3(s.x, math.max(1, s.y / 3), s.z), GameMaterialIds.Stone); Box(a, p + new int3(s.x / 2, math.max(1, s.y / 3), s.z / 2), new int3(1, math.max(2, s.y * 2 / 3), 1), active ? GameMaterialIds.Gold : GameMaterialIds.Wood); }
        private static void Switch(IStructureAuthoringSession a, int3 p, int3 s, bool active) { Box(a, p, s, GameMaterialIds.DarkStone); Box(a, p + new int3(s.x / 4, s.y / 4, 0), new int3(math.max(1, s.x / 2), math.max(1, s.y / 2), 1), active ? GameMaterialIds.Gold : GameMaterialIds.Stone); }
        private static void Button(IStructureAuthoringSession a, int3 p, int3 s, bool active) { Box(a, p, s, GameMaterialIds.Stone); Box(a, p + new int3(s.x / 4, s.y / 4, math.max(0, s.z - 1)), new int3(math.max(1, s.x / 2), math.max(1, s.y / 2), active ? 1 : 2), GameMaterialIds.Gold); }
        private static void Plate(IStructureAuthoringSession a, int3 p, int3 s, bool pressed) => Box(a, p, new int3(s.x, pressed ? 1 : math.max(1, s.y / 3), s.z), GameMaterialIds.DarkStone);
        private static void PullChain(IStructureAuthoringSession a, int3 c, int3 s) { for (int y = 0; y < s.y; y += 2) Box(a, c + new int3(0, y - s.y / 2, 0), new int3(1), GameMaterialIds.Gold); }
        private static void Winch(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, new int3(s.x, math.max(2, s.y / 3), s.z), GameMaterialIds.Wood); a.Cylinder(p.x + s.x / 2, p.y + s.y / 2, p.z + s.z / 2, math.max(1, math.min(s.x, s.z) / 3), math.max(2, s.y / 2), GameMaterialIds.DarkStone); }
        private static void Valve(IStructureAuthoringSession a, int3 c, int3 s) { a.Cylinder(c.x, c.y, c.z, math.max(2, math.min(s.x, s.y) / 2), math.max(1, s.z / 4), GameMaterialIds.DarkStone); Box(a, c - new int3(s.x / 2, 0, 0), new int3(s.x, 1, 1), GameMaterialIds.Gold); Box(a, c - new int3(0, s.y / 2, 0), new int3(1, s.y, 1), GameMaterialIds.Gold); }
        private static void Generator(IStructureAuthoringSession a, int3 p, int3 s, bool powered) { Box(a, p, s, GameMaterialIds.DarkStone); Box(a, p + new int3(1, s.y / 3, math.max(0, s.z - 1)), new int3(math.max(1, s.x - 2), math.max(1, s.y / 3), 1), powered ? GameMaterialIds.Gold : GameMaterialIds.Stone); }
        private static void FuseBox(IStructureAuthoringSession a, int3 p, int3 s, bool powered) { Box(a, p, s, GameMaterialIds.DarkStone); Box(a, p + new int3(s.x / 3, s.y / 3, math.max(0, s.z - 1)), new int3(math.max(1, s.x / 3), math.max(1, s.y / 3), 1), powered ? GameMaterialIds.Gold : GameMaterialIds.Wood); }

        private static void Chest(IStructureAuthoringSession a, int3 p, int3 s, bool open) { Box(a, p, new int3(s.x, math.max(1, s.y * 2 / 3), s.z), GameMaterialIds.Wood); Box(a, p + new int3(0, open ? s.y - 1 : s.y * 2 / 3, open ? math.max(0, s.z - 1) : 0), new int3(s.x, math.max(1, s.y / 3), open ? 1 : s.z), GameMaterialIds.Wood); }
        private static void Dresser(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Wood); for (int y = s.y / 4; y < s.y; y += math.max(2, s.y / 3)) Box(a, p + new int3(1, y, math.max(0, s.z - 1)), new int3(math.max(1, s.x - 2), 1, 1), GameMaterialIds.DarkStone); }
        private static void Cabinet(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Wood); Box(a, p + new int3(s.x / 2, 1, math.max(0, s.z - 1)), new int3(1, math.max(1, s.y - 2), 1), GameMaterialIds.DarkStone); }
        private static void Crate(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Wood); Box(a, p, new int3(1, s.y, s.z), GameMaterialIds.DarkStone); Box(a, p + new int3(math.max(0, s.x - 1), 0, 0), new int3(1, s.y, s.z), GameMaterialIds.DarkStone); }
        private static void Barrel(IStructureAuthoringSession a, int3 c, int3 s) { a.Cylinder(c.x, c.y - s.y / 2, c.z, math.max(1, math.min(s.x, s.z) / 2), s.y, GameMaterialIds.Wood); Box(a, c + new int3(-s.x / 2, -s.y / 4, -s.z / 2), new int3(s.x, 1, s.z), GameMaterialIds.DarkStone); }
        private static void Rack(IStructureAuthoringSession a, int3 p, int3 s, bool weapons) { Box(a, p, new int3(1, s.y, s.z), GameMaterialIds.Wood); Box(a, p + new int3(math.max(0, s.x - 1), 0, 0), new int3(1, s.y, s.z), GameMaterialIds.Wood); for (int y = 0; y < s.y; y += math.max(2, s.y / 4)) Box(a, p + new int3(0, y, 0), new int3(s.x, 1, s.z), weapons ? GameMaterialIds.DarkStone : GameMaterialIds.Wood); }
        private static void Bed(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, new int3(s.x, math.max(2, s.y / 3), s.z), GameMaterialIds.Wood); Box(a, p + new int3(1, math.max(1, s.y / 3), 1), new int3(math.max(1, s.x - 2), math.max(1, s.y / 3), math.max(1, s.z - 2)), GameMaterialIds.Cloth); }
        private static void Chair(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p + new int3(0, s.y / 2, 0), new int3(s.x, math.max(1, s.y / 5), s.z), GameMaterialIds.Wood); Box(a, p, new int3(math.max(1, s.x / 5), s.y, math.max(1, s.z / 5)), GameMaterialIds.Wood); Box(a, p + new int3(0, 0, math.max(0, s.z - 1)), new int3(s.x, s.y, 1), GameMaterialIds.Wood); }
        private static void Bench(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p + new int3(0, s.y / 2, 0), new int3(s.x, math.max(1, s.y / 4), s.z), GameMaterialIds.Wood); Box(a, p, new int3(2, math.max(1, s.y / 2), s.z), GameMaterialIds.Wood); Box(a, p + new int3(math.max(0, s.x - 2), 0, 0), new int3(2, math.max(1, s.y / 2), s.z), GameMaterialIds.Wood); }
        private static void Altar(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Stone); Box(a, p + new int3(-1, math.max(0, s.y - 2), -1), new int3(s.x + 2, 2, s.z + 2), GameMaterialIds.MasonryMedium); }
        private static void Bell(IStructureAuthoringSession a, int3 c, int3 s) { a.Cylinder(c.x, c.y - s.y / 2, c.z, math.max(2, math.min(s.x, s.z) / 2), math.max(2, s.y * 2 / 3), GameMaterialIds.Gold); Box(a, c + new int3(0, -s.y / 2, 0), new int3(1, s.y, 1), GameMaterialIds.DarkStone); }

        private static void Torch(IStructureAuthoringSession a, int3 c, int3 s) { Box(a, c - new int3(0, s.y / 2, 0), new int3(2, math.max(2, s.y), 2), GameMaterialIds.Wood); Box(a, c + new int3(-2, math.max(0, s.y / 3), -2), new int3(4, 3, 4), GameMaterialIds.Gold); }
        private static void Lantern(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.DarkStone); Box(a, p + new int3(1, 1, 1), math.max(new int3(1), s - 2), GameMaterialIds.Gold); }
        private static void Brazier(IStructureAuthoringSession a, int3 c, int3 s) { a.Cylinder(c.x, c.y - s.y / 2, c.z, math.max(2, math.min(s.x, s.z) / 2), math.max(2, s.y / 3), GameMaterialIds.DarkStone); Box(a, c + new int3(-s.x / 3, 0, -s.z / 3), new int3(math.max(2, s.x * 2 / 3), math.max(2, s.y / 3), math.max(2, s.z * 2 / 3)), GameMaterialIds.Gold); }
        private static void Fireplace(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Stone); Box(a, p + new int3(math.max(1, s.x / 4), 0, math.max(0, s.z - 2)), new int3(math.max(1, s.x / 2), math.max(2, s.y / 2), 2), GameMaterialIds.Gold); }

        private static void SpikeTrap(IStructureAuthoringSession a, int3 p, int3 s, bool triggered) { Box(a, p, new int3(s.x, 1, s.z), GameMaterialIds.DarkStone); int h = triggered ? math.max(2, s.y) : 1; for (int x = 1; x < s.x; x += 3) for (int z = 1; z < s.z; z += 3) Box(a, p + new int3(x, 1, z), new int3(1, h, 1), GameMaterialIds.DarkStone); }
        private static void DartTrap(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, s, GameMaterialIds.Stone); for (int y = 1; y < s.y; y += 3) Box(a, p + new int3(math.max(0, s.x - 1), y, math.max(0, s.z / 2)), new int3(1), GameMaterialIds.Empty); }
        private static void FallingBlock(IStructureAuthoringSession a, int3 p, int3 s) => Box(a, p + new int3(0, math.max(0, s.y - math.max(2, s.y / 3)), 0), new int3(s.x, math.max(2, s.y / 3), s.z), GameMaterialIds.Stone);
        private static void Crusher(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, new int3(s.x, math.max(2, s.y / 4), s.z), GameMaterialIds.DarkStone); Box(a, p + new int3(0, math.max(0, s.y * 3 / 4), 0), new int3(s.x, math.max(2, s.y / 4), s.z), GameMaterialIds.DarkStone); }
        private static void RotatingWall(IStructureAuthoringSession a, int3 p, int3 s, bool open) => Box(a, p, open ? new int3(math.max(1, s.x / 5), s.y, s.z) : s, GameMaterialIds.Stone);
        private static void Wall(IStructureAuthoringSession a, int3 p, int3 s) => Box(a, p, s, GameMaterialIds.Stone);

        private static void Cart(IStructureAuthoringSession a, int3 p, int3 s, bool mine) { Box(a, p + new int3(0, math.max(1, s.y / 3), 0), new int3(s.x, math.max(2, s.y / 2), s.z), mine ? GameMaterialIds.DarkStone : GameMaterialIds.Wood); int r = math.max(1, s.y / 4); a.Cylinder(p.x + r, p.y, p.z, r, math.max(1, s.z), GameMaterialIds.DarkStone); a.Cylinder(p.x + math.max(r, s.x - r), p.y, p.z, r, math.max(1, s.z), GameMaterialIds.DarkStone); }
        private static void Ladder(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, new int3(1, s.y, 1), GameMaterialIds.Wood); Box(a, p + new int3(math.max(1, s.x - 1), 0, 0), new int3(1, s.y, 1), GameMaterialIds.Wood); for (int y = 1; y < s.y; y += 3) Box(a, p + new int3(0, y, 0), new int3(s.x, 1, 1), GameMaterialIds.Wood); }
        private static void Rope(IStructureAuthoringSession a, int3 c, int3 s) { for (int y = -s.y / 2; y < s.y / 2; y++) Box(a, c + new int3(0, y, 0), new int3(1), GameMaterialIds.Wood); }
        private static void Zipline(IStructureAuthoringSession a, int3 p, int3 s) { int n = math.max(s.x, s.z); for (int i = 0; i < n; i++) Box(a, p + new int3(s.x > s.z ? i : 0, math.max(0, s.y - 1 - i * math.max(1, s.y / math.max(1, n)) / 2), s.z >= s.x ? i : 0), new int3(1), GameMaterialIds.DarkStone); }
        private static void Teleporter(IStructureAuthoringSession a, int3 c, int3 s, bool powered) { a.Cylinder(c.x, c.y - s.y / 2, c.z, math.max(2, math.min(s.x, s.z) / 2), math.max(2, s.y / 5), GameMaterialIds.DarkStone); Box(a, c + new int3(-1, -s.y / 4, -1), new int3(3, math.max(2, s.y / 2), 3), powered ? GameMaterialIds.Crystal : GameMaterialIds.Stone); }
        private static void Checkpoint(IStructureAuthoringSession a, int3 p, int3 s) { Box(a, p, new int3(2, s.y, 2), GameMaterialIds.Wood); Box(a, p + new int3(2, math.max(0, s.y - s.y / 3), 0), new int3(math.max(2, s.x - 2), math.max(2, s.y / 3), 1), GameMaterialIds.Cloth); }
        private static void SpawnPoint(IStructureAuthoringSession a, int3 c, int3 s) { Box(a, c + new int3(-1, -1, -1), new int3(3), GameMaterialIds.Crystal); }
    }
}
