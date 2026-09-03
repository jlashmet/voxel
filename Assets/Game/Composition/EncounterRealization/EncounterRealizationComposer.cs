using System;
using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.EncounterRealization
{
    public enum EncounterRealizationFailure
    {
        None = 0,
        MissingSiteRealization = 1,
        MissingCharacterRealization = 2,
        DuplicateCharacter = 3,
        MissingSpawnRealization = 4
    }

    /// <summary>
    /// Semantic encounter-local slot whose exact position is owned by world/campaign realization.
    /// The value names intent only; the shared bridge never derives coordinates from it.
    /// </summary>
    public readonly struct EncounterSpawnPointRef : IEquatable<EncounterSpawnPointRef>
    {
        public string Value { get; }

        public EncounterSpawnPointRef(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Encounter spawn-point id is required.", nameof(value));
            Value = value;
        }

        public bool Equals(EncounterSpawnPointRef other) =>
            string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is EncounterSpawnPointRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EncounterSpawnPointRef left, EncounterSpawnPointRef right) => left.Equals(right);
        public static bool operator !=(EncounterSpawnPointRef left, EncounterSpawnPointRef right) => !left.Equals(right);
    }

    public readonly struct EncounterCharacterIntent
    {
        public CharacterId CharacterId { get; }
        public EncounterParticipantOwnership Ownership { get; }
        public string Role { get; }
        public NpcRef Npc { get; }
        public EncounterSpawnPointRef SpawnPoint { get; }
        public bool UsesNpcPlacement { get; }
        public bool UsesSpawnPlacement { get; }

        public EncounterCharacterIntent(
            CharacterId characterId,
            EncounterParticipantOwnership ownership,
            string role)
        {
            if (!characterId.IsValid) throw new ArgumentException("Character id is required.", nameof(characterId));
            if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Encounter role is required.", nameof(role));
            CharacterId = characterId;
            Ownership = ownership;
            Role = role;
            Npc = default;
            SpawnPoint = default;
            UsesNpcPlacement = false;
            UsesSpawnPlacement = false;
        }

        public EncounterCharacterIntent(
            CharacterId characterId,
            EncounterParticipantOwnership ownership,
            string role,
            NpcRef npc)
            : this(characterId, ownership, role)
        {
            Npc = npc;
            UsesNpcPlacement = true;
        }

        public EncounterCharacterIntent(
            CharacterId characterId,
            EncounterParticipantOwnership ownership,
            string role,
            EncounterSpawnPointRef spawnPoint)
            : this(characterId, ownership, role)
        {
            SpawnPoint = spawnPoint;
            UsesSpawnPlacement = true;
        }
    }

    public sealed class EncounterRealizationSpec
    {
        public EncounterDefinition Definition { get; }
        public SiteRef SiteRole { get; }
        public ResolvedSiteId Site { get; }
        public IReadOnlyList<EncounterCharacterIntent> Characters { get; }

        public EncounterRealizationSpec(
            EncounterDefinition definition,
            SiteRef siteRole,
            ResolvedSiteId site,
            IReadOnlyList<EncounterCharacterIntent> characters)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            SiteRole = siteRole;
            Site = site;
            if (characters == null) throw new ArgumentNullException(nameof(characters));
            var copy = new EncounterCharacterIntent[characters.Count];
            for (var i = 0; i < characters.Count; i++) copy[i] = characters[i];
            Characters = Array.AsReadOnly(copy);
        }
    }

    /// <summary>
    /// Exact post-generation positions supplied by the world realization owner. Implementations live in
    /// backend/campaign composition and adapt the already-generated placement; the shared bridge never
    /// derives coordinates from names, archetypes, seeds, terrain, or scene objects.
    /// </summary>
    public interface IEncounterRealizationFacts
    {
        bool TryGetSiteAnchor(ResolvedSiteId site, out CharacterVector3 position);
        bool TryGetNpcAnchor(NpcRef npc, ResolvedSiteId site, out CharacterVector3 position);
        bool TryGetSpawnAnchor(EncounterSpawnPointRef spawnPoint, ResolvedSiteId site, out CharacterVector3 position);
    }

    public readonly struct EncounterCharacterBinding
    {
        public EncounterParticipant Participant { get; }
        public CharacterVector3 Position { get; }

        public EncounterCharacterBinding(EncounterParticipant participant, CharacterVector3 position)
        {
            Participant = participant;
            Position = position;
        }
    }

    public sealed class EncounterRealization
    {
        public EncounterDefinition Definition { get; }
        public SiteRef SiteRole { get; }
        public ResolvedSiteId Site { get; }
        public string RealizationId => Site.Value;
        public CharacterVector3 Anchor { get; }
        public IReadOnlyList<EncounterCharacterBinding> Characters { get; }

        internal EncounterRealization(
            EncounterDefinition definition,
            SiteRef siteRole,
            ResolvedSiteId site,
            CharacterVector3 anchor,
            IReadOnlyList<EncounterCharacterBinding> characters)
        {
            Definition = definition;
            SiteRole = siteRole;
            Site = site;
            Anchor = anchor;
            Characters = characters;
        }
    }

    public sealed class EncounterRealizationResult
    {
        public bool IsSuccess => Failure == EncounterRealizationFailure.None;
        public EncounterRealizationFailure Failure { get; }
        public string Diagnostic { get; }
        public EncounterRealization Realization { get; }

        private EncounterRealizationResult(
            EncounterRealizationFailure failure,
            string diagnostic,
            EncounterRealization realization)
        {
            Failure = failure;
            Diagnostic = diagnostic ?? string.Empty;
            Realization = realization;
        }

        internal static EncounterRealizationResult Success(EncounterRealization realization) =>
            new EncounterRealizationResult(EncounterRealizationFailure.None, string.Empty, realization);

        internal static EncounterRealizationResult Fail(EncounterRealizationFailure failure, string diagnostic) =>
            new EncounterRealizationResult(failure, diagnostic, null);
    }

    public static class EncounterRealizationComposer
    {
        public static EncounterRealizationResult Compose(
            EncounterRealizationSpec spec,
            IEncounterRealizationFacts facts)
        {
            if (spec == null) throw new ArgumentNullException(nameof(spec));
            if (facts == null) throw new ArgumentNullException(nameof(facts));

            if (!facts.TryGetSiteAnchor(spec.Site, out CharacterVector3 siteAnchor))
                return EncounterRealizationResult.Fail(
                    EncounterRealizationFailure.MissingSiteRealization,
                    "Encounter '" + spec.Definition.Id + "' requires realized site '" + spec.Site +
                    "' for role '" + spec.SiteRole + "', but WorldBuilder realization supplied no anchor.");

            var seen = new HashSet<CharacterId>();
            var characters = new EncounterCharacterBinding[spec.Characters.Count];
            for (var i = 0; i < spec.Characters.Count; i++)
            {
                EncounterCharacterIntent intent = spec.Characters[i];
                if (!seen.Add(intent.CharacterId))
                    return EncounterRealizationResult.Fail(
                        EncounterRealizationFailure.DuplicateCharacter,
                        "Encounter '" + spec.Definition.Id + "' contains duplicate character '" +
                        intent.CharacterId + "'.");

                CharacterVector3 position = siteAnchor;
                if (intent.UsesSpawnPlacement)
                {
                    if (!facts.TryGetSpawnAnchor(intent.SpawnPoint, spec.Site, out position))
                        return EncounterRealizationResult.Fail(
                            EncounterRealizationFailure.MissingSpawnRealization,
                            "Encounter '" + spec.Definition.Id + "' requires spawn point '" + intent.SpawnPoint +
                            "' at realized site '" + spec.Site + "', but WorldBuilder realization supplied no spawn anchor.");
                }
                else if (intent.UsesNpcPlacement &&
                         !facts.TryGetNpcAnchor(intent.Npc, spec.Site, out position))
                {
                    return EncounterRealizationResult.Fail(
                        EncounterRealizationFailure.MissingCharacterRealization,
                        "Encounter '" + spec.Definition.Id + "' requires NPC '" + intent.Npc +
                        "' at realized site '" + spec.Site + "', but WorldBuilder realization supplied no NPC anchor.");
                }

                characters[i] = new EncounterCharacterBinding(
                    new EncounterParticipant(intent.CharacterId, intent.Ownership, intent.Role),
                    position);
            }

            return EncounterRealizationResult.Success(
                new EncounterRealization(
                    spec.Definition,
                    spec.SiteRole,
                    spec.Site,
                    siteAnchor,
                    Array.AsReadOnly(characters)));
        }
    }
}
