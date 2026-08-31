using Game.Structures.Api;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Architecture;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Scene-composition adapter from the game-owned CastlePlan to renderer-neutral far-world facts.
    /// CastlePlan voxels are decimetres, so no voxel storage or physical realization is required.
    /// </summary>
    public static class ShowcaseCastleFarPresentation
    {
        public const string ProxyKey = "castle";

        public static StructureFarPresentation FromPlan(in CastlePlan plan)
        {
            int horizontalReach = System.Math.Max(
                plan.BaileyHalfX + plan.TowerRadius,
                plan.KeepHalfX);
            int depthReach = System.Math.Max(
                plan.BaileyHalfZ + plan.TowerRadius,
                plan.KeepHalfZ);
            int heightDm = System.Math.Max(
                1,
                plan.PlateauHeight + System.Math.Max(
                    plan.KeepHeight,
                    System.Math.Max(plan.TowerHeight, plan.GateTowerHeight)));

            ulong settlementKey = HashString(FnvOffset, "showcase-landmarks");
            settlementKey = HashUInt(settlementKey, plan.Seed);
            ulong structureKey = HashString(settlementKey, "castle");

            ulong architectureKey = HashString(FnvOffset, "showcase-castle");
            ulong materialFamilyKey = HashString(FnvOffset, "showcase-castle-masonry");

            ulong revision = structureKey;
            revision = HashInt(revision, plan.Centre.x);
            revision = HashInt(revision, plan.Centre.y);
            revision = HashInt(revision, plan.Centre.z);
            revision = HashInt(revision, plan.PlateauRadius);
            revision = HashInt(revision, plan.PlateauHeight);
            revision = HashInt(revision, plan.CliffDrop);
            revision = HashInt(revision, plan.BaileyHalfX);
            revision = HashInt(revision, plan.BaileyHalfZ);
            revision = HashInt(revision, plan.WallHeight);
            revision = HashInt(revision, plan.WallThickness);
            revision = HashInt(revision, plan.TowerRadius);
            revision = HashInt(revision, plan.TowerHeight);
            revision = HashInt(revision, plan.GateTowerRadius);
            revision = HashInt(revision, plan.GateTowerHeight);
            revision = HashInt(revision, plan.KeepHalfX);
            revision = HashInt(revision, plan.KeepHalfZ);
            revision = HashInt(revision, plan.KeepHeight);
            revision = HashUInt(revision, plan.Seed);

            return new StructureFarPresentation(
                structureKey,
                settlementKey,
                new Int2(plan.Centre.x - horizontalReach, plan.Centre.z - depthReach),
                new Int2(plan.Centre.x + horizontalReach + 1, plan.Centre.z + depthReach + 1),
                heightDm,
                FrontageDirection.South,
                // Castle is a game-specific landmark vocabulary. The generic archetype remains a
                // broad exterior-massing hint; ProxyKey above is the explicit composition mapping.
                StructureArchetype.Mansion,
                architectureKey,
                materialFamilyKey,
                StructureVisibilityClass.HorizonLandmark,
                revision);
        }

        private const ulong FnvOffset = 14695981039346656037UL;
        private const ulong FnvPrime = 1099511628211UL;

        private static ulong HashString(ulong hash, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                ushort c = value[i];
                hash = HashByte(hash, (byte)c);
                hash = HashByte(hash, (byte)(c >> 8));
            }
            return HashByte(hash, 0xFF);
        }

        private static ulong HashInt(ulong hash, int value) =>
            HashUInt(hash, unchecked((uint)value));

        private static ulong HashUInt(ulong hash, uint value)
        {
            for (int shift = 0; shift < 32; shift += 8)
                hash = HashByte(hash, (byte)(value >> shift));
            return hash;
        }

        private static ulong HashByte(ulong hash, byte value)
        {
            hash ^= value;
            return hash * FnvPrime;
        }
    }
}
