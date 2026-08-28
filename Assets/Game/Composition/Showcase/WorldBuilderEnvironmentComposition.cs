using System;
using Game.WorldBuilder.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Production mapping from engine-independent WorldBuilder environment intent to the existing
    /// bounded showcase/world-generation backends. Scene code chooses semantic features; this
    /// composition root owns which concrete generated-content mode realizes them.
    /// </summary>
    public static class WorldBuilderEnvironmentComposition
    {
        public readonly struct Plan
        {
            public uint Seed { get; }
            public ShowcaseFeatureContent ShowcaseContent { get; }
            public bool PopulateVegetation { get; }
            public bool PopulateAmbientLife { get; }
            public bool BuildGalleryDistrict { get; }

            internal Plan(
                uint seed,
                ShowcaseFeatureContent showcaseContent,
                bool populateVegetation,
                bool populateAmbientLife,
                bool buildGalleryDistrict)
            {
                Seed = seed;
                ShowcaseContent = showcaseContent;
                PopulateVegetation = populateVegetation;
                PopulateAmbientLife = populateAmbientLife;
                BuildGalleryDistrict = buildGalleryDistrict;
            }
        }

        /// <summary>
        /// Compatibility adapter for serialized showcase scenes. The serialized enum remains so
        /// existing scene YAML does not churn, but its value is immediately translated into a
        /// WorldBuilder semantic recipe; concrete backend selection happens only after that.
        /// </summary>
        public static WorldEnvironmentSpec SemanticSpec(
            uint seed,
            ShowcaseFeatureContent serializedContent)
        {
            return serializedContent switch
            {
                ShowcaseFeatureContent.Full =>
                    WorldEnvironmentRecipes.SettlementWithFortification(seed),
                ShowcaseFeatureContent.CastleOnly =>
                    WorldEnvironmentRecipes.FortifiedLandmark(seed),
                ShowcaseFeatureContent.HouseOnly =>
                    WorldEnvironmentRecipes.DetailedStructure(seed),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(serializedContent), serializedContent,
                    "Unknown serialized showcase composition preset."),
            };
        }

        public static Plan Resolve(in WorldEnvironmentSpec spec)
        {
            bool settlement = spec.Includes(WorldEnvironmentFeature.Settlement);
            bool fortification = spec.Includes(WorldEnvironmentFeature.Fortification);
            bool detailedStructure = spec.Includes(WorldEnvironmentFeature.DetailedStructure);
            bool gallery = spec.Includes(WorldEnvironmentFeature.GalleryDistrict);

            if (detailedStructure && (settlement || fortification || gallery))
                throw new ArgumentException(
                    "DetailedStructure is a focused composition and cannot be combined with " +
                    "settlement, fortification, or gallery composition.", nameof(spec));

            if (gallery && (!settlement || !fortification))
                throw new ArgumentException(
                    "A gallery district requires both settlement and fortification content.", nameof(spec));

            ShowcaseFeatureContent content;
            if (detailedStructure)
            {
                content = ShowcaseFeatureContent.HouseOnly;
            }
            else if (settlement && fortification)
            {
                content = ShowcaseFeatureContent.Full;
            }
            else if (fortification)
            {
                content = ShowcaseFeatureContent.CastleOnly;
            }
            else if (settlement)
            {
                throw new NotSupportedException(
                    "The current showcase backend has no settlement-without-landmark mode. " +
                    "Add that reusable backend before requesting this semantic combination.");
            }
            else
            {
                // Terrain-only currently has no dedicated showcase content mode. Do not silently
                // pretend HouseOnly means terrain-only: a caller that needs this composition must
                // add a real reusable backend first.
                throw new NotSupportedException(
                    "The current showcase backend has no terrain-only content mode. " +
                    "Add that reusable backend before requesting this semantic combination.");
            }

            return new Plan(
                spec.Seed,
                content,
                spec.Includes(WorldEnvironmentFeature.Vegetation),
                spec.Includes(WorldEnvironmentFeature.AmbientLife),
                gallery);
        }
    }
}