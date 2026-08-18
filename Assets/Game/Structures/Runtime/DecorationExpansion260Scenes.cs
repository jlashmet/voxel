using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationExpansion260SceneKind : byte
    {
        JewelerShop, GeneralStore, ArmoryShop, EnchantersWorkshop, FamiliarRoom, ArcaneGallery, NobleSalon, MusicRoom, PrivateChamber,
    }

    public struct DecorationExpansion260SceneSlot
    {
        public uint SlotId;
        public DecorationExpansion260Kind Kind;
        public DecorationSocketKind Socket;
        public ushort Weight;
        public bool Required;
        public DecorationSceneSlot Core(DecorationPropFamily family) => new DecorationSceneSlot
        {
            SlotId = SlotId, Family = family, RequestedSocket = Socket, Weight = Weight, Required = Required,
        };
    }

    public static class DecorationExpansion260SceneCatalog
    {
        public static uint SceneId(DecorationExpansion260SceneKind kind) => 0xE2600000u | ((uint)kind + 1u);

        public static DecorationExpansion260SceneSlot[] Slots(DecorationExpansion260SceneKind kind)
        {
            switch (kind)
            {
                case DecorationExpansion260SceneKind.JewelerShop:
                    return new[] { S(1,DecorationExpansion260Kind.JewelerBench,F,true,6), S(2,DecorationExpansion260Kind.GemDisplayCase,F,true,5), S(3,DecorationExpansion260Kind.CoinScale,F,false,4), S(4,DecorationExpansion260Kind.Lockbox,F,false,4), S(5,DecorationExpansion260Kind.ShopSignHanging,W,false,3) };
                case DecorationExpansion260SceneKind.GeneralStore:
                    return new[] { S(1,DecorationExpansion260Kind.GeneralStoreCounter,F,true,6), S(2,DecorationExpansion260Kind.SackDisplay,F,true,5), S(3,DecorationExpansion260Kind.ProduceBasketStand,F,false,4), S(4,DecorationExpansion260Kind.HerbDrawerCabinet,W,false,4), S(5,DecorationExpansion260Kind.AwningStriped,C,false,2) };
                case DecorationExpansion260SceneKind.ArmoryShop:
                    return new[] { S(1,DecorationExpansion260Kind.ArmorMerchantStand,F,true,6), S(2,DecorationExpansion260Kind.WeaponMerchantRack,W,true,5), S(3,DecorationExpansion260Kind.EnchantedWeaponStand,F,false,4), S(4,DecorationExpansion260Kind.EnchantedArmorStand,F,false,4), S(5,DecorationExpansion260Kind.StaffmakersRack,W,false,3) };
                case DecorationExpansion260SceneKind.EnchantersWorkshop:
                    return new[] { S(1,DecorationExpansion260Kind.EnchantersWorkbench,F,true,6), S(2,DecorationExpansion260Kind.RuneCarvingTable,F,true,5), S(3,DecorationExpansion260Kind.CrystalCabinet,F,true,4), S(4,DecorationExpansion260Kind.WandmakersBench,F,false,4), S(5,DecorationExpansion260Kind.SpellScrollCabinet,W,false,3), S(6,DecorationExpansion260Kind.ElementalBrazier,F,false,3), S(7,DecorationExpansion260Kind.ManaFont,F,false,2) };
                case DecorationExpansion260SceneKind.FamiliarRoom:
                    return new[] { S(1,DecorationExpansion260Kind.FamiliarPerch,F,true,6), S(2,DecorationExpansion260Kind.FamiliarNest,F,true,5), S(3,DecorationExpansion260Kind.EnchantedPlantStand,F,false,4), S(4,DecorationExpansion260Kind.FairyLanternCluster,C,false,4), S(5,DecorationExpansion260Kind.MagicMirror,W,false,3) };
                case DecorationExpansion260SceneKind.ArcaneGallery:
                    return new[] { S(1,DecorationExpansion260Kind.LevitationPedestal,F,true,6), S(2,DecorationExpansion260Kind.FloatingBookStand,F,true,5), S(3,DecorationExpansion260Kind.PortalKeystone,F,false,4), S(4,DecorationExpansion260Kind.WardTotem,F,false,4), S(5,DecorationExpansion260Kind.DivinationTable,F,false,3), S(6,DecorationExpansion260Kind.FairyLanternCluster,C,false,3) };
                case DecorationExpansion260SceneKind.NobleSalon:
                    return new[] { S(1,DecorationExpansion260Kind.Settee,F,true,6), S(2,DecorationExpansion260Kind.Chaise,F,true,5), S(3,DecorationExpansion260Kind.SideTable,F,false,4), S(4,DecorationExpansion260Kind.GrandMirror,W,false,4), S(5,DecorationExpansion260Kind.Candelabra,F,false,4), S(6,DecorationExpansion260Kind.WineCabinet,W,false,3) };
                case DecorationExpansion260SceneKind.MusicRoom:
                    return new[] { S(1,DecorationExpansion260Kind.Harpsichord,F,true,6), S(2,DecorationExpansion260Kind.Harp,F,true,5), S(3,DecorationExpansion260Kind.MusicStand,F,false,4), S(4,DecorationExpansion260Kind.LuteRack,W,false,4), S(5,DecorationExpansion260Kind.TrophyCase,W,false,2) };
                default:
                    return new[] { S(1,DecorationExpansion260Kind.Wardrobe,W,true,6), S(2,DecorationExpansion260Kind.WritingDesk,F,true,5), S(3,DecorationExpansion260Kind.WashBasinStand,F,false,4), S(4,DecorationExpansion260Kind.VanityTable,F,false,4), S(5,DecorationExpansion260Kind.FoldingScreen,W,false,3), S(6,DecorationExpansion260Kind.JewelryCasket,F,false,3), S(7,DecorationExpansion260Kind.PerfumeTray,F,false,2) };
            }
        }

        public static int OptionalBudget(DecorationExpansion260SceneKind kind, in DecorationContext context)
        {
            if (context.Condition == DecorationConditionTier.Ruined) return 1;
            int b = 2 + (int)context.Wealth / 2;
            if (kind == DecorationExpansion260SceneKind.EnchantersWorkshop ||
                kind == DecorationExpansion260SceneKind.ArcaneGallery ||
                kind == DecorationExpansion260SceneKind.NobleSalon || kind == DecorationExpansion260SceneKind.MusicRoom) b++;
            return b;
        }

        private const DecorationSocketKind F = DecorationSocketKind.Floor;
        private const DecorationSocketKind W = DecorationSocketKind.Wall;
        private const DecorationSocketKind C = DecorationSocketKind.Ceiling;
        private static DecorationExpansion260SceneSlot S(uint id, DecorationExpansion260Kind kind, DecorationSocketKind socket, bool required, ushort weight) =>
            new DecorationExpansion260SceneSlot { SlotId=id, Kind=kind, Socket=socket, Required=required, Weight=weight };
    }

    public static class DecorationExpansion260SceneResolver
    {
        public static bool TryResolve(DecorationExpansion260SceneKind kind, in DecorationSpace space,
            in DecorationContext context, DecorationExclusion[] exclusions, out DecorationPlacement[] placements)
        {
            placements = new DecorationPlacement[0];
            if (!space.IsWellFormed || !context.IsWellFormed || space.SpaceId != context.SpaceId || space.Kind != context.SpaceKind) return false;
            uint sceneId = DecorationExpansion260SceneCatalog.SceneId(kind);
            DecorationExpansion260SceneSlot[] slots = DecorationExpansion260SceneCatalog.Slots(kind);
            var core = new DecorationSceneSlot[slots.Length];
            for (int i=0;i<slots.Length;i++)
            {
                DecorationExpansion260Recipe r = DecorationExpansion260Catalog.Recipe(slots[i].Kind);
                if (!r.IsWellFormed || (r.Sockets & slots[i].Socket)==0) return false;
                core[i]=slots[i].Core(r.ProxyFamily);
            }
            if (!DecorationSceneScheduler.TrySelectAndOrder(in context, sceneId, core,
                DecorationExpansion260SceneCatalog.OptionalBudget(kind,in context), out DecorationSceneSlot[] ordered)) return false;
            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            var result = new DecorationPlacement[ordered.Length]; int count=0;
            for(int i=0;i<ordered.Length;i++)
            {
                DecorationExpansion260SceneSlot slot=Find(slots,ordered[i].SlotId);
                DecorationPropDescriptor d=DecorationExpansion260Catalog.Describe(in context,sceneId,slot.SlotId,slot.Kind);
                if(!d.IsWellFormed) return false;
                bool ok=DecorationPlacementResolver.TryPlace(in space,in context,sceneId,slot.SlotId,in d,sockets,exclusions,result,count,out DecorationPlacement p);
                if(!ok){ if(slot.Required)return false; continue; }
                result[count++]=p;
            }
            placements=new DecorationPlacement[count]; for(int i=0;i<count;i++) placements[i]=result[i]; return true;
        }
        private static DecorationExpansion260SceneSlot Find(DecorationExpansion260SceneSlot[] slots,uint id)
        { for(int i=0;i<slots.Length;i++) if(slots[i].SlotId==id)return slots[i]; return default; }
    }
}
