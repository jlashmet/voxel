using Game.Materials.Api;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.ModelViewer
{
    /// <summary>
    /// Reuses the exact production authoring path and hero defaults from ArchLookdev without
    /// retaining the arch-specific UI/automation surface in the generic Model Viewer.
    /// </summary>
    internal static class ModelViewerArchAdapter
    {
        private const uint ArchSeed = 0xA341u + 0x2222u;
        public const int Depth = 12;

        public static ArchLookdevBuildResult Author(IVoxelStorageRuntime storage)
        {
            const uint coatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                | (1u << Coatings.Soot) | (1u << Coatings.Wet);
            storage.RegisterMaterial(
                GameMaterialIds.MasonryMedium,
                210,
                DestructionClass.Crumble,
                SurfaceStyles.MasonryJoint,
                coatings);
            storage.ConfigureCoatingDecoration(
                Coatings.Moss,
                density: 128,
                radiusQ4: 14,
                heightQ4: 2,
                dropQ4: 10,
                separation: 0);

            var request = new ArchLookdevBuildRequest
            {
                ClearSpan = 28,
                PierHeight = 64,
                RingThickness = 7,
                Depth = Depth,
                VoussoirCount = 13,
                ShoulderWidth = 4,
                TopMargin = 4,
                FaceRecess = 1,
                PlinthHeight = 4,
                ImpostHeight = 3,
                Damage = 0,
                DamageSeed = ArchSeed,
                DamageScale = 2,
                ProfileJointHalfWidthQ4 = 4,
                ProfileBevelQ4 = 4,
                ProfileProjectionQ4 = 8,
                ProfileDepthQ4 = 16,
                StoneMaterial = GameMaterialIds.MasonryMedium,
                SurfaceStyle = SurfaceStyles.MasonryJoint,
                Coating = Coatings.Moss,
                CoatingCoverage = 48,
                BrushBudget = 2_000_000,
            };
            return StructuresComposition.BuildArchLookdev(storage, in request);
        }
    }
}
