using System;
using System.Collections.Generic;
using Game.Structures.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    public interface IArchitectureProvider
    {
        ArchitectureProviderDescriptor Descriptor { get; }

        bool TryPlan(
            in ArchitectureGenerationRequest request,
            out ArchitectureGenerationResult result,
            out string error);

        bool TryAuthor(
            IStructureAuthoringSession authoring,
            in ArchitectureGenerationRequest request,
            out ArchitectureGenerationResult result,
            out string error);
    }

    /// <summary>
    /// Production registry for semantic architecture profiles and providers. Scene composition is a
    /// consumer of this registry; registration remains in production modules and adapters.
    /// </summary>
    public static class ArchitectureRegistry
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, ArchitectureStyleProfileDescriptor> Profiles =
            new Dictionary<string, ArchitectureStyleProfileDescriptor>(StringComparer.Ordinal);
        private static readonly Dictionary<string, DecorationRegionTheme> LegacyRegions =
            new Dictionary<string, DecorationRegionTheme>(StringComparer.Ordinal);
        private static readonly Dictionary<string, IArchitectureProvider> Providers =
            new Dictionary<string, IArchitectureProvider>(StringComparer.Ordinal);
        private static bool _defaultsInstalled;

        public static ArchitectureStyleProfileDescriptor[] StyleProfiles()
        {
            EnsureDefaults();
            lock (Gate)
            {
                var result = new ArchitectureStyleProfileDescriptor[Profiles.Count];
                int i = 0;
                foreach (ArchitectureStyleProfileDescriptor descriptor in Profiles.Values)
                    result[i++] = descriptor;
                Array.Sort(result, (a, b) => string.CompareOrdinal(a.Key, b.Key));
                return result;
            }
        }

        public static ArchitectureProviderDescriptor[] ProviderDescriptors()
        {
            EnsureDefaults();
            lock (Gate)
            {
                var result = new ArchitectureProviderDescriptor[Providers.Count];
                int i = 0;
                foreach (IArchitectureProvider provider in Providers.Values)
                    result[i++] = provider.Descriptor;
                Array.Sort(result, (a, b) => string.CompareOrdinal(a.Key, b.Key));
                return result;
            }
        }

        public static bool TryGetStyleProfile(
            string key,
            out ArchitectureStyleProfileDescriptor descriptor)
        {
            EnsureDefaults();
            if (string.IsNullOrEmpty(key))
            {
                descriptor = default;
                return false;
            }

            lock (Gate)
                return Profiles.TryGetValue(key, out descriptor);
        }

        public static bool TryGetProvider(string key, out IArchitectureProvider provider)
        {
            EnsureDefaults();
            if (string.IsNullOrEmpty(key))
            {
                provider = null;
                return false;
            }

            lock (Gate)
                return Providers.TryGetValue(key, out provider);
        }

        public static bool TryRegisterStyleProfile(ArchitectureStyleProfileDescriptor descriptor)
        {
            if (!descriptor.IsWellFormed)
                return false;
            EnsureDefaults();
            lock (Gate)
            {
                if (Profiles.ContainsKey(descriptor.Key))
                    return false;
                Profiles.Add(descriptor.Key, descriptor);
                return true;
            }
        }

        public static bool TryRegisterProvider(IArchitectureProvider provider)
        {
            if (provider == null || !provider.Descriptor.IsWellFormed)
                return false;
            EnsureDefaults();
            lock (Gate)
            {
                if (Providers.ContainsKey(provider.Descriptor.Key))
                    return false;
                Providers.Add(provider.Descriptor.Key, provider);
                return true;
            }
        }

        internal static bool TryResolveLegacyRegion(
            string styleProfileKey,
            out DecorationRegionTheme region)
        {
            EnsureDefaults();
            lock (Gate)
                return LegacyRegions.TryGetValue(styleProfileKey, out region);
        }

        private static void EnsureDefaults()
        {
            if (_defaultsInstalled)
                return;

            lock (Gate)
            {
                if (_defaultsInstalled)
                    return;

                RegisterLegacyProfile("kentridge", "Kentridge", DecorationRegionTheme.Kentridge);
                RegisterLegacyProfile("hightown", "Hightown", DecorationRegionTheme.Hightown);
                RegisterLegacyProfile("moordell", "Moordell", DecorationRegionTheme.Moordell);
                RegisterLegacyProfile("rossdam", "Rossdam", DecorationRegionTheme.Rossdam);
                RegisterLegacyProfile("fairy-village", "Fairy Village", DecorationRegionTheme.FairyVillage);
                RegisterLegacyProfile("orc-village", "Orc Village", DecorationRegionTheme.OrcVillage);

                var guildProvider = new GuildHouseArchitectureProvider();
                Providers.Add(guildProvider.Descriptor.Key, guildProvider);
                _defaultsInstalled = true;
            }
        }

        private static void RegisterLegacyProfile(
            string key,
            string displayName,
            DecorationRegionTheme region)
        {
            DecorationRegionProfile profile = DecorationRegionProfiles.Resolve(region);
            if (!profile.IsWellFormed)
                throw new InvalidOperationException($"Architecture profile '{key}' has no valid production region profile.");

            Profiles.Add(
                key,
                new ArchitectureStyleProfileDescriptor(key, displayName, profile.StyleFamily));
            LegacyRegions.Add(key, region);
        }
    }
}
