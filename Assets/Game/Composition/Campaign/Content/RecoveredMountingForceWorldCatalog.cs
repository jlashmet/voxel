using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;

namespace Game.Composition.Campaign.Content
{
    /// <summary>
    /// Translation metadata for one recovered town-centered overworld region.
    /// RegionId/SettlementId are voxel-owned semantic ids. Legacy map ids are retained only as
    /// source evidence because the original game's TMX naming is not consistent across towns.
    /// CandidateBoundaryMapIds are deliberately not treated as owned maps until the recovered
    /// traversal graph gives us enough evidence to assign shared-road/wilderness semantics.
    /// </summary>
    public sealed class RecoveredOverworldRegionDefinition
    {
        public string RegionId { get; }
        public string SettlementId { get; }
        public SettlementArchetype SettlementArchetype { get; }
        public IReadOnlyList<string> LegacyOverworldMapIds { get; }
        public IReadOnlyList<string> LegacySettlementMapIds { get; }
        public IReadOnlyList<string> CandidateBoundaryMapIds { get; }

        internal RecoveredOverworldRegionDefinition(
            string regionId,
            string settlementId,
            SettlementArchetype settlementArchetype,
            string[] legacyOverworldMapIds,
            string[] legacySettlementMapIds,
            string[] candidateBoundaryMapIds)
        {
            RegionId = regionId ?? throw new ArgumentNullException(nameof(regionId));
            SettlementId = settlementId ?? throw new ArgumentNullException(nameof(settlementId));
            SettlementArchetype = settlementArchetype;
            LegacyOverworldMapIds = legacyOverworldMapIds ?? Array.Empty<string>();
            LegacySettlementMapIds = legacySettlementMapIds ?? Array.Empty<string>();
            CandidateBoundaryMapIds = candidateBoundaryMapIds ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// Handles produced when the recovered high-level world hierarchy is registered into a campaign.
    /// </summary>
    public sealed class RecoveredMountingForceWorldHandles
    {
        public RegionHandle KentridgeOverworld { get; }
        public SettlementHandle Kentridge { get; }
        public RegionHandle HightownOverworld { get; }
        public SettlementHandle Hightown { get; }
        public RegionHandle MoordellOverworld { get; }
        public SettlementHandle Moordell { get; }
        public RegionHandle RossdamOverworld { get; }
        public SettlementHandle Rossdam { get; }
        public RegionHandle FairyVillageOverworld { get; }
        public SettlementHandle FairyVillage { get; }
        public RegionHandle OrcVillageOverworld { get; }
        public SettlementHandle OrcVillage { get; }

        internal RecoveredMountingForceWorldHandles(
            RegionHandle kentridgeOverworld,
            SettlementHandle kentridge,
            RegionHandle hightownOverworld,
            SettlementHandle hightown,
            RegionHandle moordellOverworld,
            SettlementHandle moordell,
            RegionHandle rossdamOverworld,
            SettlementHandle rossdam,
            RegionHandle fairyVillageOverworld,
            SettlementHandle fairyVillage,
            RegionHandle orcVillageOverworld,
            SettlementHandle orcVillage)
        {
            KentridgeOverworld = kentridgeOverworld;
            Kentridge = kentridge;
            HightownOverworld = hightownOverworld;
            Hightown = hightown;
            MoordellOverworld = moordellOverworld;
            Moordell = moordell;
            RossdamOverworld = rossdamOverworld;
            Rossdam = rossdam;
            FairyVillageOverworld = fairyVillageOverworld;
            FairyVillage = fairyVillage;
            OrcVillageOverworld = orcVillageOverworld;
            OrcVillage = orcVillage;
        }
    }

    /// <summary>
    /// Voxel-owned normalization of the recovered Mounting Force settlement/overworld structure.
    /// This intentionally does not rename legacy maps. For example, Kentridge's recovered exterior
    /// evidence is named "overworld" / "overworld_big", while the semantic region is
    /// "kentridge-overworld".
    /// </summary>
    public static class RecoveredMountingForceWorldCatalog
    {
        public static readonly RecoveredOverworldRegionDefinition Kentridge = new RecoveredOverworldRegionDefinition(
            "kentridge-overworld",
            "kentridge",
            SettlementArchetype.Town,
            new[] { "overworld", "overworld_big" },
            new[]
            {
                "kentridge",
                "kentridge-abandoned-house",
                "kentridge-armor-shop",
                "kentridge-awon-house",
                "kentridge-church",
                "kentridge-inn",
                "kentridge-katie-house",
                "kentridge-logan-house",
                "kentridge-magic-shop",
                "kentridge-mayor-house",
                "kentridge-pub",
                "kentridge-rebecca-house",
                "kentridge-sarah-house",
                "kentridge-warehouse",
                "kentridge-warehouse-lower",
                "kentridge-weapon-shop",
                "kentridge-well",
                "medrare-house-upper",
                "medrare-house-lower"
            },
            new[]
            {
                "forest",
                "graveyard",
                "mountains",
                "mountains-cave",
                "overworld-cave",
                "overworld-farmer-house",
                "overworld-underground",
                "radcliffeMansion",
                "south-fighting-area-1"
            });

        public static readonly RecoveredOverworldRegionDefinition Hightown = new RecoveredOverworldRegionDefinition(
            "hightown-overworld",
            "hightown",
            SettlementArchetype.Town,
            Array.Empty<string>(),
            new[]
            {
                "hightown",
                "hightown-armor-shop",
                "hightown-cave",
                "hightown-church",
                "hightown-magic-shop",
                "hightown-mayor-house",
                "hightown-pub",
                "hightown-timmy-house",
                "hightown-timmy-house-back-room",
                "hightown-under-church",
                "hightown-under-church2",
                "hightown-weapon-shop"
            },
            new[]
            {
                "fighting-area-2",
                "fighting-area-2-cave-1",
                "fighting-area-1",
                "forest",
                "forest-maze",
                "bandit-hideout"
            });

        public static readonly RecoveredOverworldRegionDefinition Moordell = new RecoveredOverworldRegionDefinition(
            "moordell-overworld",
            "moordell",
            SettlementArchetype.Town,
            new[] { "overworld-moordell" },
            new[]
            {
                "moordell",
                "moordell-armor-shop",
                "moordell-building1",
                "moordell-grave",
                "moordell-inn",
                "moordell-magic-shop",
                "moordell-pub",
                "moordell-weapon-shop"
            },
            new[]
            {
                "graveyard",
                "graveyard-lower",
                "overworld",
                "overworld-moordell-cave",
                "overworld-moordell-excalibur-cave",
                "overworld-moordell-underground",
                "overworld-to-rossdam",
                "overworld-rossdam",
                "wizard-trials"
            });

        public static readonly RecoveredOverworldRegionDefinition Rossdam = new RecoveredOverworldRegionDefinition(
            "rossdam-overworld",
            "rossdam",
            SettlementArchetype.Town,
            new[] { "overworld-rossdam" },
            new[]
            {
                "rossdam",
                "rossdam-armor-shop",
                "rossdam-king-chamber",
                "rossdam-magic-shop",
                "rossdam-rorik-house",
                "rossdam-weapon-shop"
            },
            new[] { "overworld-to-rossdam", "end-logan", "epilogue" });

        public static readonly RecoveredOverworldRegionDefinition FairyVillage = new RecoveredOverworldRegionDefinition(
            "fairy-village-overworld",
            "fairy-village",
            SettlementArchetype.Village,
            Array.Empty<string>(),
            new[]
            {
                "fairy-village",
                "fairy-village-cave",
                "fairy-village-treehouse",
                "fairy-village-accessory-shop",
                "fairy-village-inn",
                "fairy-village-magic-shop",
                "fairy-village-mary-house",
                "fairy-village-pub",
                "fairy-village-rita-house",
                "fairy-village-weapon-shop"
            },
            new[]
            {
                "south-fighting-area-1",
                "south-fighting-area-1-cave",
                "mountains",
                "orc-village",
                "overworld-logan-castle"
            });

        public static readonly RecoveredOverworldRegionDefinition OrcVillage = new RecoveredOverworldRegionDefinition(
            "orc-village-overworld",
            "orc-village",
            SettlementArchetype.Village,
            Array.Empty<string>(),
            new[]
            {
                "orc-village",
                "orc-village-armor-shop",
                "orc-village-magic-shop",
                "orc-village-pub",
                "orc-village-weapon-shop"
            },
            new[]
            {
                "south-fighting-area-1",
                "south-fighting-area-1-cave",
                "fairy-village",
                "mountains",
                "overworld-logan-castle"
            });

        public static readonly IReadOnlyList<RecoveredOverworldRegionDefinition> All = new[]
        {
            Kentridge,
            Hightown,
            Moordell,
            Rossdam,
            FairyVillage,
            OrcVillage
        };

        public static RecoveredMountingForceWorldHandles RegisterHierarchy(WorldBlueprintBuilder world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));

            RegionHandle kentridgeOverworld = world.Region(Kentridge.RegionId);
            SettlementHandle kentridge = kentridgeOverworld.Settlement(
                Kentridge.SettlementId,
                Kentridge.SettlementArchetype);

            RegionHandle hightownOverworld = world.Region(Hightown.RegionId);
            SettlementHandle hightown = hightownOverworld.Settlement(
                Hightown.SettlementId,
                Hightown.SettlementArchetype);

            RegionHandle moordellOverworld = world.Region(Moordell.RegionId);
            SettlementHandle moordell = moordellOverworld.Settlement(
                Moordell.SettlementId,
                Moordell.SettlementArchetype);

            RegionHandle rossdamOverworld = world.Region(Rossdam.RegionId);
            SettlementHandle rossdam = rossdamOverworld.Settlement(
                Rossdam.SettlementId,
                Rossdam.SettlementArchetype);

            RegionHandle fairyVillageOverworld = world.Region(FairyVillage.RegionId);
            SettlementHandle fairyVillage = fairyVillageOverworld.Settlement(
                FairyVillage.SettlementId,
                FairyVillage.SettlementArchetype);

            RegionHandle orcVillageOverworld = world.Region(OrcVillage.RegionId);
            SettlementHandle orcVillage = orcVillageOverworld.Settlement(
                OrcVillage.SettlementId,
                OrcVillage.SettlementArchetype);

            return new RecoveredMountingForceWorldHandles(
                kentridgeOverworld,
                kentridge,
                hightownOverworld,
                hightown,
                moordellOverworld,
                moordell,
                rossdamOverworld,
                rossdam,
                fairyVillageOverworld,
                fairyVillage,
                orcVillageOverworld,
                orcVillage);
        }
    }
}
