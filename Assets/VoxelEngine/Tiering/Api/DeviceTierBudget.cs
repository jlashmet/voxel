using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace VoxelEngine.Tiering.Api
{
    /// <summary>
    /// Device tier classification used to select presentation-only rendering budgets.
    ///
    /// Only three tiers exist — PC, Console, and Mobile-HE (Constitution Principle IV: platform
    /// scope is PC, console, and high-end mobile only). Mid-tier and low-tier phones are out of
    /// scope. Tier selection drives *presentation parameters* exclusively: pool capacity, detail
    /// radius, render scale, probe spacing, debris count, and view distance.
    ///
    /// The following fields are STRUCTURALLY ABSENT from this type because they must never tier:
    ///   - interestRadius (the specific C-006 trap)
    ///   - tick rate
    ///   - reconciliation window
    ///   - collision parameters
    ///   - hit resolution
    ///   - world state simulation
    ///   - any Core integer job parameter
    /// </summary>
    public enum DeviceTier
    {
        /// <summary>Discrete GPU, 8 GB VRAM or more. Vulkan 1.2 / DX12.</summary>
        PC = 0,

        /// <summary>Current-generation consoles. Platform-native API.</summary>
        Console = 1,

        /// <summary>Flagship phones released within ~3 years, tile-based GPU. Vulkan 1.1+ / Metal 3.</summary>
        MobileHE = 2,
    }

    /// <summary>
    /// Presentation-only tier budgets derived from device-matrix.md values.
    ///
    /// Constitution Principle IV (Tiering boundary): device class affects presentation parameters
    /// ONLY — never interest radius, tick rate, collision, world state, or any Core job. The fields
    /// in this type are exclusively presentation parameters:
    ///   - brickPoolCapacity       (memory budget for mixed bricks)
    ///   - detailRadius            (full-detail mip-0 render radius)
    ///   - renderScale             (render resolution fraction, 0.75 on Mobile-HE)
    ///   - probeSpacing            (irradiance probe placement spacing in world units)
    ///   - maxDebris               (maximum visual-only debris bodies)
    ///   - maxViewDistance         (farthest distance bricks are rendered)
    ///   - surfaceGeometryBudget   (GPU bytes for extracted surface geometry)
    /// </summary>
    public readonly struct DeviceTierBudget
    {
        /// <summary>Brick pool capacity in bytes — 5.0 GB on PC, 1.0 GB on Console, 384 MB on Mobile-HE.</summary>
        public readonly long BrickPoolCapacity;

        /// <summary>Full-detail radius (mip-0) in metres — device-matrix.md LOD values.</summary>
        public readonly int DetailRadius;

        /// <summary>Render resolution scale fraction — 1.0 on PC/Console, 0.75 on Mobile-HE.</summary>
        public readonly float RenderScale;

        /// <summary>Irradiance probe spacing in world units — 2 m on PC/Console, 4 m on Mobile-HE.</summary>
        public readonly float ProbeSpacing;

        /// <summary>Maximum visual-only debris bodies — 2000 on PC, 1500 on Console, 400 on Mobile-HE.</summary>
        public readonly int MaxDebris;

        /// <summary>Max view distance in metres — 10 km on PC/Console, 6 km on Mobile-HE (device-matrix.md).</summary>
        public readonly int MaxViewDistance;

        /// <summary>Mip transition distance start — where mip-based rendering begins (device-matrix.md).</summary>
        public readonly int MipTransitionStart;

        /// <summary>Implicit far-field distance — beyond this, only mip textures are used (device-matrix.md).</summary>
        public readonly int FarFieldStart;

        /// <summary>
        /// GPU bytes for the shared extracted-surface geometry arena — device-matrix.md.
        ///
        /// This is derived presentation only: it holds Transvoxel output, never authoritative voxel
        /// state, so tiering it does not touch the tiering boundary. It has to be sized against the
        /// full-detail ring rather than guessed. At 0.1 m voxels a 64-cell chunk spans 6.4 m and its
        /// faceted surface averages ~12.5 K vertices, and the innermost ring alone puts ~1.5 K chunks
        /// in a production frustum. Undersizing it does not degrade gracefully: publication fails to
        /// acquire a lease and the view keeps permanent holes where geometry never lands.
        /// </summary>
        public readonly long SurfaceGeometryBudget;

        /// <summary>Construct a tier budget with all fields explicitly set.</summary>
        public DeviceTierBudget(
            long brickPoolCapacity,
            int detailRadius,
            float renderScale,
            float probeSpacing,
            int maxDebris,
            int maxViewDistance,
            int mipTransitionStart,
            int farFieldStart,
            long surfaceGeometryBudget)
        {
            BrickPoolCapacity = brickPoolCapacity;
            DetailRadius = detailRadius;
            RenderScale = renderScale;
            ProbeSpacing = probeSpacing;
            MaxDebris = maxDebris;
            MaxViewDistance = maxViewDistance;
            MipTransitionStart = mipTransitionStart;
            FarFieldStart = farFieldStart;
            SurfaceGeometryBudget = surfaceGeometryBudget;
        }

        /// <summary>Get the tier budget for a given device class. Values from device-matrix.md.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DeviceTierBudget GetForTier(DeviceTier tier)
        {
            return tier switch
            {
                DeviceTier.PC => new DeviceTierBudget(
                    brickPoolCapacity:  5_000_000_000L,   // 5.0 GB
                    detailRadius:       400,               // 400 m full-detail radius
                    renderScale:        1.0f,              // Native resolution
                    probeSpacing:       2f,                // 2 m probe spacing
                    maxDebris:          2000,              // 2000 visual-only debris bodies
                    maxViewDistance:    10000,             // 10 km
                    mipTransitionStart: 400,               // Start mip transition at 400 m
                    farFieldStart:      1200,              // Implicit far-field beyond 1200 m
                    surfaceGeometryBudget: 1_073_741_824L  // 1.0 GB of extracted surface geometry
                ),

                DeviceTier.Console => new DeviceTierBudget(
                    brickPoolCapacity:  1_073_741_824L,    // 1.0 GB
                    detailRadius:       350,               // 350 m full-detail radius
                    renderScale:        1.0f,              // Native resolution
                    probeSpacing:       2f,                // 2 m probe spacing
                    maxDebris:          1500,              // 1500 visual-only debris bodies
                    maxViewDistance:    10000,             // 10 km
                    mipTransitionStart: 350,               // Start mip transition at 350 m
                    farFieldStart:      1000,              // Implicit far-field beyond 1000 m
                    surfaceGeometryBudget: 536_870_912L  // 512 MB of extracted surface geometry
                ),

                DeviceTier.MobileHE => new DeviceTierBudget(
                    brickPoolCapacity:  402_653_184L,      // 384 MB
                    detailRadius:       200,               // 200 m full-detail radius
                    renderScale:        0.75f,             // 0.75 scale + upscale (device-matrix.md)
                    probeSpacing:       4f,                // 4 m probe spacing (half density of PC/Console)
                    maxDebris:          400,               // 400 visual-only debris bodies (device-matrix.md)
                    maxViewDistance:    6000,              // 6 km max view distance
                    mipTransitionStart: 200,               // Start mip transition at 200 m
                    farFieldStart:      600,               // Implicit far-field beyond 600 m
                    surfaceGeometryBudget: 268_435_456L  // 256 MB of extracted surface geometry
                ),

                _ => throw new ArgumentOutOfRangeException(nameof(tier), $"Unknown device tier: {tier}")
            };
        }

        /// <summary>Detect the device tier automatically based on platform and GPU capabilities.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static DeviceTier Detect()
        {
#if UNITY_EDITOR
            return DeviceTier.PC; // Editor always runs on PC hardware.
#elif UNITY_IOS || UNITY_TVOS
            // iOS devices are all high-end (Apple silicon) — classify as MobileHE.
            return DeviceTier.MobileHE;
#elif UNITY_ANDROID
            // Android: check GPU feature level to distinguish Mobile-HE from out-of-scope devices.
            if (SystemInfo.graphicsMemorySize >= 4096) // 4+ GB dedicated GPU memory is a high-end indicator.
                return DeviceTier.MobileHE;

            // Fallback: check max texture size as a proxy for GPU tier.
            if (SystemInfo.maxTextureSize >= 8192)
                return DeviceTier.MobileHE;

            // Mid-tier or low-tier Android — out of scope per Constitution Principle IV, but we
            // still classify it as Mobile-HE to avoid crashes (just with lower budgets).
            return DeviceTier.MobileHE;
#elif UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX
            return SystemInfo.graphicsMemorySize >= 8192 ? DeviceTier.PC : DeviceTier.Console;
#elif UNITY_SWITCH
            return DeviceTier.Console;
#else
            return DeviceTier.Console; // Conservative default for unknown platforms.
#endif
        }

        /// <summary>True when this tier uses sub-native resolution rendering (Mobile-HE only).</summary>
        public bool HasSubNativeResolution => RenderScale < 1.0f;

        /// <summary>String representation for debugging and telemetry.</summary>
        public override string ToString() =>
            $"Budget(cap={BrickPoolCapacity}, detail={DetailRadius}m, scale={RenderScale}, " +
            $"probes={ProbeSpacing}m, debris={MaxDebris}, view={MaxViewDistance}m, " +
            $"surface={SurfaceGeometryBudget})";
    }
}
