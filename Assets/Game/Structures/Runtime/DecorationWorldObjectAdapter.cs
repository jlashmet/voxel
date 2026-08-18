using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public static class DecorationWorldObjectAdapter
    {
        public static bool TryCreate(in DecorationPlacement placement, out WorldObjectDescriptor descriptor)
        {
            descriptor = default;
            WorldObjectCapabilities capabilities = MapCapabilities(placement.Interaction);
            if ((capabilities & (WorldObjectCapabilities.Interactable | WorldObjectCapabilities.Stateful |
                                 WorldObjectCapabilities.Movable | WorldObjectCapabilities.Destructible |
                                 WorldObjectCapabilities.Container | WorldObjectCapabilities.Lootable)) == 0)
                return false;

            WorldObjectKind kind = MapKind(placement.Family);
            if (kind == WorldObjectKind.Unknown) return false;

            WorldObjectPreset preset = WorldObjectContentCatalog.Get(kind);
            capabilities |= preset.Capabilities;

            descriptor = new WorldObjectDescriptor
            {
                Id = WorldObjectIds.FromDecoration(placement.Id),
                Kind = kind,
                Capabilities = capabilities,
                Bounds = placement.Bounds,
                Facing = placement.Facing,
                Variant = placement.Variant,
                LocalKey = placement.SlotId,
                ParentId = placement.SceneId,
                DefaultState = preset.DefaultState,
                Parameter0 = preset.Parameter0,
                Parameter1 = preset.Parameter1,
                Parameter2 = preset.Parameter2,
                Parameter3 = preset.Parameter3,
            };
            return descriptor.IsWellFormed;
        }

        public static WorldObjectCapabilities MapCapabilities(DecorationInteractionFlags flags)
        {
            WorldObjectCapabilities value = WorldObjectCapabilities.None;
            if ((flags & DecorationInteractionFlags.BlocksNavigation) != 0) value |= WorldObjectCapabilities.BlocksNavigation;
            if ((flags & DecorationInteractionFlags.Destructible) != 0) value |= WorldObjectCapabilities.Destructible | WorldObjectCapabilities.Stateful | WorldObjectCapabilities.Persistent;
            if ((flags & DecorationInteractionFlags.Container) != 0) value |= WorldObjectCapabilities.Container | WorldObjectCapabilities.Interactable | WorldObjectCapabilities.Stateful | WorldObjectCapabilities.Persistent;
            if ((flags & DecorationInteractionFlags.Lootable) != 0) value |= WorldObjectCapabilities.Lootable | WorldObjectCapabilities.Interactable | WorldObjectCapabilities.Stateful | WorldObjectCapabilities.Persistent;
            if ((flags & DecorationInteractionFlags.Movable) != 0) value |= WorldObjectCapabilities.Movable | WorldObjectCapabilities.Interactable | WorldObjectCapabilities.Stateful | WorldObjectCapabilities.Persistent;
            if ((flags & DecorationInteractionFlags.EmitsLight) != 0) value |= WorldObjectCapabilities.EmitsLight;
            if ((flags & DecorationInteractionFlags.EmitsParticles) != 0) value |= WorldObjectCapabilities.EmitsParticles;
            return value;
        }

        private static WorldObjectKind MapKind(DecorationPropFamily family)
        {
            switch (family)
            {
                case DecorationPropFamily.Bed: return WorldObjectKind.Bed;
                case DecorationPropFamily.Dresser: return WorldObjectKind.Dresser;
                case DecorationPropFamily.WallTorch: return WorldObjectKind.Torch;
                case DecorationPropFamily.Chair: return WorldObjectKind.Chair;
                case DecorationPropFamily.Bench: return WorldObjectKind.Bench;
                case DecorationPropFamily.Chest: return WorldObjectKind.Chest;
                case DecorationPropFamily.Bookcase: return WorldObjectKind.Bookshelf;
                case DecorationPropFamily.Fireplace: return WorldObjectKind.Fireplace;
                case DecorationPropFamily.WeaponRack: return WorldObjectKind.WeaponRack;
                case DecorationPropFamily.Altar: return WorldObjectKind.Altar;
                case DecorationPropFamily.Crate: return WorldObjectKind.Crate;
                case DecorationPropFamily.Barrel: return WorldObjectKind.Barrel;
                case DecorationPropFamily.Lantern: return WorldObjectKind.Lantern;
                default: return WorldObjectKind.Unknown;
            }
        }
    }
}
