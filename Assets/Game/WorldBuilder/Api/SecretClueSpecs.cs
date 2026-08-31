using System;
using System.Collections.Generic;

namespace Game.WorldBuilder.Api
{
    public readonly struct SecretClueId : IEquatable<SecretClueId>
    {
        public string Id { get; }
        public SecretClueId(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretClueId other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretClueId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public enum SecretClueKind
    {
        Environmental = 0,
        Inspectable = 1,
        Readable = 2,
        Rumor = 3,
        Memory = 4
    }

    public enum SecretClueRequirement
    {
        Optional = 0,
        Required = 1
    }

    public enum SecretClueSourceKind
    {
        Site = 0,
        Npc = 1
    }

    /// <summary>
    /// Semantic source candidate for a clue. It intentionally carries WorldBuilder identities only;
    /// generated transforms, colliders and interaction objects are supplied by downstream realization.
    /// </summary>
    public readonly struct SecretClueSourceSpec
    {
        public SecretClueSourceKind Kind { get; }
        public SiteRef Site { get; }
        public NpcRef Npc { get; }

        private SecretClueSourceSpec(SecretClueSourceKind kind, SiteRef site, NpcRef npc)
        {
            Kind = kind;
            Site = site;
            Npc = npc;
        }

        public static SecretClueSourceSpec AtSite(SiteRef site) =>
            new SecretClueSourceSpec(SecretClueSourceKind.Site, site, default);

        public static SecretClueSourceSpec FromNpc(NpcRef npc) =>
            new SecretClueSourceSpec(SecretClueSourceKind.Npc, default, npc);
    }

    /// <summary>One authored step in the discovery chain for a stable secret.</summary>
    public sealed class SecretClueSpec
    {
        public SecretClueId Id { get; }
        public SecretRef Secret { get; }
        public int Stage { get; }
        public SecretClueKind Kind { get; }
        public SecretClueRequirement Requirement { get; }
        public IReadOnlyList<SecretClueSourceSpec> Sources { get; }
        public SiteRef TargetSite { get; }
        public bool HasTargetSite { get; }
        public string ContentKey { get; }
        public string MemoryTopic { get; }

        internal SecretClueSpec(
            SecretClueId id,
            SecretRef secret,
            int stage,
            SecretClueKind kind,
            SecretClueRequirement requirement,
            SecretClueSourceSpec[] sources,
            SiteRef targetSite,
            bool hasTargetSite,
            string contentKey,
            string memoryTopic)
        {
            if (stage < 1) throw new ArgumentOutOfRangeException(nameof(stage));
            if (sources == null || sources.Length == 0)
                throw new ArgumentException("A secret clue requires at least one semantic source candidate.", nameof(sources));
            if (string.IsNullOrWhiteSpace(contentKey))
                throw new ArgumentException("A secret clue requires authored content.", nameof(contentKey));

            Id = id;
            Secret = secret;
            Stage = stage;
            Kind = kind;
            Requirement = requirement;
            Sources = sources;
            TargetSite = targetSite;
            HasTargetSite = hasTargetSite;
            ContentKey = contentKey;
            MemoryTopic = string.IsNullOrWhiteSpace(memoryTopic) ? string.Empty : memoryTopic;
        }
    }

    public sealed class SecretClueBuilder
    {
        private readonly SecretClueId _id;
        private readonly SecretRef _secret;
        private readonly List<SecretClueSourceSpec> _sources = new List<SecretClueSourceSpec>();
        private int _stage;
        private SecretClueKind _kind;
        private bool _hasStage;
        private bool _hasKind;
        private SecretClueRequirement _requirement = SecretClueRequirement.Required;
        private SiteRef _targetSite;
        private bool _hasTargetSite;
        private string _contentKey;
        private string _memoryTopic;

        internal SecretClueBuilder(SecretClueId id, SecretRef secret)
        {
            _id = id;
            _secret = secret;
        }

        public SecretClueBuilder Stage(int stage)
        {
            if (stage < 1) throw new ArgumentOutOfRangeException(nameof(stage));
            _stage = stage;
            _hasStage = true;
            return this;
        }

        public SecretClueBuilder Kind(SecretClueKind kind)
        {
            _kind = kind;
            _hasKind = true;
            return this;
        }

        public SecretClueBuilder Required()
        {
            _requirement = SecretClueRequirement.Required;
            return this;
        }

        public SecretClueBuilder Optional()
        {
            _requirement = SecretClueRequirement.Optional;
            return this;
        }

        public SecretClueBuilder SourceAt(SiteRef site)
        {
            _sources.Add(SecretClueSourceSpec.AtSite(site));
            return this;
        }

        public SecretClueBuilder SourceFrom(NpcRef npc)
        {
            _sources.Add(SecretClueSourceSpec.FromNpc(npc));
            return this;
        }

        public SecretClueBuilder Target(SiteRef site)
        {
            _targetSite = site;
            _hasTargetSite = true;
            return this;
        }

        public SecretClueBuilder Content(string contentKey)
        {
            _contentKey = contentKey;
            return this;
        }

        public SecretClueBuilder RememberAs(string memoryTopic)
        {
            _memoryTopic = memoryTopic;
            return this;
        }

        internal SecretClueSpec Build()
        {
            if (!_hasStage)
                throw new InvalidOperationException("Secret clue '" + _id + "' requires a stage.");
            if (!_hasKind)
                throw new InvalidOperationException("Secret clue '" + _id + "' requires a clue kind.");
            if (_sources.Count == 0)
                throw new InvalidOperationException("Secret clue '" + _id + "' requires at least one source.");
            if (string.IsNullOrWhiteSpace(_contentKey))
                throw new InvalidOperationException("Secret clue '" + _id + "' requires authored content.");

            return new SecretClueSpec(
                _id,
                _secret,
                _stage,
                _kind,
                _requirement,
                _sources.ToArray(),
                _targetSite,
                _hasTargetSite,
                _contentKey,
                _memoryTopic);
        }
    }

    public sealed partial class WorldBlueprintBuilder
    {
        public SecretClueId Clue(string id, SecretRef secret, Action<SecretClueBuilder> configure)
        {
            var clueId = new SecretClueId(id);
            var builder = new SecretClueBuilder(clueId, secret);
            configure?.Invoke(builder);
            _campaign.SecretClues.Add(builder.Build());
            return clueId;
        }
    }
}
