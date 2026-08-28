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
                // Terrain-only uses the focused house pipeline's no-castle world topology. The
                // caller may leave authored feature placement empty; importantly, the semantic
                // resolver never silently opts a fortification into a terrain-only request.
                content = ShowcaseFeatureContent.HouseOnly;
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
