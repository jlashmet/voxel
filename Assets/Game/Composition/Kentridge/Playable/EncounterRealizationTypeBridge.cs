using System.Collections.Generic;
using Game.Characters.Api;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Kentridge-local type disambiguation for the composition bridge namespace/type name collision.
    /// It carries the shared realization object unchanged and owns no placement policy.
    /// </summary>
    internal sealed class EncounterRealization
    {
        private readonly global::Game.Composition.EncounterRealization.EncounterRealization _value;

        private EncounterRealization(global::Game.Composition.EncounterRealization.EncounterRealization value)
        {
            _value = value;
        }

        public EncounterDefinition Definition => _value.Definition;
        public SiteRef SiteRole => _value.SiteRole;
        public ResolvedSiteId Site => _value.Site;
        public string RealizationId => _value.RealizationId;
        public CharacterVector3 Anchor => _value.Anchor;
        public IReadOnlyList<global::Game.Composition.EncounterRealization.EncounterCharacterBinding> Characters => _value.Characters;

        public static implicit operator EncounterRealization(
            global::Game.Composition.EncounterRealization.EncounterRealization value) =>
            value == null ? null : new EncounterRealization(value);
    }
}
