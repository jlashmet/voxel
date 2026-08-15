using System;

namespace Game.WorldBuilder.Api
{
    internal static class WorldIdRules
    {
        public static string Require(string value, string paramName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("World-builder ids must be non-empty.", paramName);
            return value;
        }
    }

    public readonly struct SiteRef : IEquatable<SiteRef>
    {
        public string Id { get; }
        public SiteRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SiteRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SiteRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct NpcRef : IEquatable<NpcRef>
    {
        public string Id { get; }
        public NpcRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(NpcRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is NpcRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct CutsceneRef : IEquatable<CutsceneRef>
    {
        public string Id { get; }
        public CutsceneRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(CutsceneRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CutsceneRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct ObjectiveRef : IEquatable<ObjectiveRef>
    {
        public string Id { get; }
        public ObjectiveRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(ObjectiveRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ObjectiveRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct LootTableRef : IEquatable<LootTableRef>
    {
        public string Id { get; }
        public LootTableRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(LootTableRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is LootTableRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    public readonly struct SecretPolicyRef : IEquatable<SecretPolicyRef>
    {
        public string Id { get; }
        public SecretPolicyRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretPolicyRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretPolicyRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }

    /// <summary>Stable authored identity for one required secret, distinct from a procedural secret policy.</summary>
    public readonly struct SecretRef : IEquatable<SecretRef>
    {
        public string Id { get; }
        public SecretRef(string id) => Id = WorldIdRules.Require(id, nameof(id));
        public bool Equals(SecretRef other) => string.Equals(Id, other.Id, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SecretRef other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Id ?? string.Empty);
        public override string ToString() => Id ?? string.Empty;
    }
}
