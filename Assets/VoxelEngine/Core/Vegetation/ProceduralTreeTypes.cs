using Unity.Mathematics;

namespace VoxelEngine.Core.Vegetation
{
    public enum TreeSpecies : byte
    {
        Oak,
        Pine,
        Birch,
        Maple,
        Willow,
        Sakura,
        Dead,
    }

    public enum TreeLeafStyle : byte
    {
        Broad,
        Needle,
        Narrow,
        Blossom,
        None,
    }

    /// <summary>
    /// Authoritative semantic identity for one procedural tree. Geometry, LOD meshes and foliage
    /// are derived from this tuple; the same world seed therefore produces the same tree on every
    /// client without serialising a render mesh.
    /// </summary>
    public struct TreeInstance
    {
        public float3 PositionMetres;
        public TreeSpecies Species;
        public uint Seed;
        public float Scale;
    }

    /// <summary>
    /// Species grammar rather than a prefab. Ranges are sampled deterministically per instance.
    /// Branch parameters describe a shared skeleton from which every visual LOD is derived.
    /// </summary>
    public struct TreeSpeciesProfile
    {
        public float HeightMin;
        public float HeightMax;
        public float TrunkRadiusMin;
        public float TrunkRadiusMax;
        public float TrunkTaper;

        public int BranchLevels;
        public int PrimaryBranches;
        public int ChildBranches;
        public float BranchStart;
        public float BranchAngleMin;
        public float BranchAngleMax;
        public float BranchLengthFactor;
        public float BranchLengthDecay;
        public float BranchRadiusDecay;
        public float UpwardBias;
        public float Droop;
        public float Gnarliness;

        public TreeLeafStyle LeafStyle;
        public int LeavesPerTip;
        public float LeafSize;
        public float LeafSizeVariance;
        public float LeafSpread;

        public float4 BarkColour;
        public float4 BarkColourSecondary;
        public float4 LeafColourA;
        public float4 LeafColourB;

        public float MidHeight => (HeightMin + HeightMax) * 0.5f;
    }

    public static class TreeSpeciesProfiles
    {
        public static TreeSpeciesProfile Get(TreeSpecies species)
        {
            switch (species)
            {
                case TreeSpecies.Pine:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 12f, HeightMax = 22f,
                        TrunkRadiusMin = 0.24f, TrunkRadiusMax = 0.48f, TrunkTaper = 0.18f,
                        BranchLevels = 2, PrimaryBranches = 16, ChildBranches = 2,
                        BranchStart = 0.20f, BranchAngleMin = 58f, BranchAngleMax = 82f,
                        BranchLengthFactor = 0.34f, BranchLengthDecay = 0.55f,
                        BranchRadiusDecay = 0.48f, UpwardBias = 0.08f, Droop = 0.09f,
                        Gnarliness = 0.045f,
                        LeafStyle = TreeLeafStyle.Needle, LeavesPerTip = 10,
                        LeafSize = 0.42f, LeafSizeVariance = 0.18f, LeafSpread = 0.75f,
                        BarkColour = new float4(0.28f, 0.18f, 0.09f, 1f),
                        BarkColourSecondary = new float4(0.18f, 0.11f, 0.06f, 1f),
                        LeafColourA = new float4(0.10f, 0.26f, 0.10f, 1f),
                        LeafColourB = new float4(0.18f, 0.35f, 0.14f, 1f),
                    };

                case TreeSpecies.Birch:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 10f, HeightMax = 18f,
                        TrunkRadiusMin = 0.16f, TrunkRadiusMax = 0.31f, TrunkTaper = 0.24f,
                        BranchLevels = 3, PrimaryBranches = 10, ChildBranches = 2,
                        BranchStart = 0.32f, BranchAngleMin = 34f, BranchAngleMax = 58f,
                        BranchLengthFactor = 0.31f, BranchLengthDecay = 0.58f,
                        BranchRadiusDecay = 0.46f, UpwardBias = 0.22f, Droop = 0.05f,
                        Gnarliness = 0.085f,
                        LeafStyle = TreeLeafStyle.Broad, LeavesPerTip = 8,
                        LeafSize = 0.34f, LeafSizeVariance = 0.20f, LeafSpread = 0.75f,
                        BarkColour = new float4(0.72f, 0.70f, 0.63f, 1f),
                        BarkColourSecondary = new float4(0.28f, 0.25f, 0.20f, 1f),
                        LeafColourA = new float4(0.34f, 0.53f, 0.18f, 1f),
                        LeafColourB = new float4(0.52f, 0.66f, 0.27f, 1f),
                    };

                case TreeSpecies.Maple:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 9f, HeightMax = 16f,
                        TrunkRadiusMin = 0.25f, TrunkRadiusMax = 0.52f, TrunkTaper = 0.28f,
                        BranchLevels = 3, PrimaryBranches = 9, ChildBranches = 3,
                        BranchStart = 0.25f, BranchAngleMin = 42f, BranchAngleMax = 68f,
                        BranchLengthFactor = 0.39f, BranchLengthDecay = 0.58f,
                        BranchRadiusDecay = 0.48f, UpwardBias = 0.13f, Droop = 0.08f,
                        Gnarliness = 0.13f,
                        LeafStyle = TreeLeafStyle.Broad, LeavesPerTip = 11,
                        LeafSize = 0.43f, LeafSizeVariance = 0.22f, LeafSpread = 0.95f,
                        BarkColour = new float4(0.31f, 0.22f, 0.14f, 1f),
                        BarkColourSecondary = new float4(0.20f, 0.13f, 0.08f, 1f),
                        LeafColourA = new float4(0.32f, 0.48f, 0.14f, 1f),
                        LeafColourB = new float4(0.63f, 0.35f, 0.10f, 1f),
                    };

