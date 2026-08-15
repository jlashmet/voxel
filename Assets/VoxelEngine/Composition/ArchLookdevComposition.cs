using System;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;
using VoxelEngine.Structures.Runtime;

namespace VoxelEngine.Composition
{
    /// <summary>
    /// Application-owned parameters for the hero-arch lookdev bench. These are UI values, not
    /// Structures.Runtime feature types; Composition translates them into the concrete authoring
    /// implementation.
    /// </summary>
    public readonly struct ArchLookdevSettings
    {
        public readonly int ClearSpan;
        public readonly int PierHeight;
        public readonly int RingThickness;
        public readonly int Depth;
        public readonly int VoussoirCount;
        public readonly int ShoulderWidth;
        public readonly int TopMargin;
        public readonly int FaceRecess;
        public readonly int PlinthHeight;
        public readonly int ImpostHeight;
        public readonly int Damage;
        public readonly int DamageSeedOffset;
        public readonly int DamageScale;
        public readonly byte JointHalfWidthQ4;
        public readonly byte BevelQ4;
        public readonly sbyte ProjectionQ4;
        public readonly byte FaceDepthQ4;
        public readonly byte MossCoverage;
        public readonly byte MossDensity;
        public readonly byte MossRadiusQ4;
        public readonly byte MossHeightQ4;
        public readonly byte MossDropQ4;
        public readonly byte MossSeparation;

        public ArchLookdevSettings(
            int clearSpan,
            int pierHeight,
            int ringThickness,
            int depth,
            int voussoirCount,
            int shoulderWidth,
            int topMargin,
            int faceRecess,
            int plinthHeight,
            int impostHeight,
            int damage,
            int damageSeedOffset,
            int damageScale,
            byte jointHalfWidthQ4,
            byte bevelQ4,
            sbyte projectionQ4,
            byte faceDepthQ4,
            byte mossCoverage,
            byte mossDensity,
            byte mossRadiusQ4,
            byte mossHeightQ4,
            byte mossDropQ4,
            byte mossSeparation)
        {
            ClearSpan = clearSpan;
            PierHeight = pierHeight;
            RingThickness = ringThickness;
            Depth = depth;
            VoussoirCount = voussoirCount;
            ShoulderWidth = shoulderWidth;
            TopMargin = topMargin;
            FaceRecess = faceRecess;
            PlinthHeight = plinthHeight;
            ImpostHeight = impostHeight;
            Damage = damage;
            DamageSeedOffset = damageSeedOffset;
            DamageScale = damageScale;
            JointHalfWidthQ4 = jointHalfWidthQ4;
            BevelQ4 = bevelQ4;
            ProjectionQ4 = projectionQ4;
            FaceDepthQ4 = faceDepthQ4;
            MossCoverage = mossCoverage;
            MossDensity = mossDensity;
            MossRadiusQ4 = mossRadiusQ4;
            MossHeightQ4 = mossHeightQ4;
            MossDropQ4 = mossDropQ4;
            MossSeparation = mossSeparation;
        }
    }

    /// <summary>
    /// Composition-owned lifetime for the lookdev world. The scene sees only Composition and
    /// stable API capabilities; concrete structure/storage implementations never escape.
    /// </summary>
    public sealed class ArchLookdevWorld : IDisposable
    {
        internal ArchLookdevWorld(
            IVoxelStorageRuntime storage,
            IProfileBlockReadSource profileBlocks,
            int width,
            int height)
        {
            Storage = storage ?? throw new ArgumentNullException(nameof(storage));
            ProfileBlocks = profileBlocks ?? throw new ArgumentNullException(nameof(profileBlocks));
            Width = width;
            Height = height;
        }

        public IVoxelStorageRuntime Storage { get; }
        public IProfileBlockReadSource ProfileBlocks { get; }
        public int Width { get; }
        public int Height { get; }

        public void Dispose() => Storage.Dispose();
    }

