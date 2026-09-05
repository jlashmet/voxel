using System;
using Game.Composition.Kentridge.Playable;
using Game.Encounters.Api;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Composition-only persistence adaptation for the production forest encounter. The concrete
    /// runtime already implements IEncounterRegistry; this helper keeps capture/restore on that
    /// public owner contract instead of adding scene-private mutation hooks.
    /// </summary>
    internal static class KentridgeForestPersistenceExtensions
    {
        public static EncounterRegistrySnapshot CaptureEncounterState(
            this KentridgeForestBanditEncounter forest)
        {
            return RequireRegistry(forest).Capture();
        }

        public static EncounterMutationFailure RestoreEncounterState(
            this KentridgeForestBanditEncounter forest,
            EncounterRegistrySnapshot state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            return RequireRegistry(forest).Restore(state);
        }

        private static IEncounterRegistry RequireRegistry(KentridgeForestBanditEncounter forest)
        {
            if (forest == null) throw new ArgumentNullException(nameof(forest));
            return forest.EncounterQuery as IEncounterRegistry
                   ?? throw new InvalidOperationException(
                       "Kentridge forest encounter registry is unavailable outside an active composed session.");
        }
    }
}
