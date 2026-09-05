using System;
using Game.Characters.Api;
using Game.Composition.Kentridge.Playable;
using Game.Encounters.Api;
using UnityEngine;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Composition-only persistence adaptation for the production forest encounter. The concrete
    /// runtime already implements IEncounterRegistry; this helper keeps capture/restore on that
    /// public owner contract instead of adding scene-private mutation hooks.
    /// </summary>
    internal static class KentridgeForestPersistenceExtensions
    {
        private static readonly EncounterId ForestEncounterId =
            new EncounterId("kentridge-forest-bandits");

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

            EncounterSnapshot forestState = null;
            for (int i = 0; i < state.Encounters.Count; i++)
            {
                EncounterSnapshot candidate = state.Encounters[i];
                if (candidate == null || candidate.Id != ForestEncounterId) continue;
                forestState = candidate;
                break;
            }
            if (forestState == null) return EncounterMutationFailure.InvalidSnapshot;

            // The production persistence bridge intentionally captures after the representative
            // combat has settled. Combat has no public mid-battle restore contract, so claiming an
            // Active/Resolving/merely-Resolved snapshot would leave a fabricated combat runtime.
            if (forestState.Lifecycle == EncounterLifecycleState.Active ||
                forestState.Lifecycle == EncounterLifecycleState.Resolving ||
                forestState.Lifecycle == EncounterLifecycleState.Resolved)
                return EncounterMutationFailure.InvalidSnapshot;

            EncounterMutationFailure restored = RequireRegistry(forest).Restore(state);
            if (restored != EncounterMutationFailure.None ||
                forestState.Lifecycle != EncounterLifecycleState.Cleaned)
                return restored;

            // A fresh graph has already realized its authored encounter-owned bandits. Restoring a
            // Cleaned encounter does not replay historical cleanup facts, so reconcile the current
            // production presentation/character authority to the saved current state explicitly.
            KentridgePlayableSlice slice = forest.GetComponent<KentridgePlayableSlice>();
            KentridgeCharacterHost host = slice?.CharacterHost;
            if (host == null) return EncounterMutationFailure.InvalidSnapshot;

            for (int i = 0; i < 3; i++)
            {
                CharacterId id = CharacterId.FromStableKey(
                    "enemy",
                    "kentridge-forest-bandit-" + (i + 1));
                CharacterRegistryFailure removed = host.Characters.Remove(id);
                if (removed != CharacterRegistryFailure.None &&
                    removed != CharacterRegistryFailure.UnknownCharacterId)
                    return EncounterMutationFailure.InvalidSnapshot;

                if (i < forest.Bandits.Count && forest.Bandits[i] != null)
                    UnityEngine.Object.Destroy(forest.Bandits[i]);
            }

            return EncounterMutationFailure.None;
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
