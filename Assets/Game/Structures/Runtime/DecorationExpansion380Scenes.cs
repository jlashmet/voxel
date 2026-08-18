using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion380SceneKind : byte
    {
        SpellClassroom = 0,
        WizardLibrary = 1,
        ForbiddenArchive = 2,
    }

    public struct DecorationExpansion380SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion380Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
    }

    public static class DecorationExpansion380SceneCatalog
    {
        public static uint SceneId(DecorationExpansion380SceneKind kind) => 0xE3800000u | ((uint)kind + 1u);

        public static DecorationExpansion380SceneSlot[] Slots(DecorationExpansion380SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion380SceneKind.SpellClassroom:
                    return new[]
                    {
                        S(1, DecorationExpansion380Kind.StudentSpellDesk, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion380Kind.RunePracticeBoard, DecorationSocketKind.Wall, true, 6),
                        S(3, DecorationExpansion380Kind.EnchantedLectern, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion380Kind.SpellTargetDummy, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion380Kind.WandPracticeRack, DecorationSocketKind.Wall, false, 4),
                        S(6, DecorationExpansion380Kind.FamiliarStudyPerch, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion380Kind.PortalLessonFrame, DecorationSocketKind.Floor, false, 3),
                    };
                case DecorationExpansion380SceneKind.WizardLibrary:
                    return new[]
                    {
                        S(1, DecorationExpansion380Kind.FloatingBookshelf, DecorationSocketKind.Wall, true, 7),
                        S(2, DecorationExpansion380Kind.ScriptoriumDesk, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion380Kind.ScrollSortingRack, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion380Kind.MagicalGlobe, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion380Kind.AnimatedMapTable, DecorationSocketKind.Floor, false, 5),
                        S(6, DecorationExpansion380Kind.QuillAndInkStation, DecorationSocketKind.Floor, false, 4),
                        S(7, DecorationExpansion380Kind.FacultyResearchDesk, DecorationSocketKind.Floor, false, 4),
                    };
                default:
                    return new[]
                    {
                        S(1, DecorationExpansion380Kind.ForbiddenBookCage, DecorationSocketKind.Floor, true, 7),
                        S(2, DecorationExpansion380Kind.ChainedTomeStand, DecorationSocketKind.Floor, true, 6),
                        S(3, DecorationExpansion380Kind.ArcaneArchiveChest, DecorationSocketKind.Floor, true, 6),
                        S(4, DecorationExpansion380Kind.MagicalSpecimenCabinet, DecorationSocketKind.Floor, false, 5),
                        S(5, DecorationExpansion380Kind.ConstellationProjector, DecorationSocketKind.Floor, false, 4),
                        S(6, DecorationExpansion380Kind.ApprenticeAlchemyDesk, DecorationSocketKind.Floor, false, 4),
                    };
            }
        }

        public static int OptionalBudget(DecorationExpansion380SceneKind kind, DecorationRegionTheme region, in DecorationContext context)
        {
            int budget = 2 + (int)context.Wealth / 2;
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            if (profile.IsWellFormed && profile.Prefers(DecorationRegionContentTags.Scholar)) budget += 2;
            if (profile.IsWellFormed && profile.Prefers(DecorationRegionContentTags.Enchanted)) budget += 1;
            if (context.Condition == DecorationConditionTier.Ruined) budget = 1;
            return budget;
        }

        private static DecorationExpansion380SceneSlot S(uint id, DecorationExpansion380Kind kind, DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion380SceneSlot { SlotId = id, Kind = kind, Socket = socket, Required = required, Weight = weight };
    }

    public static class DecorationExpansion380SceneResolver
    {
        public static bool TryResolve(DecorationExpansion380SceneKind kind, DecorationRegionTheme region,
            in DecorationSpace space, in DecorationContext context, DecorationExclusion[] exclusions,
            out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind) return false;
            DecorationExpansion380SceneSlot[] slots = DecorationExpansion380SceneCatalog.Slots(kind);
            uint sceneId = DecorationExpansion380SceneCatalog.SceneId(kind);
            var core = new DecorationSceneSlot[slots.Length];
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            for (int i = 0; i < slots.Length; i++)
            {
                DecorationExpansion380Recipe recipe = DecorationExpansion380Catalog.Recipe(slots[i].Kind);
                if (!recipe.IsWellFormed || (recipe.Sockets & slots[i].Socket) == 0) return false;
                ushort weight = slots[i].Weight;
                if (!slots[i].Required && profile.IsWellFormed && profile.Prefers(DecorationRegionContentTags.Scholar)) weight = (ushort)(weight + 4);
                core[i] = new DecorationSceneSlot
                {
                    SlotId = slots[i].SlotId, Family = recipe.ProxyFamily, RequestedSocket = slots[i].Socket,
                    Required = slots[i].Required, Weight = weight,
                };
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                    DecorationExpansion380SceneCatalog.OptionalBudget(kind, region, in context), out DecorationSceneSlot[] ordered)) return false;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var resolved = new DecorationPlacement[ordered.Length];
            int count = 0;
            for (int i = 0; i < ordered.Length; i++)
            {
                DecorationExpansion380SceneSlot slot = Find(slots, ordered[i].SlotId);
                DecorationPropDescriptor descriptor = DecorationExpansion380Catalog.Describe(in context, sceneId, slot.SlotId, slot.Kind);
                if (!descriptor.IsWellFormed) return false;
                bool placed = DecorationPlacementResolver.TryPlace(in space, in context, sceneId, slot.SlotId,
                    in descriptor, sockets, exclusions, resolved, count, out DecorationPlacement placement);
                if (!placed) { if (slot.Required) return false; continue; }
                resolved[count++] = placement;
            }
            placements = new DecorationPlacement[count];
            for (int i = 0; i < count; i++) placements[i] = resolved[i];
            return true;
        }

        private static DecorationExpansion380SceneSlot Find(DecorationExpansion380SceneSlot[] slots, uint slotId)
        {
            for (int i = 0; i < slots.Length; i++) if (slots[i].SlotId == slotId) return slots[i];
            return default;
        }
    }
}