    public static partial class StructuresComposition
    {
        private const byte ArchLookdevStoneMaterial = Mat.MasonryMedium;

        /// <summary>
        /// Builds the production-path hero arch while keeping feature emitters, profile storage,
        /// rasterisation and weathering inside the Composition/Structures.Runtime wiring root.
        /// </summary>
        public static ArchLookdevWorld CreateArchLookdevWorld(in ArchLookdevSettings settings)
        {
            IVoxelStorageRuntime storage = VoxelEngineBootstrap.CreateStorage(8, 24_000);
            try
            {
                const uint coatings = (1u << Coatings.Moss) | (1u << Coatings.Snow)
                                    | (1u << Coatings.Soot) | (1u << Coatings.Wet);
                storage.RegisterMaterial(
                    ArchLookdevStoneMaterial,
                    210,
                    DestructionClass.Crumble,
                    SurfaceStyles.MasonryJoint,
                    coatings);
                storage.ConfigureCoatingDecoration(
                    Coatings.Moss,
                    settings.MossDensity,
                    settings.MossRadiusQ4,
                    settings.MossHeightQ4,
                    settings.MossDropQ4,
                    settings.MossSeparation);

                var profiles = new ProfileBlockStore();
                var arch = new ArchFeatureDefinition
                {
                    ClearSpan = settings.ClearSpan,
                    PierHeight = settings.PierHeight,
                    RingThickness = settings.RingThickness,
                    Depth = settings.Depth,
                    VoussoirCount = settings.VoussoirCount,
                    JointRecessDepth = 1,
                    ProfileJointHalfWidthQ4 = settings.JointHalfWidthQ4,
                    ProfileBevelQ4 = settings.BevelQ4,
                    ProfileProjectionQ4 = settings.ProjectionQ4,
                    ProfileDepthQ4 = settings.FaceDepthQ4,
                    StoneMaterial = ArchLookdevStoneMaterial,
                    PierStyle = SurfaceStyles.MasonryJoint,
                    RingStyle = SurfaceStyles.MasonryJoint,
                };
                var bay = new ArchBayFeatureDefinition
                {
                    Arch = arch,
                    ShoulderWidth = settings.ShoulderWidth,
                    TopMargin = settings.TopMargin,
                    FaceRecess = settings.FaceRecess,
                    PlinthHeight = settings.PlinthHeight,
                    ImpostHeight = settings.ImpostHeight,
                    Damage = (ArchRuinDamage)settings.Damage,
                    DamageSeed = 0xA341u + (uint)settings.DamageSeedOffset,
                    DamageScale = (byte)settings.DamageScale,
                };

                int3 origin = new(-bay.Width / 2, 0, 0);
                using (var primitives = new NativeList<Primitive>(
                           bay.Metadata.MaxPrimitives,
                           Allocator.Temp))
                {
                    if (!bay.Emit(origin, primitives, profiles))
                        throw new InvalidOperationException("Arch parameters did not emit.");
                    RasterResult result = PrimitiveRasteriser.Rasterise(
                        primitives.AsArray(),
                        origin,
                        origin + bay.Metadata.Footprint,
                        storage.Reads,
                        storage.Mutations);
                    if (result.BudgetExceeded)
                        throw new InvalidOperationException("Arch exceeded the feature budget.");
                }

                var brush = new VoxelBrush(
                    storage.Reads,
                    storage.Mutations,
                    storage.MaterialAuthoring,
                    2_000_000);
                MasonryWeathering.CoatExposedSurfaces(
                    ref brush,
                    origin - 2,
                    bay.Metadata.Footprint + 4,
                    Coatings.Moss,
                    0xA341u + (uint)settings.DamageSeedOffset,
                    settings.MossCoverage,
                    dripPasses: 0);

                storage.PublishAllResidentRegions();
                return new ArchLookdevWorld(storage, profiles, bay.Width, bay.Height);
            }
            catch
            {
                storage.Dispose();
                throw;
            }
        }
    }
}