                case TreeSpecies.Willow:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 8f, HeightMax = 15f,
                        TrunkRadiusMin = 0.30f, TrunkRadiusMax = 0.60f, TrunkTaper = 0.27f,
                        BranchLevels = 3, PrimaryBranches = 9, ChildBranches = 3,
                        BranchStart = 0.20f, BranchAngleMin = 48f, BranchAngleMax = 76f,
                        BranchLengthFactor = 0.48f, BranchLengthDecay = 0.63f,
                        BranchRadiusDecay = 0.44f, UpwardBias = 0.04f, Droop = 0.40f,
                        Gnarliness = 0.15f,
                        LeafStyle = TreeLeafStyle.Narrow, LeavesPerTip = 12,
                        LeafSize = 0.48f, LeafSizeVariance = 0.18f, LeafSpread = 1.15f,
                        BarkColour = new float4(0.32f, 0.27f, 0.16f, 1f),
                        BarkColourSecondary = new float4(0.21f, 0.18f, 0.11f, 1f),
                        LeafColourA = new float4(0.30f, 0.48f, 0.18f, 1f),
                        LeafColourB = new float4(0.47f, 0.60f, 0.25f, 1f),
                    };

                case TreeSpecies.Sakura:
                    return new TreeSpeciesProfile
                    {
                        // Japanese cherry: lower, spreading crown with graceful asymmetric limbs.
                        HeightMin = 6f, HeightMax = 12f,
                        TrunkRadiusMin = 0.24f, TrunkRadiusMax = 0.46f, TrunkTaper = 0.31f,
                        BranchLevels = 3, PrimaryBranches = 8, ChildBranches = 3,
                        BranchStart = 0.16f, BranchAngleMin = 48f, BranchAngleMax = 76f,
                        BranchLengthFactor = 0.51f, BranchLengthDecay = 0.61f,
                        BranchRadiusDecay = 0.47f, UpwardBias = 0.10f, Droop = 0.16f,
                        Gnarliness = 0.22f,
                        LeafStyle = TreeLeafStyle.Blossom, LeavesPerTip = 15,
                        LeafSize = 0.30f, LeafSizeVariance = 0.25f, LeafSpread = 0.90f,
                        BarkColour = new float4(0.31f, 0.20f, 0.17f, 1f),
                        BarkColourSecondary = new float4(0.19f, 0.11f, 0.10f, 1f),
                        LeafColourA = new float4(1.00f, 0.61f, 0.73f, 1f),
                        LeafColourB = new float4(1.00f, 0.82f, 0.88f, 1f),
                    };

                case TreeSpecies.Dead:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 7f, HeightMax = 14f,
                        TrunkRadiusMin = 0.27f, TrunkRadiusMax = 0.55f, TrunkTaper = 0.34f,
                        BranchLevels = 3, PrimaryBranches = 7, ChildBranches = 2,
                        BranchStart = 0.22f, BranchAngleMin = 38f, BranchAngleMax = 72f,
                        BranchLengthFactor = 0.41f, BranchLengthDecay = 0.58f,
                        BranchRadiusDecay = 0.46f, UpwardBias = 0.10f, Droop = 0.18f,
                        Gnarliness = 0.30f,
                        LeafStyle = TreeLeafStyle.None, LeavesPerTip = 0,
                        LeafSize = 0f, LeafSizeVariance = 0f, LeafSpread = 0f,
                        BarkColour = new float4(0.24f, 0.19f, 0.14f, 1f),
                        BarkColourSecondary = new float4(0.13f, 0.11f, 0.09f, 1f),
                        LeafColourA = new float4(0f), LeafColourB = new float4(0f),
                    };

                case TreeSpecies.Oak:
                default:
                    return new TreeSpeciesProfile
                    {
                        HeightMin = 9f, HeightMax = 16f,
                        TrunkRadiusMin = 0.32f, TrunkRadiusMax = 0.68f, TrunkTaper = 0.30f,
                        BranchLevels = 3, PrimaryBranches = 9, ChildBranches = 3,
                        BranchStart = 0.20f, BranchAngleMin = 44f, BranchAngleMax = 70f,
                        BranchLengthFactor = 0.43f, BranchLengthDecay = 0.60f,
                        BranchRadiusDecay = 0.49f, UpwardBias = 0.12f, Droop = 0.10f,
                        Gnarliness = 0.18f,
                        LeafStyle = TreeLeafStyle.Broad, LeavesPerTip = 11,
                        LeafSize = 0.42f, LeafSizeVariance = 0.20f, LeafSpread = 1.0f,
                        BarkColour = new float4(0.30f, 0.22f, 0.13f, 1f),
                        BarkColourSecondary = new float4(0.18f, 0.13f, 0.08f, 1f),
                        LeafColourA = new float4(0.24f, 0.43f, 0.14f, 1f),
                        LeafColourB = new float4(0.39f, 0.56f, 0.20f, 1f),
                    };
            }
        }
    }
}
