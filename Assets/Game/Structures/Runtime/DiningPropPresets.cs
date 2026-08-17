using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public enum DiningLongAxis : byte
    {
        Auto = 0,
        X = 1,
        Z = 2,
    }

    public static class DiningPropPresets
    {
        public static DecorationPropDescriptor Table(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(in context, DiningSceneDefinition.SceneId, DiningSceneDefinition.TableSlot);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Table,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(
                    math.min(84, 56 + wealth * 6 + style.SilhouetteBias * 2 + (int)(seed & 3u) * 4),
                    9 + math.max(0, style.SilhouetteBias),
                    14 + wealth + (int)((seed >> 3) & 1u) * 2),
                Clearance = new int3(4, 0, 4),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x7AB1E001u),
            };
        }

        public static DecorationPropDescriptor Bench(in DecorationContext context, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, DiningSceneDefinition.SceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bench,
                AcceptedSockets = DecorationSocketKind.BesideAnchor | DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.AnchorRelative,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation | DecorationInteractionFlags.Destructible,
                Size = new int3(
                    44 + (int)(seed & 3u) * 4,
                    6 + math.max(0, style.SilhouetteBias),
                    6 + (int)((seed >> 3) & 1u)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBE4C4001u),
            };
        }

        public static DecorationPropDescriptor Chair(in DecorationContext context, uint slotId)
        {
            uint seed = DecorationSeed.ForSlot(in context, DiningSceneDefinition.SceneId, slotId);
            DecorationStyleProfile style = DecorationContextProfiles.ResolveStyle(context.StyleId);
            int wealth = (int)context.Wealth;
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Chair,
                AcceptedSockets = DecorationSocketKind.BesideAnchor | DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.AnchorRelative,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.BlocksNavigation |
                              DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.Movable,
                Size = new int3(
                    7 + (wealth >= (int)DecorationWealthTier.Wealthy ? 1 : 0),
                    10 + math.max(0, style.SilhouetteBias) + (int)((seed >> 2) & 1u),
                    7 + (int)(seed & 1u)),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xC4A17001u),
            };
        }
    }

    public static class DiningSceneDefinition
    {
        public const uint SceneId = 0x44494E31u; // DIN1
        public const uint TableSlot = 1;
        public const uint BenchNegativeSlot = 2;
        public const uint BenchPositiveSlot = 3;
        public const uint ChairNegativeSlot = 4;
        public const uint ChairPositiveSlot = 5;

        public static DecorationSceneSlot[] CreateSlots() => new[]
        {
            Slot(TableSlot, DecorationPropFamily.Table, DecorationSocketKind.Floor, 0, true),
            Slot(BenchNegativeSlot, DecorationPropFamily.Bench, DecorationSocketKind.BesideAnchor, TableSlot, true),
            Slot(BenchPositiveSlot, DecorationPropFamily.Bench, DecorationSocketKind.BesideAnchor, TableSlot, true),
            Slot(ChairNegativeSlot, DecorationPropFamily.Chair, DecorationSocketKind.BesideAnchor, TableSlot, false, 2),
            Slot(ChairPositiveSlot, DecorationPropFamily.Chair, DecorationSocketKind.BesideAnchor, TableSlot, false, 2),
        };

        public static int OptionalSeatBudget(in DecorationContext context)
        {
            if ((int)context.Condition <= (int)DecorationConditionTier.Abandoned)
                return 0;
            if ((int)context.Wealth >= (int)DecorationWealthTier.Wealthy)
                return 2;
            return (int)context.Wealth >= (int)DecorationWealthTier.Comfortable ? 1 : 0;
        }

        private static DecorationSceneSlot Slot(
            uint id,
            DecorationPropFamily family,
            DecorationSocketKind socket,
            uint anchor,
            bool required,
            ushort weight = 1) => new DecorationSceneSlot
            {
                SlotId = id,
                Family = family,
                RequestedSocket = socket,
                AnchorSlotId = anchor,
                Weight = weight,
                Required = required,
            };
    }
}
