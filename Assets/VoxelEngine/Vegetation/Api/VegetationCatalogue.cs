namespace VoxelEngine.Core.Vegetation
{
    /// <summary>
    /// Shared species catalogue. Renderers consume GrowthForm/Traits while placement consumes the
    /// ecological weights, so adding a fantasy species does not require a new renderer.
    /// </summary>
    public static class VegetationCatalogue
    {
        public const int Count = 46;

        public static VegetationKind KindAt(int index)
        {
            return (VegetationKind)index;
        }

        public static VegetationGrowthForm GrowthForm(VegetationKind kind)
        {
            return Get(kind).GrowthForm;
        }

        public static bool HasTrait(VegetationKind kind, VegetationTraits trait)
        {
            return (Get(kind).Traits & trait) != 0;
        }

        public static VegetationProfile Get(VegetationKind kind)
        {
            switch (kind)
            {
                case VegetationKind.Flower:
                    return P(kind, VegetationGrowthForm.Tuft, ground: 1f, rock: 0.08f,
                        moisture: 0.35f, shade: -0.20f, slope: 0.70f);
                case VegetationKind.Fern:
                    return P(kind, VegetationGrowthForm.Frond, ground: 1f, rock: 0.15f,
                        moisture: 0.80f, shade: 0.75f, slope: 0.80f);
                case VegetationKind.Bush:
                    return P(kind, VegetationGrowthForm.Shrub, VegetationTraits.Woody,
                        ground: 1f, rock: 0.05f, moisture: 0.25f, shade: 0.05f, slope: 0.55f);
                case VegetationKind.Moss:
                    return P(kind, VegetationGrowthForm.Creeper, ground: 0.45f, rock: 1f, wood: 0.80f,
                        masonry: 1f, moisture: 1f, shade: 0.90f);
                case VegetationKind.Vine:
                    return P(kind, VegetationGrowthForm.Climber, VegetationTraits.Cuttable,
                        rock: 0.55f, wood: 1f, masonry: 1f, moisture: 0.70f, shade: 0.45f);
                case VegetationKind.Clover:
                    return P(kind, VegetationGrowthForm.Tuft, ground: 1f, moisture: 0.45f,
                        shade: 0.05f, slope: 0.80f);
                case VegetationKind.Weed:
                    return P(kind, VegetationGrowthForm.Tuft, ground: 1f, rock: 0.08f,
                        masonry: 0.05f, moisture: 0.05f, shade: -0.15f, slope: 0.80f);
                case VegetationKind.Nettle:
                    return P(kind, VegetationGrowthForm.Tuft, VegetationTraits.Toxic,
                        ground: 1f, moisture: 0.65f, shade: 0.35f, slope: 0.70f);
                case VegetationKind.Reed:
                    return P(kind, VegetationGrowthForm.Tuft, ground: 0.65f, water: 0.75f,
                        moisture: 1f, shade: -0.10f, slope: 0.60f);
                case VegetationKind.Cattail:
                    return P(kind, VegetationGrowthForm.Tuft, ground: 0.55f, water: 1f,
                        moisture: 1f, shade: -0.15f, slope: 0.55f);
                case VegetationKind.Mushroom:
                    return P(kind, VegetationGrowthForm.Fungus, VegetationTraits.Edible,
                        ground: 0.85f, rock: 0.15f, wood: 0.55f, moisture: 0.80f, shade: 0.85f);
                case VegetationKind.FallenLeaves:
                    return P(kind, VegetationGrowthForm.Debris, VegetationTraits.Dead,
                        ground: 0.60f, moisture: -0.10f, shade: 0.35f, slope: 0.45f);
                case VegetationKind.PineNeedles:
                    return P(kind, VegetationGrowthForm.Debris, VegetationTraits.Dead,
                        ground: 0.45f, moisture: -0.25f, shade: 0.20f, slope: 0.45f);
                case VegetationKind.Ivy:
                    return P(kind, VegetationGrowthForm.Climber, VegetationTraits.Cuttable,
                        rock: 0.55f, wood: 0.85f, masonry: 1f, moisture: 0.60f, shade: 0.45f);
                case VegetationKind.Lichen:
                    return P(kind, VegetationGrowthForm.Creeper, rock: 1f, wood: 0.45f,
                        masonry: 0.75f, moisture: 0.20f, shade: 0.10f);
                case VegetationKind.WallFern:
                    return P(kind, VegetationGrowthForm.Frond, rock: 0.70f, wood: 0.40f,
                        masonry: 0.80f, moisture: 0.90f, shade: 0.95f);
                case VegetationKind.BerryBush:
                    return P(kind, VegetationGrowthForm.Shrub,
                        VegetationTraits.Woody | VegetationTraits.Edible,
                        ground: 0.85f, moisture: 0.45f, shade: 0.15f, slope: 0.55f);
                case VegetationKind.ThornBush:
                    return P(kind, VegetationGrowthForm.Shrub,
                        VegetationTraits.Woody | VegetationTraits.Thorny,
                        ground: 0.70f, moisture: -0.20f, shade: -0.10f, slope: 0.55f);
                case VegetationKind.HedgeShrub:
                    return P(kind, VegetationGrowthForm.Shrub, VegetationTraits.Woody,
                        ground: 0.65f, moisture: 0.20f, shade: 0.05f, slope: 0.45f);
                case VegetationKind.DeadShrub:
                    return P(kind, VegetationGrowthForm.Shrub,
                        VegetationTraits.Woody | VegetationTraits.Dead,
                        ground: 0.35f, rock: 0.05f, moisture: -0.75f, shade: -0.15f, slope: 0.60f);
                case VegetationKind.Sapling:
                    return P(kind, VegetationGrowthForm.Shrub, VegetationTraits.Woody,
                        ground: 0.65f, moisture: 0.30f, shade: 0.15f, slope: 0.50f);
                case VegetationKind.FloweringShrub:
                    return P(kind, VegetationGrowthForm.Shrub, VegetationTraits.Woody,
                        ground: 0.70f, moisture: 0.35f, shade: -0.10f, slope: 0.50f);
                case VegetationKind.LilyPad:
                    return P(kind, VegetationGrowthForm.Aquatic, water: 1f,
                        moisture: 1f, shade: 0.10f);
                case VegetationKind.WaterGrass:
                    return P(kind, VegetationGrowthForm.Aquatic, water: 1f, ground: 0.15f,
                        moisture: 1f, shade: -0.10f);
                case VegetationKind.Algae:
                    return P(kind, VegetationGrowthForm.Aquatic, water: 0.90f, rock: 0.15f,
                        moisture: 1f, shade: 0.15f);
                case VegetationKind.ShelfFungus:
                    return P(kind, VegetationGrowthForm.Fungus, wood: 1f, rock: 0.10f,
                        moisture: 0.65f, shade: 0.80f);
                case VegetationKind.FallenLog:
                    return P(kind, VegetationGrowthForm.Debris,
                        VegetationTraits.Woody | VegetationTraits.Dead,
                        ground: 0.12f, moisture: 0.15f, shade: 0.30f, slope: 0.35f);
                case VegetationKind.ExposedRoot:
                    return P(kind, VegetationGrowthForm.Root, VegetationTraits.Woody,
                        ground: 0.22f, rock: 0.08f, moisture: 0.10f, shade: 0.15f, slope: 0.65f);
                case VegetationKind.HangingMoss:
                    return P(kind, VegetationGrowthForm.Hanger, rock: 0.35f, wood: 1f,
                        masonry: 0.45f, moisture: 1f, shade: 0.90f);
                case VegetationKind.TrunkMoss:
                    return P(kind, VegetationGrowthForm.Creeper, wood: 1f, rock: 0.15f,
                        moisture: 0.90f, shade: 0.75f);
                case VegetationKind.Epiphyte:
                    return P(kind, VegetationGrowthForm.Frond, wood: 1f, rock: 0.15f,
                        moisture: 0.80f, shade: 0.65f);
                case VegetationKind.DeadBranch:
                    return P(kind, VegetationGrowthForm.Debris,
                        VegetationTraits.Woody | VegetationTraits.Dead,
                        ground: 0.18f, moisture: -0.20f, shade: 0.10f, slope: 0.40f);
                case VegetationKind.HangingVine:
                    return P(kind, VegetationGrowthForm.Hanger, VegetationTraits.Cuttable,
                        rock: 0.50f, wood: 1f, masonry: 0.75f, moisture: 0.65f, shade: 0.40f);
                case VegetationKind.ClimbingVine:
                    return P(kind, VegetationGrowthForm.Climber, VegetationTraits.Cuttable,
                        rock: 0.45f, wood: 1f, masonry: 0.90f, moisture: 0.55f, shade: 0.25f);
                case VegetationKind.DanglingRoot:
                    return P(kind, VegetationGrowthForm.Hanger,
                        VegetationTraits.Woody | VegetationTraits.Cuttable,
                        rock: 0.35f, ground: 0.05f, wood: 0.60f, moisture: 0.45f, shade: 0.55f);
                case VegetationKind.DeadGrass:
                    return P(kind, VegetationGrowthForm.Tuft, VegetationTraits.Dead,
                        ground: 0.40f, moisture: -0.85f, shade: -0.20f, slope: 0.75f);
                case VegetationKind.DeadVine:
                    return P(kind, VegetationGrowthForm.Hanger,
                        VegetationTraits.Dead | VegetationTraits.Cuttable,
                        rock: 0.15f, wood: 0.35f, masonry: 0.35f, moisture: -0.70f, shade: 0.05f);

                case VegetationKind.Glowshroom:
                    return P(kind, VegetationGrowthForm.Fungus,
                        VegetationTraits.Magical | VegetationTraits.Luminous,
                        ground: 0.70f, rock: 0.35f, wood: 0.55f, moisture: 0.80f, shade: 0.90f,
                        arcane: 1f, minArcane: 0.35f);
                case VegetationKind.ManaBloom:
                    return P(kind, VegetationGrowthForm.Tuft,
                        VegetationTraits.Magical | VegetationTraits.Luminous,
                        ground: 0.80f, rock: 0.05f, moisture: 0.30f, shade: -0.15f,
                        arcane: 1f, minArcane: 0.45f, slope: 0.65f);
                case VegetationKind.CrystalShrub:
                    return P(kind, VegetationGrowthForm.Shrub,
                        VegetationTraits.Magical | VegetationTraits.Woody,
                        ground: 0.55f, rock: 0.55f, moisture: -0.15f, shade: -0.05f,
                        arcane: 1f, minArcane: 0.55f, slope: 0.65f);
                case VegetationKind.WispReed:
                    return P(kind, VegetationGrowthForm.Tuft,
                        VegetationTraits.Magical | VegetationTraits.Luminous,
                        ground: 0.30f, water: 1f, moisture: 1f, shade: 0.10f,
                        arcane: 0.95f, minArcane: 0.40f, slope: 0.55f);
                case VegetationKind.MoonFern:
                    return P(kind, VegetationGrowthForm.Frond,
                        VegetationTraits.Magical | VegetationTraits.Luminous,
                        ground: 0.65f, rock: 0.15f, wood: 0.15f, moisture: 0.60f, shade: 0.90f,
                        arcane: 0.85f, minArcane: 0.35f, slope: 0.75f);
                case VegetationKind.EmberThorn:
                    return P(kind, VegetationGrowthForm.Shrub,
                        VegetationTraits.Magical | VegetationTraits.Thorny | VegetationTraits.Luminous,
                        ground: 0.55f, rock: 0.20f, moisture: -0.85f, shade: -0.45f,
                        arcane: 0.90f, minArcane: 0.50f, slope: 0.65f);
                case VegetationKind.StarMoss:
                    return P(kind, VegetationGrowthForm.Creeper,
                        VegetationTraits.Magical | VegetationTraits.Luminous,
                        ground: 0.25f, rock: 0.75f, wood: 0.55f, masonry: 0.80f,
                        moisture: 0.70f, shade: 0.75f, arcane: 1f, minArcane: 0.40f);
                case VegetationKind.ArcaneVine:
                    return P(kind, VegetationGrowthForm.Climber,
                        VegetationTraits.Magical | VegetationTraits.Luminous | VegetationTraits.Cuttable,
                        rock: 0.45f, wood: 0.80f, masonry: 0.90f, moisture: 0.55f, shade: 0.30f,
                        arcane: 1f, minArcane: 0.45f);

                case VegetationKind.Grass:
                default:
                    return P(VegetationKind.Grass, VegetationGrowthForm.Tuft,
                        ground: 1f, rock: 0.04f, moisture: 0.20f, shade: -0.05f, slope: 0.85f);
            }
        }

        public static float SurfaceWeight(in VegetationProfile profile, VegetationSurface surface)
        {
            switch (surface)
            {
                case VegetationSurface.Rock: return profile.RockWeight;
                case VegetationSurface.Wood: return profile.WoodWeight;
                case VegetationSurface.Masonry: return profile.MasonryWeight;
                case VegetationSurface.Water: return profile.WaterWeight;
                case VegetationSurface.Ground:
                default: return profile.GroundWeight;
            }
        }

        private static VegetationProfile P(
            VegetationKind kind,
            VegetationGrowthForm form,
            VegetationTraits traits = VegetationTraits.None,
            float ground = 0f,
            float rock = 0f,
            float wood = 0f,
            float masonry = 0f,
            float water = 0f,
            float moisture = 0f,
            float shade = 0f,
            float arcane = 0f,
            float minArcane = 0f,
            float slope = 1f)
        {
            return new VegetationProfile
            {
                Kind = kind,
                GrowthForm = form,
                Traits = traits,
                GroundWeight = ground,
                RockWeight = rock,
                WoodWeight = wood,
                MasonryWeight = masonry,
                WaterWeight = water,
                MoistureAffinity = moisture,
                ShadeAffinity = shade,
                ArcaneAffinity = arcane,
                MinArcaneSaturation = minArcane,
                SlopeTolerance = slope,
            };
        }
    }
}
