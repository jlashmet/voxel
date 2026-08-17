using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public static class CaveCampPropPresets
    {
        public static DecorationPropDescriptor Campfire(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(
                in context, CaveCampSceneDefinition.SceneId, CaveCampSceneDefinition.CampfireSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Campfire,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.EmitsLight |
                              DecorationInteractionFlags.EmitsParticles,
                Size = new int3(5 + (int)(seed & 1u) * 2, 4, 5 + (int)((seed >> 1) & 1u) * 2),
                Clearance = new int3(2, 0, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xCA4F1EEu),
            };
        }

        public static DecorationPropDescriptor Bedroll(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(
                in context, CaveCampSceneDefinition.SceneId, CaveCampSceneDefinition.BedrollSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Bedroll,
                AcceptedSockets = DecorationSocketKind.Floor,
                MountMode = DecorationMountMode.Floor,
                Backend = DecorationRenderBackend.ThinSurface,
                Interaction = DecorationInteractionFlags.Destructible | DecorationInteractionFlags.Movable,
                Size = new int3(5 + (int)(seed & 1u), 1, 9 + (int)((seed >> 1) & 1u) * 2),
                Clearance = new int3(1, 0, 1),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0xBED4011u),
            };
        }

        public static DecorationPropDescriptor Lantern(in DecorationContext context)
        {
            uint seed = DecorationSeed.ForSlot(
                in context, CaveCampSceneDefinition.SceneId, CaveCampSceneDefinition.LanternSlot);
            return new DecorationPropDescriptor
            {
                Family = DecorationPropFamily.Lantern,
                AcceptedSockets = DecorationSocketKind.Wall,
                MountMode = DecorationMountMode.Wall,
                Backend = DecorationRenderBackend.BoxAssembly,
                Interaction = DecorationInteractionFlags.Destructible |
                              DecorationInteractionFlags.EmitsLight,
                Size = new int3(3, 6 + (int)(seed & 1u), 3),
                Clearance = new int3(2, 2, 2),
                Variant = DecorationSeed.Derive(seed, context.StyleId ^ 0x1A47E2Au),
            };
        }
    }

    public static class CaveCampSceneDefinition
    {
        public const uint SceneId = 0x43414D50u; // CAMP
        public const uint CampfireSlot = 1;
        public const uint BedrollSlot = 2;
        public const uint LanternSlot = 3;

        public static DecorationSceneSlot[] CreateSlots() => new[]
        {
            new DecorationSceneSlot
            {
                SlotId = CampfireSlot,
                Family = DecorationPropFamily.Campfire,
                RequestedSocket = DecorationSocketKind.Floor,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = BedrollSlot,
                Family = DecorationPropFamily.Bedroll,
                RequestedSocket = DecorationSocketKind.Floor,
                Weight = 1,
                Required = true,
            },
            new DecorationSceneSlot
            {
                SlotId = LanternSlot,
                Family = DecorationPropFamily.Lantern,
                RequestedSocket = DecorationSocketKind.Wall,
                Weight = 1,
                Required = true,
            },
        };
    }

    /// <summary>
    /// First cave decoration scene. It consumes cave-specific surface candidates but resolves every
    /// prop through the same DecorationSceneScheduler and DecorationPlacementResolver used by rooms.
    /// </summary>
    public static class CaveCampSceneResolver
    {
        public const int PlacementCount = 3;

        public static bool TryResolve(
            in DecorationSpace space,
            in DecorationContext context,
            CaveDecorationCandidate[] candidates,
            DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed ||
                space.Kind != DecorationSpaceKind.CaveChamber ||
                context.StructureKind != DecorationStructureKind.Cave ||
                context.SpaceKind != DecorationSpaceKind.CaveChamber)
                return false;

            DecorationSceneSlot[] slots = CaveCampSceneDefinition.CreateSlots();
            if (!DecorationSceneScheduler.TrySelectAndOrder(
                    in context, CaveCampSceneDefinition.SceneId, slots, 0,
                    out DecorationSceneSlot[] orderedSlots))
                return false;
            if (orderedSlots.Length != PlacementCount)
                return false;

            DecorationSocket[] sockets = CaveDecorationSurfaceAnalyzer.PlacementSockets(candidates);
            if (sockets.Length == 0)
                return false;

            var resolved = new DecorationPlacement[PlacementCount];
            for (int i = 0; i < orderedSlots.Length; i++)
            {
                DecorationPropDescriptor descriptor = Descriptor(in context, orderedSlots[i].Family);
                if (!descriptor.IsWellFormed || !descriptor.Accepts(orderedSlots[i].RequestedSocket))
                    return false;

                if (!DecorationPlacementResolver.TryPlace(
                        in space,
                        in context,
                        CaveCampSceneDefinition.SceneId,
                        orderedSlots[i].SlotId,
                        in descriptor,
                        sockets,
                        exclusions,
                        resolved,
                        i,
                        out resolved[i]))
                    return false;
            }

            placements = resolved;
            return true;
        }

        private static DecorationPropDescriptor Descriptor(
            in DecorationContext context,
            DecorationPropFamily family)
        {
            switch (family)
            {
                case DecorationPropFamily.Campfire:
                    return CaveCampPropPresets.Campfire(in context);
                case DecorationPropFamily.Bedroll:
                    return CaveCampPropPresets.Bedroll(in context);
                case DecorationPropFamily.Lantern:
                    return CaveCampPropPresets.Lantern(in context);
                default:
                    return default;
            }
        }
    }
}
