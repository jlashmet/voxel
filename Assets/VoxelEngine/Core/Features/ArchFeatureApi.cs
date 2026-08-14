using System;
using Unity.Mathematics;

namespace VoxelEngine.Core.Features
{
    /// <summary>
    /// Material/presentation inputs for the reusable arch feature. Geometry callers do not need to
    /// know the internal ArchFeatureDefinition layout and can supply any registered feature styles.
    /// </summary>
    public readonly struct ArchFeatureStyle
    {
        public readonly byte StoneMaterial;
        public readonly ushort PierStyle;
        public readonly ushort RingStyle;
        public readonly byte Coating;

        public ArchFeatureStyle(byte stoneMaterial, ushort pierStyle, ushort ringStyle, byte coating)
        {
            StoneMaterial = stoneMaterial;
            PierStyle = pierStyle;
            RingStyle = ringStyle;
            Coating = coating;
        }
    }

    /// <summary>
    /// High-level, voxel-unit request for an architectural arch bay. ClearSpan and ClearHeight
    /// describe the usable opening; the compiler derives the lower-level semicircle and pier data.
    /// Optional zero-valued detail fields select deterministic structural defaults.
    /// </summary>
    public readonly struct ArchBayRequest
    {
        public readonly int ClearSpan;
        public readonly int ClearHeight;
        public readonly int Depth;
        public readonly int RingThickness;
        public readonly int ShoulderWidth;
        public readonly int TopMargin;
        public readonly int VoussoirCount;
        public readonly int JointRecessDepth;
        public readonly uint Seed;
        public readonly ArchRuinDamage Damage;
        public readonly byte DamageScale;

        public ArchBayRequest(
            int clearSpan,
            int clearHeight,
            int depth,
            int ringThickness = 0,
            int shoulderWidth = 0,
            int topMargin = 0,
            int voussoirCount = 0,
            int jointRecessDepth = 0,
            uint seed = 0,
            ArchRuinDamage damage = ArchRuinDamage.Intact,
            byte damageScale = 0)
        {
            ClearSpan = clearSpan;
            ClearHeight = clearHeight;
            Depth = depth;
            RingThickness = ringThickness;
            ShoulderWidth = shoulderWidth;
            TopMargin = topMargin;
            VoussoirCount = voussoirCount;
            JointRecessDepth = jointRecessDepth;
            Seed = seed;
            Damage = damage;
            DamageScale = damageScale;
        }
    }

    /// <summary>
    /// Stable construction API for architectural arches. This is intentionally the only place that
    /// translates a semantic clear opening into ArchFeatureDefinition/ArchBayFeatureDefinition.
    /// The existing feature emitters remain the single source of voxel geometry.
    /// </summary>
    public static class ArchFeatureApi
    {
        public static ArchBayFeatureDefinition CompileBay(
            in ArchBayRequest request,
            in ArchFeatureStyle style)
        {
            if (request.ClearSpan < 4)
                throw new ArgumentOutOfRangeException(nameof(request), "Arch clear span must be at least four voxels.");
            if (request.ClearHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(request), "Arch clear height must be positive.");
            if (request.Depth <= 0)
                throw new ArgumentOutOfRangeException(nameof(request), "Arch depth must be positive.");
            if (style.StoneMaterial == 0)
                throw new ArgumentException("Arch style requires a non-air stone material.", nameof(style));

            // The structural arch uses an integer semicircle and therefore requires an even span.
            // Round inward so a compiled feature never exceeds the opening requested by its caller.
            int clearSpan = request.ClearSpan & ~1;
            if (clearSpan < 4) clearSpan = 4;
            int clearRadius = clearSpan / 2;
            int pierHeight = request.ClearHeight - clearRadius;
            if (pierHeight <= 0)
                throw new ArgumentOutOfRangeException(nameof(request),
                    "Arch clear height must exceed half of the normalized clear span.");

            int ringThickness = request.RingThickness > 0
                ? request.RingThickness
                : math.max(2, clearSpan / 6);
            int voussoirCount = request.VoussoirCount > 0
                ? request.VoussoirCount
                : math.clamp(clearSpan + 1, 7, 17);
            int jointRecessDepth = math.clamp(
                request.JointRecessDepth, 0, math.max(0, request.Depth - 1));

            var arch = new ArchFeatureDefinition
            {
                ClearSpan = clearSpan,
                PierHeight = pierHeight,
                RingThickness = ringThickness,
                Depth = request.Depth,
                VoussoirCount = voussoirCount,
                JointRecessDepth = jointRecessDepth,
                StoneMaterial = style.StoneMaterial,
                PierStyle = style.PierStyle,
                RingStyle = style.RingStyle,
                Coating = style.Coating,
            };

            return new ArchBayFeatureDefinition
            {
                Arch = arch,
                ShoulderWidth = math.max(0, request.ShoulderWidth),
                TopMargin = request.TopMargin > 0 ? request.TopMargin : math.max(1, ringThickness),
                FaceRecess = math.clamp(math.max(1, request.Depth / 4), 1, math.max(1, request.Depth - 1)),
                PlinthHeight = math.max(2, pierHeight / 5),
                ImpostHeight = math.max(2, pierHeight / 6),
                Damage = request.Damage,
                DamageSeed = request.Seed,
                DamageScale = request.DamageScale,
            };
        }
    }
}
