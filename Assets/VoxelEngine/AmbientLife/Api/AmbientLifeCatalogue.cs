namespace VoxelEngine.AmbientLife.Api
{
    public static class AmbientLifeCatalogue
    {
        public const int Count = 16;

        public static AmbientLifeKind KindAt(int index)
        {
            return (AmbientLifeKind)index;
        }

        public static bool HasTrait(AmbientLifeKind kind, AmbientLifeTraits trait)
        {
            return (Get(kind).Traits & trait) != 0;
        }

        public static AmbientLifeProfile Get(AmbientLifeKind kind)
        {
            switch (kind)
            {
                case AmbientLifeKind.Butterfly:
                    return P(kind, AmbientMovementForm.Flutter, AmbientActivity.Day,
                        AmbientLifeTraits.Pollinator | AmbientLifeTraits.Flying,
                        0.20f, flower: 1f, shade: -0.30f, moisture: 0.10f, min: 2, max: 8);
                case AmbientLifeKind.Bee:
                    return P(kind, AmbientMovementForm.Orbit, AmbientActivity.Day,
                        AmbientLifeTraits.Pollinator | AmbientLifeTraits.Flying | AmbientLifeTraits.Audible,
                        0.18f, flower: 1f, shade: -0.35f, min: 3, max: 10);
                case AmbientLifeKind.Moth:
                    return P(kind, AmbientMovementForm.Flutter, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Flying,
                        0.16f, flower: 0.30f, shade: 0.55f, moisture: 0.20f, min: 2, max: 7);
                case AmbientLifeKind.Dragonfly:
                    return P(kind, AmbientMovementForm.Dart, AmbientActivity.Day,
                        AmbientLifeTraits.Flying | AmbientLifeTraits.WaterAssociated,
                        0.08f, water: 1f, moisture: 0.65f, shade: -0.20f, min: 1, max: 5);
                case AmbientLifeKind.Beetle:
                    return P(kind, AmbientMovementForm.GroundScuttle, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.None,
                        0.10f, moisture: 0.45f, deadwood: 0.70f, shade: 0.35f, min: 1, max: 5);
                case AmbientLifeKind.Cricket:
                    return P(kind, AmbientMovementForm.GroundScuttle, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Audible,
                        0.14f, moisture: 0.10f, shade: 0.20f, min: 2, max: 8);
                case AmbientLifeKind.Frog:
                    return P(kind, AmbientMovementForm.Hop, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Audible | AmbientLifeTraits.WaterAssociated,
                        0.04f, water: 1f, moisture: 1f, shade: 0.25f, min: 1, max: 4);
                case AmbientLifeKind.Songbird:
                    return P(kind, AmbientMovementForm.Flock, AmbientActivity.Day,
                        AmbientLifeTraits.Flying | AmbientLifeTraits.Audible,
                        0.05f, shade: 0.15f, flower: 0.10f, min: 1, max: 4);
                case AmbientLifeKind.Bat:
                    return P(kind, AmbientMovementForm.Flock, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Flying | AmbientLifeTraits.Audible,
                        0.04f, shade: 0.60f, water: 0.20f, min: 2, max: 7);
                case AmbientLifeKind.SporeMote:
                    return P(kind, AmbientMovementForm.Drift, AmbientActivity.All,
                        AmbientLifeTraits.None,
                        0.03f, fungus: 1f, moisture: 0.70f, shade: 0.70f, min: 4, max: 18);
                case AmbientLifeKind.GlowMoth:
                    return P(kind, AmbientMovementForm.Flutter, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Magical | AmbientLifeTraits.Luminous | AmbientLifeTraits.Flying,
                        0.02f, flower: 0.35f, shade: 0.45f, arcane: 1f, minArcane: 0.35f, min: 2, max: 9);
                case AmbientLifeKind.Wisp:
                    return P(kind, AmbientMovementForm.HoverSwarm, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Magical | AmbientLifeTraits.Luminous | AmbientLifeTraits.Flying,
                        0.015f, moisture: 0.25f, shade: 0.40f, arcane: 1f, minArcane: 0.55f, min: 1, max: 5);
                case AmbientLifeKind.Emberfly:
                    return P(kind, AmbientMovementForm.Dart, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Magical | AmbientLifeTraits.Luminous | AmbientLifeTraits.Flying,
                        0.015f, shade: -0.35f, moisture: -0.70f, arcane: 0.90f, minArcane: 0.45f, min: 3, max: 12);
                case AmbientLifeKind.ManaButterfly:
                    return P(kind, AmbientMovementForm.Flutter, AmbientActivity.Day | AmbientActivity.Dusk,
                        AmbientLifeTraits.Magical | AmbientLifeTraits.Luminous | AmbientLifeTraits.Pollinator | AmbientLifeTraits.Flying,
                        0.015f, flower: 0.90f, shade: -0.15f, arcane: 1f, minArcane: 0.45f, min: 1, max: 6);
                case AmbientLifeKind.SeedLight:
                    return P(kind, AmbientMovementForm.Drift, AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Magical | AmbientLifeTraits.Luminous | AmbientLifeTraits.Flying,
                        0.012f, flower: 0.20f, shade: 0.20f, arcane: 1f, minArcane: 0.50f, min: 3, max: 14);
                case AmbientLifeKind.Firefly:
                default:
                    return P(AmbientLifeKind.Firefly, AmbientMovementForm.HoverSwarm,
                        AmbientActivity.Dusk | AmbientActivity.Night,
                        AmbientLifeTraits.Luminous | AmbientLifeTraits.Flying | AmbientLifeTraits.WaterAssociated,
                        0.12f, moisture: 0.80f, water: 0.50f, shade: 0.40f, min: 4, max: 16);
            }
        }

        private static AmbientLifeProfile P(
            AmbientLifeKind kind,
            AmbientMovementForm movement,
            AmbientActivity activity,
            AmbientLifeTraits traits,
            float baseWeight,
            float moisture = 0f,
            float shade = 0f,
            float flower = 0f,
            float water = 0f,
            float fungus = 0f,
            float deadwood = 0f,
            float arcane = 0f,
            float minArcane = 0f,
            ushort min = 1,
            ushort max = 4)
        {
            return new AmbientLifeProfile
            {
                Kind = kind,
                Movement = movement,
                Activity = activity,
                Traits = traits,
                BaseWeight = baseWeight,
                MoistureAffinity = moisture,
                ShadeAffinity = shade,
                FlowerAffinity = flower,
                WaterAffinity = water,
                FungusAffinity = fungus,
                DeadwoodAffinity = deadwood,
                ArcaneAffinity = arcane,
                MinArcaneSaturation = minArcane,
                MinCount = min,
                MaxCount = max,
            };
        }
    }
}
