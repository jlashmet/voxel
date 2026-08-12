using Unity.Mathematics;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>
    /// Controls how authoritative binary voxel occupancy becomes a derived visual surface.
    /// These values are presentation-only and never mutate voxel storage, collision, destruction,
    /// or networking state.
    ///
    /// BlurPasses is intentionally part of the material contract. A renderer must select the
    /// material's filtered field before applying distance recovery/smoothing; globally blurring all
    /// occupancy first destroys manufactured edges and cannot be repaired reliably afterwards.
    /// </summary>
    public readonly struct VoxelSurfaceProfile
    {
        public const int MaxSupportedBlurPasses = 2;

        public VoxelSurfaceProfile(float smoothing, int blurPasses = 2,
                                   float densityBias = 0f,
                                   float modificationStrength = 0f,
                                   float modificationScaleVoxels = 4f,
                                   float planarization = 0f,
                                   float planarizationThreshold = 0.88f,
                                   float distanceRecovery = 0f,
                                   float curveRecovery = 0f)
        {
            Smoothing = math.saturate(smoothing);
            BlurPasses = math.clamp(blurPasses, 0, MaxSupportedBlurPasses);
            DensityBias = math.clamp(densityBias, -0.45f, 0.45f);
            ModificationStrength = math.clamp(modificationStrength, 0f, 0.35f);
            ModificationScaleVoxels = math.max(0.25f, modificationScaleVoxels);
            Planarization = math.saturate(planarization);
            PlanarizationThreshold = math.clamp(planarizationThreshold, 0.58f, 0.995f);
            DistanceRecovery = math.saturate(distanceRecovery);
            CurveRecovery = math.saturate(curveRecovery);
        }

        /// <summary>0 = preserve occupancy; 1 = use the selected reconstructed visual field.</summary>
        public float Smoothing { get; }

        /// <summary>
        /// Number of low-pass density passes this material permits before reconstruction. Zero is
        /// appropriate for cut/manufactured materials; soft terrain generally uses two.
        /// </summary>
        public int BlurPasses { get; }

        /// <summary>Positive expands the visual surface; negative contracts it.</summary>
        public float DensityBias { get; }

        /// <summary>Amplitude of deterministic presentation-only geometric variation.</summary>
        public float ModificationStrength { get; }

        /// <summary>Approximate wavelength of geometric variation, measured in voxels.</summary>
        public float ModificationScaleVoxels { get; }

        /// <summary>Strength with which strongly axis-aligned faces return to exact voxel planes.</summary>
        public float Planarization { get; }

        /// <summary>Minimum dominant normal component before planarization begins.</summary>
        public float PlanarizationThreshold { get; }

        /// <summary>Blend from selected blur field toward local coverage/gradient distance.</summary>
        public float DistanceRecovery { get; }

        /// <summary>Blend between tight radius-one and broad radius-two distance recovery.</summary>
        public float CurveRecovery { get; }

        public static VoxelSurfaceProfile Legacy => new(
            smoothing: 0.92f,
            blurPasses: 2);

        public static VoxelSurfaceProfile SoftTerrain => new(
            smoothing: 0.94f,
            blurPasses: 2,
            distanceRecovery: 0.10f,
            curveRecovery: 0.35f);

        public static VoxelSurfaceProfile HardManufactured => new(
            smoothing: 0.72f,
            blurPasses: 0,
            planarization: 0.62f,
            planarizationThreshold: 0.82f,
            distanceRecovery: 0.72f,
            curveRecovery: 0.18f);

        /// <summary>
        /// Cut stone bypasses occupancy blur completely. Coverage-distance recovery gives the arch
        /// a continuous curve while aggressive planarization returns broad ashlar faces to planes.
        /// This is intentionally a very different reconstruction from dirt/terrain.
        /// </summary>
        public static VoxelSurfaceProfile DressedStone => new(
            smoothing: 1.0f,
            blurPasses: 0,
            densityBias: -0.004f,
            planarization: 0.94f,
            planarizationThreshold: 0.74f,
            distanceRecovery: 1.0f,
            curveRecovery: 0.70f);

        public static VoxelSurfaceProfile RecessedMasonryJoint => new(
            smoothing: 1.0f,
            blurPasses: 0,
            densityBias: -0.085f,
            planarization: 0.94f,
            planarizationThreshold: 0.74f,
            distanceRecovery: 1.0f,
            curveRecovery: 0.70f);

        public static VoxelSurfaceProfile RoughRock => new(
            smoothing: 0.82f,
            blurPasses: 1,
            modificationStrength: 0.035f,
            modificationScaleVoxels: 5.5f,
            distanceRecovery: 0.48f,
            curveRecovery: 0.55f);
    }

    /// <summary>
    /// Dense material-id lookup used by every derived surface renderer. This is the canonical
    /// rendering/reconstruction contract for the current 0..17 material palette. Renderers should
    /// consume this table by material id rather than containing Stone/Dirt/etc conditionals.
    /// </summary>
    public sealed class VoxelSurfaceProfileSet
    {
        private readonly VoxelSurfaceProfile[] _profiles = new VoxelSurfaceProfile[256];

        public VoxelSurfaceProfileSet() : this(VoxelSurfaceProfile.Legacy) { }

        public VoxelSurfaceProfileSet(VoxelSurfaceProfile defaultProfile)
        {
            for (int i = 0; i < _profiles.Length; i++)
                _profiles[i] = defaultProfile;
        }

        public VoxelSurfaceProfile Get(byte material) => _profiles[material];

        public VoxelSurfaceProfileSet Set(byte material, VoxelSurfaceProfile profile)
        {
            _profiles[material] = profile;
            return this;
        }

        /// <summary>
        /// Canonical surface behavior for the palette defined by material ids 0..17. Keeping this
        /// next to the profile type means CPU/GPU renderers can share one contract without depending
        /// on showcase or structure-generation assemblies.
        /// </summary>
        public static VoxelSurfaceProfileSet Canonical()
        {
            var set = new VoxelSurfaceProfileSet(VoxelSurfaceProfile.Legacy);

            set.Set(0,  VoxelSurfaceProfile.Legacy);             // empty: inherited from nearest solid at boundaries
            set.Set(1,  VoxelSurfaceProfile.DressedStone);       // stone
            set.Set(2,  VoxelSurfaceProfile.HardManufactured);   // wood
            set.Set(3,  VoxelSurfaceProfile.SoftTerrain);        // sand
            set.Set(4,  VoxelSurfaceProfile.HardManufactured);   // glass
            set.Set(5,  VoxelSurfaceProfile.RoughRock);          // bedrock
            set.Set(6,  VoxelSurfaceProfile.RecessedMasonryJoint);// dark stone / mortar in current masonry kit
            set.Set(7,  VoxelSurfaceProfile.HardManufactured);   // slate
            set.Set(8,  VoxelSurfaceProfile.HardManufactured);   // tile
            set.Set(9,  new VoxelSurfaceProfile(0.55f, 1));       // cloth
            set.Set(10, VoxelSurfaceProfile.SoftTerrain);        // grass
            set.Set(11, new VoxelSurfaceProfile(0.96f, 2));       // water
            set.Set(12, VoxelSurfaceProfile.HardManufactured);   // gold
            set.Set(13, VoxelSurfaceProfile.SoftTerrain);        // dirt
            set.Set(14, new VoxelSurfaceProfile(0.90f, 2, modificationStrength: 0.018f,
                                                 modificationScaleVoxels: 4.5f)); // moss
            set.Set(15, VoxelSurfaceProfile.HardManufactured);   // lit window
            set.Set(16, new VoxelSurfaceProfile(0.96f, 2));       // cascade
            set.Set(17, new VoxelSurfaceProfile(0.70f, 1,
                                                 distanceRecovery: 0.55f,
                                                 curveRecovery: 0.45f)); // crystal
            return set;
        }
    }
}
