using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum DecorationShowcaseEntrySource : byte
    {
        RegisteredDecoration = 0,
        Preset = 1,
        MineCave = 2,
        NaturalCave = 3,
        WorldObject = 4,
    }

    /// <summary>
    /// Production-owned identities for reusable descriptor factories that are not otherwise enumerable.
    /// This is the canonical read boundary for browsers/tools; consumers must not duplicate this list.
    /// </summary>
    public enum DecorationShowcasePresetKind : ushort
    {
        RoomBed = 1,
        RoomBench = 2,
        RoomChair = 3,
        RoomWorkTable = 4,
        RoomWallTorch = 5,
        RoomPainting = 6,
        RoomAltar = 7,
        RoomThrone = 8,
        DiningTable = 9,
        DiningBench = 10,
        DiningChair = 11,
        LightingFireplace = 12,
        LightingCandle = 13,
        LightingChandelier = 14,
        LightingStandingLamp = 15,
        StorageCrate = 16,
        StorageBarrel = 17,
        StorageChest = 18,
        StorageShelf = 19,
        StorageBookcase = 20,
        MartialShieldDisplay = 21,
        MartialWeaponRack = 22,
        MartialArmorDisplay = 23,
        TextileBanner = 24,
        TextileCurtain = 25,
    }

    public readonly struct DecorationShowcaseEntry : IEquatable<DecorationShowcaseEntry>
    {
        public readonly string StableId;
        public readonly string DisplayName;
        public readonly string Category;
        public readonly DecorationShowcaseEntrySource Source;
        public readonly ushort SourceId;

        public DecorationShowcaseEntry(
            string stableId,
            string displayName,
            string category,
            DecorationShowcaseEntrySource source,
            ushort sourceId)
        {
            StableId = stableId ?? string.Empty;
            DisplayName = displayName ?? string.Empty;
            Category = category ?? string.Empty;
            Source = source;
            SourceId = sourceId;
        }

        public bool IsWellFormed =>
            !string.IsNullOrEmpty(StableId) &&
            !string.IsNullOrEmpty(DisplayName) &&
            !string.IsNullOrEmpty(Category) &&
            SourceId != 0;

        public bool Equals(DecorationShowcaseEntry other) =>
            string.Equals(StableId, other.StableId, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is DecorationShowcaseEntry other && Equals(other);
        public override int GetHashCode() => StableId == null ? 0 : StableId.GetHashCode();
        public override string ToString() => StableId + " " + DisplayName;
    }

    /// <summary>
    /// Deterministic production-owned enumeration/query boundary for independently previewable
    /// decoration and world-object content. Scene/UI consumers iterate this API rather than owning a
    /// second catalogue. Stable registered decoration IDs remain the persistence IDs owned by their
    /// existing catalogues.
    /// </summary>
    public static class DecorationShowcaseCatalog
    {
        public const int PresetCount = 25;
        public const int MineCaveCount = 8;
        public const int NaturalCaveCount = 8;
        public const int WorldObjectCount = 48;
        public const uint PreviewSceneId = 0x50525031u; // PRP1

        public static int RegisteredDecorationCount =>
            DecorationContentCatalog.KindCount +
            DecorationExpansion200Catalog.Count +
            DecorationExpansion260Catalog.Count +
            DecorationExpansion300Catalog.Count +
            DecorationExpansion320Catalog.Count +
            DecorationExpansion340Catalog.Count +
            DecorationExpansion360Catalog.Count +
            DecorationExpansion380Catalog.Count +
            DecorationExpansion400Catalog.Count +
            GuildSignatureDecorationCatalog.Count;

        public static int Count =>
            RegisteredDecorationCount + PresetCount + MineCaveCount + NaturalCaveCount + WorldObjectCount;

        public static DecorationShowcaseEntry[] CreateEntries()
        {
            var entries = new DecorationShowcaseEntry[Count];
            int output = 0;

            for (ushort id = 1; id <= RegisteredDecorationCount; id++)
            {
                entries[output++] = new DecorationShowcaseEntry(
                    "decoration:" + id,
                    FriendlyName(RegisteredDecorationName(id)),
                    RegisteredDecorationCategory(id),
                    DecorationShowcaseEntrySource.RegisteredDecoration,
                    id);
            }

            for (ushort id = 1; id <= PresetCount; id++)
            {
                var kind = (DecorationShowcasePresetKind)id;
                entries[output++] = new DecorationShowcaseEntry(
                    "preset:" + id,
                    FriendlyName(kind.ToString()),
                    PresetCategory(kind),
                    DecorationShowcaseEntrySource.Preset,
                    id);
            }

            for (ushort id = 1; id <= MineCaveCount; id++)
            {
                var kind = (MineCaveDecorationKind)(id - 1);
                entries[output++] = new DecorationShowcaseEntry(
                    "mine-cave:" + id,
                    FriendlyName(kind.ToString()),
                    "Cave / Mine",
                    DecorationShowcaseEntrySource.MineCave,
                    id);
            }

            for (ushort id = 1; id <= NaturalCaveCount; id++)
            {
                var kind = (NaturalCaveDecorationKind)(id - 1);
                entries[output++] = new DecorationShowcaseEntry(
                    "natural-cave:" + id,
                    FriendlyName(kind.ToString()),
                    "Cave / Natural",
                    DecorationShowcaseEntrySource.NaturalCave,
                    id);
            }

            for (ushort id = 1; id <= WorldObjectCount; id++)
            {
                var kind = (WorldObjectKind)id;
                entries[output++] = new DecorationShowcaseEntry(
                    "world-object:" + id,
                    FriendlyName(kind.ToString()),
                    "World Objects",
                    DecorationShowcaseEntrySource.WorldObject,
                    id);
            }

            if (output != entries.Length)
                throw new InvalidOperationException("Decoration showcase catalogue count drifted from its canonical sources.");
            return entries;
        }

        public static bool TryDescribeDecoration(
            in DecorationContext context,
            ushort stableId,
            out DecorationPropDescriptor descriptor)
        {
            descriptor = default;
            if (!context.IsWellFormed || stableId == 0 || stableId > RegisteredDecorationCount)
                return false;

            uint slotId = stableId;
            if (stableId <= 114)
                descriptor = DecorationContentCatalog.Describe(in context, PreviewSceneId, slotId, (DecorationContentKind)stableId);
            else if (stableId <= 200)
                descriptor = DecorationExpansion200Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpandedContentKind)stableId);
            else if (stableId <= 260)
                descriptor = DecorationExpansion260Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion260Kind)stableId);
            else if (stableId <= 300)
                descriptor = DecorationExpansion300Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion300Kind)stableId);
            else if (stableId <= 320)
                descriptor = DecorationExpansion320Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion320Kind)stableId);
            else if (stableId <= 340)
                descriptor = DecorationExpansion340Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion340Kind)stableId);
            else if (stableId <= 360)
                descriptor = DecorationExpansion360Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion360Kind)stableId);
            else if (stableId <= 380)
                descriptor = DecorationExpansion380Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion380Kind)stableId);
            else if (stableId <= 400)
                descriptor = DecorationExpansion400Catalog.Describe(in context, PreviewSceneId, slotId, (DecorationExpansion400Kind)stableId);
            else
                descriptor = GuildSignatureDecorationCatalog.Describe(in context, PreviewSceneId, slotId, (GuildSignatureKind)stableId);

            return descriptor.IsWellFormed;
        }

        public static bool TryDescribePreset(
            in DecorationContext context,
            DecorationShowcasePresetKind kind,
            out DecorationPropDescriptor descriptor)
        {
            descriptor = default;
            ushort raw = (ushort)kind;
            if (!context.IsWellFormed || raw == 0 || raw > PresetCount)
                return false;

            uint slotId = (uint)kind + 1000u;
            switch (kind)
            {
                case DecorationShowcasePresetKind.RoomBed:
                    descriptor = RoomScenePropPresets.Bed(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomBench:
                    descriptor = RoomScenePropPresets.Bench(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomChair:
                    descriptor = RoomScenePropPresets.Chair(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomWorkTable:
                    descriptor = RoomScenePropPresets.WorkTable(in context, PreviewSceneId, slotId, 24); break;
                case DecorationShowcasePresetKind.RoomWallTorch:
                    descriptor = RoomScenePropPresets.WallTorch(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomPainting:
                    descriptor = RoomScenePropPresets.Painting(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomAltar:
                    descriptor = RoomScenePropPresets.Altar(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.RoomThrone:
                    descriptor = RoomScenePropPresets.Throne(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.DiningTable:
                    descriptor = DiningPropPresets.Table(in context); break;
                case DecorationShowcasePresetKind.DiningBench:
                    descriptor = DiningPropPresets.Bench(in context, slotId); break;
                case DecorationShowcasePresetKind.DiningChair:
                    descriptor = DiningPropPresets.Chair(in context, slotId); break;
                case DecorationShowcasePresetKind.LightingFireplace:
                    descriptor = LightingPropPresets.Fireplace(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.LightingCandle:
                    descriptor = LightingPropPresets.Candle(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.LightingChandelier:
                    descriptor = LightingPropPresets.Chandelier(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.LightingStandingLamp:
                    descriptor = LightingPropPresets.StandingLamp(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.StorageCrate:
                    descriptor = StorageContainerPresets.Crate(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.StorageBarrel:
                    descriptor = StorageContainerPresets.Barrel(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.StorageChest:
                    descriptor = StorageFurniturePresets.Chest(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.StorageShelf:
                    descriptor = StorageFurniturePresets.Shelf(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.StorageBookcase:
                    descriptor = StorageFurniturePresets.Bookcase(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.MartialShieldDisplay:
                    descriptor = MartialDisplayPresets.ShieldDisplay(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.MartialWeaponRack:
                    descriptor = MartialDisplayPresets.WeaponRack(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.MartialArmorDisplay:
                    descriptor = MartialDisplayPresets.ArmorDisplay(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.TextileBanner:
                    descriptor = TextileDisplayPresets.Banner(in context, PreviewSceneId, slotId); break;
                case DecorationShowcasePresetKind.TextileCurtain:
                    descriptor = TextileDisplayPresets.Curtain(in context, PreviewSceneId, slotId); break;
                default:
                    return false;
            }

            return descriptor.IsWellFormed;
        }

        public static bool TryGetWorldObjectPreset(ushort stableId, out WorldObjectPreset preset)
        {
            preset = default;
            if (stableId == 0 || stableId > WorldObjectCount)
                return false;
            preset = WorldObjectContentCatalog.Get((WorldObjectKind)stableId);
            return preset.Kind != WorldObjectKind.Unknown;
        }

        private static string RegisteredDecorationName(ushort id)
        {
            if (id <= 114) return ((DecorationContentKind)id).ToString();
            if (id <= 200) return ((DecorationExpandedContentKind)id).ToString();
            if (id <= 260) return ((DecorationExpansion260Kind)id).ToString();
            if (id <= 300) return ((DecorationExpansion300Kind)id).ToString();
            if (id <= 320) return ((DecorationExpansion320Kind)id).ToString();
            if (id <= 340) return ((DecorationExpansion340Kind)id).ToString();
            if (id <= 360) return ((DecorationExpansion360Kind)id).ToString();
            if (id <= 380) return ((DecorationExpansion380Kind)id).ToString();
            if (id <= 400) return ((DecorationExpansion400Kind)id).ToString();
            return ((GuildSignatureKind)id).ToString();
        }

        private static string RegisteredDecorationCategory(ushort id)
        {
            if (id <= 114) return "Decorations / Core";
            if (id <= 200) return "Decorations / Expansion 200";
            if (id <= 260) return "Decorations / Expansion 260";
            if (id <= 300) return "Decorations / Expansion 300";
            if (id <= 320) return "Decorations / Expansion 320";
            if (id <= 340) return "Decorations / Expansion 340";
            if (id <= 360) return "Decorations / Expansion 360";
            if (id <= 380) return "Decorations / Expansion 380";
            if (id <= 400) return "Decorations / Expansion 400";
            return "Decorations / Guild Signature";
        }

        private static string PresetCategory(DecorationShowcasePresetKind kind)
        {
            ushort raw = (ushort)kind;
            if (raw <= (ushort)DecorationShowcasePresetKind.RoomThrone) return "Presets / Room";
            if (raw <= (ushort)DecorationShowcasePresetKind.DiningChair) return "Presets / Dining";
            if (raw <= (ushort)DecorationShowcasePresetKind.LightingStandingLamp) return "Presets / Lighting";
            if (raw <= (ushort)DecorationShowcasePresetKind.StorageBookcase) return "Presets / Storage";
            if (raw <= (ushort)DecorationShowcasePresetKind.MartialArmorDisplay) return "Presets / Martial";
            return "Presets / Textile";
        }

        private static string FriendlyName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "Unnamed";
            var chars = new char[value.Length * 2];
            int output = 0;
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]))
                    chars[output++] = ' ';
                chars[output++] = current;
            }
            return new string(chars, 0, output);
        }
    }
}
