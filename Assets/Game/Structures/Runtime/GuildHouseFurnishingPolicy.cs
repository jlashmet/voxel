using System;
using Game.Structures.Api;

namespace Game.Structures.Runtime
{
    public enum GuildHouseUnplacedReason : byte
    {
        None = 0,
        RoomUnavailable = 1,
        NoValidPlacement = 2,
    }

    public readonly struct GuildHouseUnplacedFurnishing
    {
        public readonly ushort StableId;
        public readonly GuildHouseUnplacedReason Reason;

        public GuildHouseUnplacedFurnishing(ushort stableId, GuildHouseUnplacedReason reason)
        {
            StableId = stableId;
            Reason = reason;
        }

        public bool IsWellFormed => StableId != 0 && Reason != GuildHouseUnplacedReason.None;
    }

    /// <summary>
    /// Canonical optional-furnishing selection for one production guild-house kind. A default value
    /// means "unspecified" and preserves the legacy room-scene behavior. A specified empty palette
    /// explicitly requests no optional furnishings while keeping generated-room required fixtures.
    /// </summary>
    public readonly struct GuildHouseFurnishingPalette
    {
        private readonly bool _specified;
        private readonly GuildHouseKind _kind;
        private readonly ushort[] _selectedOptionalArchetypes;

        private GuildHouseFurnishingPalette(
            GuildHouseKind kind,
            ushort[] selectedOptionalArchetypes)
        {
            _specified = true;
            _kind = kind;
            _selectedOptionalArchetypes = selectedOptionalArchetypes ?? Array.Empty<ushort>();
        }

        public bool IsSpecified => _specified;
        public GuildHouseKind Kind => _kind;
        public ushort[] SelectedOptionalArchetypes =>
            _selectedOptionalArchetypes == null
                ? Array.Empty<ushort>()
                : (ushort[])_selectedOptionalArchetypes.Clone();

        public bool Contains(ushort stableId)
        {
            if (!_specified || _selectedOptionalArchetypes == null)
                return false;
            for (int i = 0; i < _selectedOptionalArchetypes.Length; i++)
                if (_selectedOptionalArchetypes[i] == stableId)
                    return true;
            return false;
        }

        /// <summary>
        /// Validates user selection against the production query surface and canonicalizes it in
        /// house-program order so input click/order never changes semantic placement.
        /// </summary>
        public static bool TryCreate(
            GuildHouseKind kind,
            ushort[] selectedOptionalArchetypes,
            out GuildHouseFurnishingPalette palette)
        {
            palette = default;
            if (selectedOptionalArchetypes == null ||
                !GuildHouseCatalogQuery.TryGetFurnishings(kind, out GuildHouseFurnishingOption[] options))
                return false;

            for (int i = 0; i < selectedOptionalArchetypes.Length; i++)
            {
                ushort stableId = selectedOptionalArchetypes[i];
                bool selectable = false;
                for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
                {
                    GuildHouseFurnishingOption option = options[optionIndex];
                    if (option.Decoration.StableId == stableId && option.Selectable)
                    {
                        selectable = true;
                        break;
                    }
                }

                if (!selectable)
                    return false;

                for (int previous = 0; previous < i; previous++)
                    if (selectedOptionalArchetypes[previous] == stableId)
                        return false;
            }

            var canonical = new ushort[selectedOptionalArchetypes.Length];
            int output = 0;
            for (int optionIndex = 0; optionIndex < options.Length; optionIndex++)
            {
                GuildHouseFurnishingOption option = options[optionIndex];
                if (!option.Selectable)
                    continue;

                for (int selectedIndex = 0; selectedIndex < selectedOptionalArchetypes.Length; selectedIndex++)
                {
                    if (selectedOptionalArchetypes[selectedIndex] != option.Decoration.StableId)
                        continue;
                    canonical[output++] = option.Decoration.StableId;
                    break;
                }
            }

            if (output != canonical.Length)
                return false;

            palette = new GuildHouseFurnishingPalette(kind, canonical);
            return true;
        }
    }
}
