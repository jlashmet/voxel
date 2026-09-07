using System;
using System.Collections.Generic;
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
    /// second catalogue. Canonical enum values remain the identity authority; this class only joins
    /// the owning production catalogues and reusable factories into one read surface.
    /// </summary>
    public static class DecorationShowcaseCatalog
    {
        public const uint PreviewSceneId = 0x50525031u; // PRP1

        public static int PresetCount => Enum.GetValues(typeof(DecorationShowcasePresetKind)).Length;
        public static int MineCaveCount => Enum.GetValues(typeof(MineCaveDecorationKind)).Length;
        public static int NaturalCaveCount => Enum.GetValues(typeof(NaturalCaveDecorationKind)).Length;
        public static int WorldObjectCount => CountWorldObjects();

        public static int RegisteredDecorationCount =>
            CountNonZero(typeof(DecorationContentKind)) +
            CountNonZero(typeof(DecorationExpandedContentKind)) +
            CountNonZero(typeof(DecorationExpansion260Kind)) +
            CountNonZero(typeof(DecorationExpansion300Kind)) +
            CountNonZero(typeof(DecorationExpansion320Kind)) +
            CountNonZero(typeof(DecorationExpansion340Kind)) +
            CountNonZero(typeof(DecorationExpansion360Kind)) +
            CountNonZero(typeof(DecorationExpansion380Kind)) +
            CountNonZero(typeof(DecorationExpansion400Kind)) +
            CountNonZero(typeof(GuildSignatureKind));

        public static int Count =>
            RegisteredDecorationCount + PresetCount + MineCaveCount + NaturalCaveCount + WorldObjectCount;

        public static DecorationShowcaseEntry[] CreateEntries()
        {
            var entries = new List<DecorationShowcaseEntry>(Count);
            var identities = new HashSet<string>(StringComparer.Ordinal);

            AppendRegistered(entries, identities, typeof(DecorationContentKind), "Decorations / Core");
            AppendRegistered(entries, identities, typeof(DecorationExpandedContentKind), "Decorations / Expansion 200");
            AppendRegistered(entries, identities, typeof(DecorationExpansion260Kind), "Decorations / Expansion 260");
            AppendRegistered(entries, identities, typeof(DecorationExpansion300Kind), "Decorations / Expansion 300");
            AppendRegistered(entries, identities, typeof(DecorationExpansion320Kind), "Decorations / Expansion 320");
            AppendRegistered(entries, identities, typeof(DecorationExpansion340Kind), "Decorations / Expansion 340");
            AppendRegistered(entries, identities, typeof(DecorationExpansion360Kind), "Decorations / Expansion 360");
            AppendRegistered(entries, identities, typeof(DecorationExpansion380Kind), "Decorations / Expansion 380");
            AppendRegistered(entries, identities, typeof(DecorationExpansion400Kind), "Decorations / Expansion 400");
            AppendRegistered(entries, identities, typeof(GuildSignatureKind), "Decorations / Guild Signature");

            Array presetValues = Enum.GetValues(typeof(DecorationShowcasePresetKind));
            for (int i = 0; i < presetValues.Length; i++)
            {
                var kind = (DecorationShowcasePresetKind)presetValues.GetValue(i);
                ushort raw = (ushort)kind;
                AddUnique(entries, identities, new DecorationShowcaseEntry(
                    "preset:" + raw,
                    FriendlyName(kind.ToString()),
                    PresetCategory(kind),
                    DecorationShowcaseEntrySource.Preset,
                    raw));
            }

            Array mineValues = Enum.GetValues(typeof(MineCaveDecorationKind));
            for (int i = 0; i < mineValues.Length; i++)
            {
                var kind = (MineCaveDecorationKind)mineValues.GetValue(i);
                ushort sourceId = (ushort)((byte)kind + 1);
                AddUnique(entries, identities, new DecorationShowcaseEntry(
                    "mine-cave:" + sourceId,
                    FriendlyName(kind.ToString()),
                    "Cave / Mine",
                    DecorationShowcaseEntrySource.MineCave,
                    sourceId));
            }

            Array naturalValues = Enum.GetValues(typeof(NaturalCaveDecorationKind));
            for (int i = 0; i < naturalValues.Length; i++)
            {
                var kind = (NaturalCaveDecorationKind)naturalValues.GetValue(i);
                ushort sourceId = (ushort)((byte)kind + 1);
                AddUnique(entries, identities, new DecorationShowcaseEntry(
                    "natural-cave:" + sourceId,
                    FriendlyName(kind.ToString()),
                    "Cave / Natural",
                    DecorationShowcaseEntrySource.NaturalCave,
                    sourceId));
            }

            Array worldObjectValues = Enum.GetValues(typeof(WorldObjectKind));
            for (int i = 0; i < worldObjectValues.Length; i++)
            {
                var kind = (WorldObjectKind)worldObjectValues.GetValue(i);
                if (kind == WorldObjectKind.Unknown)
                    continue;
                WorldObjectPreset preset = WorldObjectContentCatalog.Get(kind);
                if (preset.Kind == WorldObjectKind.Unknown)
                    continue;
                ushort raw = Convert.ToUInt16(kind);
                AddUnique(entries, identities, new DecorationShowcaseEntry(
                    "world-object:" + raw,
                    FriendlyName(kind.ToString()),
                    "World Objects",
                    DecorationShowcaseEntrySource.WorldObject,
                    raw));
            }

            if (entries.Count != Count)
                throw new InvalidOperationException("Decoration showcase catalogue count drifted from its canonical sources.");
            return entries.ToArray();
        }

        public static bool TryDescribeDecoration(
            in DecorationContext context,
            ushort stableId,
            out DecorationPropDescriptor descriptor)
        {
            descriptor = default;
            if (!context.IsWellFormed || stableId == 0)
                return false;

            if (IsDefined(typeof(DecorationContentKind), stableId))
                descriptor = DecorationContentCatalog.Describe(in context, PreviewSceneId, stableId, (DecorationContentKind)stableId);
            else if (IsDefined(typeof(DecorationExpandedContentKind), stableId))
                descriptor = DecorationExpansion200Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpandedContentKind)stableId);
            else if (IsDefined(typeof(DecorationExpansion260Kind), stableId))
                descriptor = DecorationExpansion260Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion260Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion300Kind), stableId))
                descriptor = DecorationExpansion300Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion300Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion320Kind), stableId))
                descriptor = DecorationExpansion320Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion320Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion340Kind), stableId))
                descriptor = DecorationExpansion340Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion340Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion360Kind), stableId))
                descriptor = DecorationExpansion360Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion360Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion380Kind), stableId))
                descriptor = DecorationExpansion380Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion380Kind)stableId);
            else if (IsDefined(typeof(DecorationExpansion400Kind), stableId))
                descriptor = DecorationExpansion400Catalog.Describe(in context, PreviewSceneId, stableId, (DecorationExpansion400Kind)stableId);
            else if (IsDefined(typeof(GuildSignatureKind), stableId))
                descriptor = GuildSignatureDecorationCatalog.Describe(in context, PreviewSceneId, stableId, (GuildSignatureKind)stableId);
            else
                return false;

            return descriptor.IsWellFormed;
        }

        public static bool TryDescribePreset(
            in DecorationContext context,
            DecorationShowcasePresetKind kind,
            out DecorationPropDescriptor descriptor)
        {
            descriptor = default;
            if (!context.IsWellFormed || !Enum.IsDefined(typeof(DecorationShowcasePresetKind), kind))
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

        public static bool TryDescribeMineCave(
            in DecorationContext context,
            ushort sourceId,
            out MineCaveDecorationDescriptor descriptor)
        {
            descriptor = default;
            if (!context.IsWellFormed || sourceId == 0)
                return false;
            int raw = sourceId - 1;
            if (!Enum.IsDefined(typeof(MineCaveDecorationKind), (byte)raw))
                return false;
            descriptor = MineCaveDecorationCatalog.Describe(in context, (MineCaveDecorationKind)raw, sourceId);
            return descriptor.IsWellFormed;
        }

        public static bool TryDescribeNaturalCave(
            in DecorationContext context,
            ushort sourceId,
            out NaturalCaveDecorationDescriptor descriptor)
        {
            descriptor = default;
            if (!context.IsWellFormed || sourceId == 0)
                return false;
            int raw = sourceId - 1;
            if (!Enum.IsDefined(typeof(NaturalCaveDecorationKind), (byte)raw))
                return false;
            descriptor = NaturalCaveDecorationCatalog.Describe(in context, (NaturalCaveDecorationKind)raw, sourceId);
            return descriptor.IsWellFormed;
        }

        public static bool TryGetWorldObjectPreset(ushort stableId, out WorldObjectPreset preset)
        {
            preset = default;
            var kind = (WorldObjectKind)stableId;
            if (stableId == 0 || !Enum.IsDefined(typeof(WorldObjectKind), kind))
                return false;
            preset = WorldObjectContentCatalog.Get(kind);
            return preset.Kind != WorldObjectKind.Unknown;
        }

        private static void AppendRegistered(
            List<DecorationShowcaseEntry> entries,
            HashSet<string> identities,
            Type enumType,
            string category)
        {
            Array values = Enum.GetValues(enumType);
            for (int i = 0; i < values.Length; i++)
            {
                object value = values.GetValue(i);
                ushort raw = Convert.ToUInt16(value);
                if (raw == 0)
                    continue;
                string name = Enum.GetName(enumType, value) ?? value.ToString();
                AddUnique(entries, identities, new DecorationShowcaseEntry(
                    "decoration:" + raw,
                    FriendlyName(name),
                    category,
                    DecorationShowcaseEntrySource.RegisteredDecoration,
                    raw));
            }
        }

        private static void AddUnique(
            List<DecorationShowcaseEntry> entries,
            HashSet<string> identities,
            in DecorationShowcaseEntry entry)
        {
            if (!entry.IsWellFormed)
                throw new InvalidOperationException("Canonical showcase entry was malformed: " + entry.StableId);
            if (!identities.Add(entry.StableId))
                throw new InvalidOperationException("Duplicate canonical showcase identity: " + entry.StableId);
            entries.Add(entry);
        }

        private static int CountNonZero(Type enumType)
        {
            Array values = Enum.GetValues(enumType);
            int count = 0;
            for (int i = 0; i < values.Length; i++)
                if (Convert.ToUInt64(values.GetValue(i)) != 0UL)
                    count++;
            return count;
        }

        private static int CountWorldObjects()
        {
            Array values = Enum.GetValues(typeof(WorldObjectKind));
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                var kind = (WorldObjectKind)values.GetValue(i);
                if (kind != WorldObjectKind.Unknown && WorldObjectContentCatalog.Get(kind).Kind != WorldObjectKind.Unknown)
                    count++;
            }
            return count;
        }

        private static bool IsDefined(Type enumType, ushort value) =>
            Enum.IsDefined(enumType, Enum.ToObject(enumType, value));

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
