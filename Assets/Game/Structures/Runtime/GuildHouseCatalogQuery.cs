using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    public readonly struct GuildHouseDescriptor
    {
        public readonly GuildHouseKind Kind;
        public readonly string Key;
        public readonly string DisplayName;
        public readonly GuildHouseTrait Traits;
        public readonly byte MinimumRooms;
        public readonly byte PreferredRooms;

        public GuildHouseDescriptor(
            GuildHouseKind kind,
            string key,
            string displayName,
            GuildHouseTrait traits,
            byte minimumRooms,
            byte preferredRooms)
        {
            Kind = kind;
            Key = key;
            DisplayName = displayName;
            Traits = traits;
            MinimumRooms = minimumRooms;
            PreferredRooms = preferredRooms;
        }

        public bool IsWellFormed =>
            Kind != default &&
            !string.IsNullOrEmpty(Key) &&
            !string.IsNullOrEmpty(DisplayName) &&
            MinimumRooms > 0 &&
            PreferredRooms >= MinimumRooms;
    }

    public readonly struct DecorationCanonicalDescriptor
    {
        public readonly ushort StableId;
        public readonly string DisplayName;
        public readonly DecorationPropFamily Family;
        public readonly DecorationSocketKind AcceptedSockets;
        public readonly DecorationMountMode MountMode;
        public readonly DecorationRenderBackend Backend;
        public readonly DecorationInteractionFlags Interaction;
        public readonly int3 Size;
        public readonly int3 Clearance;

        public DecorationCanonicalDescriptor(
            ushort stableId,
            string displayName,
            DecorationPropFamily family,
            DecorationSocketKind acceptedSockets,
            DecorationMountMode mountMode,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            int3 clearance)
        {
            StableId = stableId;
            DisplayName = displayName;
            Family = family;
            AcceptedSockets = acceptedSockets;
            MountMode = mountMode;
            Backend = backend;
            Interaction = interaction;
            Size = size;
            Clearance = clearance;
        }

        public DecorationPropDescriptor ToPropDescriptor() => new DecorationPropDescriptor
        {
            Family = Family,
            AcceptedSockets = AcceptedSockets,
            MountMode = MountMode,
            Backend = Backend,
            Interaction = Interaction,
            Size = Size,
            Clearance = Clearance,
            Variant = StableId,
        };

        public bool IsWellFormed =>
            StableId != 0 &&
            !string.IsNullOrEmpty(DisplayName) &&
            DecorationValidation.IsWellFormed(ToPropDescriptor());
    }

    public readonly struct GuildHouseFurnishingOption
    {
        public readonly DecorationCanonicalDescriptor Decoration;
        public readonly bool RequiredFixture;

        public GuildHouseFurnishingOption(
            DecorationCanonicalDescriptor decoration,
            bool requiredFixture)
        {
            Decoration = decoration;
            RequiredFixture = requiredFixture;
        }

        public bool Selectable => !RequiredFixture;
    }

    /// <summary>
    /// Read-only semantic query boundary for production guild houses and their canonical furnishing
    /// identities. HouseShowcase and other browsers consume this surface rather than owning parallel
    /// house or decoration catalogs.
    /// </summary>
    public static class GuildHouseCatalogQuery
    {
        private readonly struct Registration
        {
            public readonly GuildHouseKind Kind;
            public readonly string Key;
            public readonly string DisplayName;

            public Registration(GuildHouseKind kind, string key, string displayName)
            {
                Kind = kind;
                Key = key;
                DisplayName = displayName;
            }
        }

        private static readonly Registration[] Registrations =
        {
            new Registration(GuildHouseKind.Adventurers, "adventurers", "Adventurers' Guild"),
            new Registration(GuildHouseKind.Wizards, "wizards", "Wizards' Guild"),
            new Registration(GuildHouseKind.Knights, "knights", "Knights' Guild"),
            new Registration(GuildHouseKind.Assassins, "assassins", "Assassins' Guild"),
            new Registration(GuildHouseKind.Druids, "druids", "Druids' Guild"),
            new Registration(GuildHouseKind.Thieves, "thieves", "Thieves' Guild"),
            new Registration(GuildHouseKind.Clerics, "clerics", "Clerics' Guild"),
            new Registration(GuildHouseKind.Rangers, "rangers", "Rangers' Guild"),
            new Registration(GuildHouseKind.Bards, "bards", "Bards' Guild"),
            new Registration(GuildHouseKind.Alchemists, "alchemists", "Alchemists' Guild"),
        };

        public static GuildHouseDescriptor[] Houses()
        {
            var result = new GuildHouseDescriptor[Registrations.Length];
            for (int i = 0; i < Registrations.Length; i++)
            {
                Registration registration = Registrations[i];
                GuildHouseProgram program = GuildHouseProgramCatalog.Get(registration.Kind);
                result[i] = new GuildHouseDescriptor(
                    registration.Kind,
                    registration.Key,
                    registration.DisplayName,
                    program.Traits,
                    program.MinimumRooms,
                    program.PreferredRooms);
            }

            return result;
        }

        public static bool TryGetHouse(GuildHouseKind kind, out GuildHouseDescriptor descriptor)
        {
            for (int i = 0; i < Registrations.Length; i++)
            {
                Registration registration = Registrations[i];
                if (registration.Kind != kind)
                    continue;

                GuildHouseProgram program = GuildHouseProgramCatalog.Get(kind);
                descriptor = new GuildHouseDescriptor(
                    kind,
                    registration.Key,
                    registration.DisplayName,
                    program.Traits,
                    program.MinimumRooms,
                    program.PreferredRooms);
                return descriptor.IsWellFormed;
            }

            descriptor = default;
            return false;
        }

        public static bool TryGetFurnishings(
            GuildHouseKind kind,
            out GuildHouseFurnishingOption[] furnishings)
        {
            furnishings = Array.Empty<GuildHouseFurnishingOption>();
            if (!TryGetHouse(kind, out _))
                return false;

            GuildHouseProgram program = GuildHouseProgramCatalog.Get(kind);
            var orderedIds = new List<ushort>(64);
            var requiredByHouse = new Dictionary<ushort, bool>();

            for (int roomIndex = 0; roomIndex < program.Rooms.Length; roomIndex++)
            {
                GuildHouseRoomProgram room = program.Rooms[roomIndex];
                if (!Collect(
                        room.RequiredArchetypes,
                        room.Required,
                        orderedIds,
                        requiredByHouse))
                    return false;
                if (!Collect(
                        room.OptionalArchetypes,
                        false,
                        orderedIds,
                        requiredByHouse))
                    return false;
            }

            var result = new GuildHouseFurnishingOption[orderedIds.Count];
            for (int i = 0; i < orderedIds.Count; i++)
            {
                ushort stableId = orderedIds[i];
                if (!DecorationCanonicalCatalog.TryGet(stableId, out DecorationCanonicalDescriptor canonical))
                    return false;
                result[i] = new GuildHouseFurnishingOption(canonical, requiredByHouse[stableId]);
            }

            furnishings = result;
            return true;
        }

        private static bool Collect(
            ushort[] ids,
            bool requiredFixture,
            List<ushort> orderedIds,
            Dictionary<ushort, bool> requiredByHouse)
        {
            if (ids == null)
                return false;

            for (int i = 0; i < ids.Length; i++)
            {
                ushort stableId = ids[i];
                if (!DecorationCanonicalCatalog.TryGet(stableId, out _))
                    return false;

                if (requiredByHouse.TryGetValue(stableId, out bool alreadyRequired))
                {
                    requiredByHouse[stableId] = alreadyRequired || requiredFixture;
                    continue;
                }

                requiredByHouse.Add(stableId, requiredFixture);
                orderedIds.Add(stableId);
            }

            return true;
        }
    }

    /// <summary>
    /// Normalizes the existing stable decoration catalogs into one semantic read-only descriptor.
    /// It does not own decoration identity or geometry; each range delegates to its production recipe.
    /// </summary>
    public static class DecorationCanonicalCatalog
    {
        public static bool TryGet(ushort stableId, out DecorationCanonicalDescriptor descriptor)
        {
            descriptor = default;
            if (stableId >= 1 && stableId <= 114)
            {
                DecorationContentKind kind = (DecorationContentKind)stableId;
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.AcceptedSockets,
                    recipe.MountMode, recipe.Backend, recipe.Interaction, recipe.BaseSize, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 115 && stableId <= 200)
            {
                DecorationExpandedContentKind kind = (DecorationExpandedContentKind)stableId;
                DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.AcceptedSockets,
                    recipe.MountMode, recipe.Backend, recipe.Interaction, recipe.BaseSize, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 201 && stableId <= 260)
            {
                DecorationExpansion260Kind kind = (DecorationExpansion260Kind)stableId;
                DecorationExpansion260Recipe recipe = DecorationExpansion260Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 261 && stableId <= 300)
            {
                DecorationExpansion300Kind kind = (DecorationExpansion300Kind)stableId;
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 301 && stableId <= 320)
            {
                DecorationExpansion320Kind kind = (DecorationExpansion320Kind)stableId;
                DecorationExpansion320Recipe recipe = DecorationExpansion320Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 321 && stableId <= 340)
            {
                DecorationExpansion340Kind kind = (DecorationExpansion340Kind)stableId;
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 341 && stableId <= 360)
            {
                DecorationExpansion360Kind kind = (DecorationExpansion360Kind)stableId;
                DecorationExpansion360Recipe recipe = DecorationExpansion360Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 361 && stableId <= 380)
            {
                DecorationExpansion380Kind kind = (DecorationExpansion380Kind)stableId;
                DecorationExpansion380Recipe recipe = DecorationExpansion380Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            if (stableId >= 381 && stableId <= 400)
            {
                DecorationExpansion400Kind kind = (DecorationExpansion400Kind)stableId;
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe(kind);
                if (!recipe.IsWellFormed)
                    return false;
                descriptor = Create(
                    stableId, kind.ToString(), recipe.ProxyFamily, recipe.Sockets,
                    recipe.Mount, recipe.Backend, recipe.Interaction, recipe.Size, recipe.Clearance);
                return descriptor.IsWellFormed;
            }

            return false;
        }

        private static DecorationCanonicalDescriptor Create(
            ushort stableId,
            string displayName,
            DecorationPropFamily family,
            DecorationSocketKind sockets,
            DecorationMountMode mount,
            DecorationRenderBackend backend,
            DecorationInteractionFlags interaction,
            int3 size,
            int3 clearance) =>
            new DecorationCanonicalDescriptor(
                stableId,
                displayName,
                family,
                sockets,
                mount,
                backend,
                interaction,
                size,
                clearance);
    }
}
