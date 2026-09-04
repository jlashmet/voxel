using System;
using Game.Characters.Api;
using Game.Composition.EncounterRealization;
using Game.Encounters.Api;
using Game.WorldBuilder.Api;
using WorldBuilderCampaign = Game.WorldBuilder.Api.Campaign;

namespace Game.Composition.Kentridge.Playable
{
    /// <summary>
    /// Named Kentridge policy that adapts the exact WorldBuilder macro layout selected for the
    /// physical backend into the shared encounter-realization bridge. The shared bridge remains
    /// place-agnostic; the three formation slots belong to this authored forest ambush.
    /// </summary>
    public static class KentridgeForestEncounterRealization
    {
        private const float DecimetresToMetres = 0.1f;
        private static readonly object Gate = new object();
        private static readonly SiteRef ForestSiteRole = CreateForestSiteRole();
        private static readonly EncounterSpawnPointRef LeftBandit = new EncounterSpawnPointRef("bandit-left");
        private static readonly EncounterSpawnPointRef CentreBandit = new EncounterSpawnPointRef("bandit-centre");
        private static readonly EncounterSpawnPointRef RightBandit = new EncounterSpawnPointRef("bandit-right");
        private static Facts s_Facts;

        /// <summary>
        /// Called by the Kentridge world composition with the same planned layout instance that is
        /// handed to TopDownWorldLayoutSelection for backend realization.
        /// </summary>
        public static void RememberMacroLayout(
            TopDownWorldLayout layout,
            string forestNodeId,
            int rootXdm,
            int rootZdm,
            int cellSizeDm)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            if (string.IsNullOrWhiteSpace(forestNodeId))
                throw new ArgumentException("Forest node id is required.", nameof(forestNodeId));
            if (cellSizeDm < 1) throw new ArgumentOutOfRangeException(nameof(cellSizeDm));
            if (!layout.TryGetPosition(forestNodeId, out TopDownWorldGridPoint forestGrid))
                throw new InvalidOperationException(
                    "WorldBuilder macro layout did not realize required Kentridge forest node '" + forestNodeId + "'.");

            float forestX = (rootXdm + forestGrid.X * cellSizeDm) * DecimetresToMetres;
            float forestZ = (rootZdm + forestGrid.Y * cellSizeDm) * DecimetresToMetres;
            var site = new ResolvedSiteId("macro-" + forestNodeId);
            var anchor = new CharacterVector3(forestX, 0f, forestZ);

            // These are authored encounter formation policy inside the realized forest area, not
            // independent terrain/site placement. Centralizing them here keeps SpawnBandits free of
            // world-coordinate calculations and lets the bridge consume exact realization facts.
            var facts = new Facts(site, anchor);
            facts.Add(LeftBandit, new CharacterVector3(forestX - 5.4f, 0f, forestZ - 0.8f));
            facts.Add(CentreBandit, new CharacterVector3(forestX + 0.8f, 0f, forestZ + 1.2f));
            facts.Add(RightBandit, new CharacterVector3(forestX + 5.8f, 0f, forestZ + 0.1f));

            lock (Gate)
                s_Facts = facts;
        }

        public static EncounterRealizationResult Compose(
            EncounterDefinition definition,
            CharacterId first,
            CharacterId second,
            CharacterId third)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            Facts facts;
            lock (Gate)
                facts = s_Facts;
            if (facts == null)
                throw new InvalidOperationException(
                    "Kentridge forest encounter requires WorldBuilder macro realization before encounter composition.");

            var spec = new EncounterRealizationSpec(
                definition,
                ForestSiteRole,
                facts.Site,
                new[]
                {
                    new EncounterCharacterIntent(first, EncounterParticipantOwnership.EncounterOwned, "enemy", LeftBandit),
                    new EncounterCharacterIntent(second, EncounterParticipantOwnership.EncounterOwned, "enemy", CentreBandit),
                    new EncounterCharacterIntent(third, EncounterParticipantOwnership.EncounterOwned, "enemy", RightBandit)
                });
            return EncounterRealizationComposer.Compose(spec, facts);
        }

        private static SiteRef CreateForestSiteRole()
        {
            CampaignBuilder campaign = WorldBuilderCampaign.Create("kentridge-forest-encounter-realization");
            return campaign.World.Region("kentridge-macro-world").Site("forest-ambush-area").Ref;
        }

        private sealed class Facts : IEncounterRealizationFacts
        {
            private readonly EncounterSpawnPointRef[] _slots = new EncounterSpawnPointRef[3];
            private readonly CharacterVector3[] _positions = new CharacterVector3[3];
            private int _count;

            public ResolvedSiteId Site { get; }
            private CharacterVector3 SiteAnchor { get; }

            public Facts(ResolvedSiteId site, CharacterVector3 siteAnchor)
            {
                Site = site;
                SiteAnchor = siteAnchor;
            }

            public void Add(EncounterSpawnPointRef slot, CharacterVector3 position)
            {
                if (_count >= _slots.Length)
                    throw new InvalidOperationException("Kentridge forest encounter has too many authored formation slots.");
                _slots[_count] = slot;
                _positions[_count] = position;
                _count++;
            }

            public bool TryGetSiteAnchor(ResolvedSiteId site, out CharacterVector3 position)
            {
                if (site.Equals(Site))
                {
                    position = SiteAnchor;
                    return true;
                }
                position = default;
                return false;
            }

            public bool TryGetNpcAnchor(NpcRef npc, ResolvedSiteId site, out CharacterVector3 position)
            {
                position = default;
                return false;
            }

            public bool TryGetSpawnAnchor(
                EncounterSpawnPointRef spawnPoint,
                ResolvedSiteId site,
                out CharacterVector3 position)
            {
                if (!site.Equals(Site))
                {
                    position = default;
                    return false;
                }
                for (var i = 0; i < _count; i++)
                {
                    if (_slots[i] != spawnPoint) continue;
                    position = _positions[i];
                    return true;
                }
                position = default;
                return false;
            }
        }
    }
}
