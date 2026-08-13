using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Features;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    public sealed partial class TerrainLookdev
    {
        private const byte TerrainTurfNear = 22;
        private const byte TerrainTurfMid = 23;
        private const byte TerrainTurfFar = 24;
        private bool _tonalOverlayApplied;

        private void Start()
        {
            ConfigureTurfPresentation();
            VoxelRenderBridge.SunDirection = new Vector3(-0.18f, 0.95f, -0.25f).normalized;
            ApplyTonalOverlay();
        }

        private void ConfigureTurfPresentation()
        {
            const uint weather = (1u << Coatings.Moss) | (1u << Coatings.Wet);
            _palette.Register(TerrainTurfNear, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfMid, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);
            _palette.Register(TerrainTurfFar, 24, DestructionClass.Powder, SurfaceStyles.Smooth, weather);

            Vector4 sourceSampling = VoxelPresentationCatalogue.MaterialSampling[Mat.Grass];
            Vector4 sourceSurface = VoxelPresentationCatalogue.MaterialSurface[Mat.Grass];
            SetTurfPresentation(TerrainTurfNear, new Color(0.27f, 0.34f, 0.15f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfMid, new Color(0.40f, 0.43f, 0.20f), sourceSampling, sourceSurface);
            SetTurfPresentation(TerrainTurfFar, new Color(0.53f, 0.51f, 0.25f), sourceSampling, sourceSurface);
        }

        private static void SetTurfPresentation(byte material, Color colour,
            Vector4 sourceSampling, Vector4 sourceSurface)
        {
            VoxelPresentationCatalogue.MaterialAlbedo[material] =
                new Vector4(colour.r, colour.g, colour.b, 1f);
            VoxelPresentationCatalogue.MaterialSampling[material] =
                new Vector4(sourceSampling.x, sourceSampling.y, sourceSampling.z, 0.05f);
            VoxelPresentationCatalogue.MaterialSurface[material] =
                new Vector4(sourceSurface.x, 0.025f, 0.82f, 0f);
            VoxelPresentationCatalogue.MaterialVariation[material] =
                new Vector4(0.68f, 0.006f, 0f, 0.004f);
        }

        private void ApplyTonalOverlay()
        {
            if (!_built || _tonalOverlayApplied) return;
            var writer = new VoxelBrush(_table, _pool, in _palette, 2_600_000);
            for (int z = TerrainZMin; z <= TerrainZMax; z++)
            for (int x = TerrainXMin; x <= TerrainXMax; x++)
            {
                int baseTop = HeightVoxel(x, z);
                int relief = ReferenceReliefVoxels(x, z);
                int top = baseTop + relief;
                if (relief > 0)
                    writer.FillColumnBulk(x, baseTop + 1, top, z, Mat.TerrainEarth);
                writer.SetStyled(x, top, z, GroundToneMaterial(x, z), SurfaceStyles.Smooth,
                    GroundToneCoating(x, z));
            }

            AddRaisedRockFields(ref writer);

            if (writer.BudgetExceeded)
                throw new System.InvalidOperationException("Terrain tonal overlay exceeded voxel authoring budget.");
            _table = writer.Table;
            _pool = writer.Pool;
            using (NativeArray<int3> regions = _table.GetResidentCoords(Allocator.Temp))
                for (int i = 0; i < regions.Length; i++) _changes.PublishRegion(regions[i]);
            _tonalOverlayApplied = true;
        }

        private static void AddRaisedRockFields(ref VoxelBrush writer)
        {
            var rng = new Unity.Mathematics.Random(Seed ^ 0x39C17u);
            for (int cluster = 0; cluster < 52; cluster++)
            {
                int z = rng.NextInt(-28, 345);
                int path = PathCenterVoxel(z);
                int side = rng.NextBool() ? -1 : 1;
                int centreX = path + side * rng.NextInt(36, 145);
                if (centreX < TerrainXMin + 10 || centreX > TerrainXMax - 10) continue;

                int count = rng.NextInt(3, 8);
                int stride = rng.NextInt(5, 10);
                for (int i = 0; i < count; i++)
                {
                    int x = centreX + (i - count / 2) * stride + rng.NextInt(-3, 4);
                    int zz = z + rng.NextInt(-6, 7) + (i - count / 2) / 2;
                    if (x < TerrainXMin + 5 || x > TerrainXMax - 5) continue;
                    if (math.abs(x - PathCenterVoxel(zz)) < 24) continue;

                    int hx = rng.NextInt(2, 6);
                    int hy = rng.NextInt(1, 4);
                    int hz = rng.NextInt(2, 6);
                    int top = HeightVoxel(x, zz) + ReferenceReliefVoxels(x, zz);
                    StampRoundedBox(ref writer, new int3(x, top + hy - 1, zz),
                        new int3(hx, hy, hz), math.min(2, math.min(hx, hz)),
                        Mat.TerrainLimestone, SurfaceStyles.Rounded, rng.NextFloat() < 0.36f);
                }
            }

            for (int i = 0; i < 120; i++)
            {
                int z = rng.NextInt(-35, 330);
                int x = rng.NextInt(TerrainXMin + 8, TerrainXMax - 8);
                if (math.abs(x - PathCenterVoxel(z)) < 26) continue;
                int hx = rng.NextInt(2, 4);
                int hy = rng.NextInt(1, 3);
                int hz = rng.NextInt(2, 4);
                int top = HeightVoxel(x, z) + ReferenceReliefVoxels(x, z);
                StampRoundedBox(ref writer, new int3(x, top + hy - 1, z),
                    new int3(hx, hy, hz), 1, Mat.TerrainLimestone,
                    SurfaceStyles.Rounded, rng.NextFloat() < 0.24f);
            }
        }

        private static int ReferenceReliefVoxels(int x, int z)
        {
            float relief = 0f;
            relief += 7f * SoftHill(x, z, -112, 24, 82, 76);
            relief += 7f * SoftHill(x, z, 108, 30, 84, 78);
            relief += 7f * SoftHill(x, z, -108, 105, 108, 94);
            relief += 8f * SoftHill(x, z, 104, 118, 110, 96);
            relief += 6f * SoftHill(x, z, -98, 205, 124, 110);
            relief += 7f * SoftHill(x, z, 96, 218, 124, 112);
            relief += 5f * SoftHill(x, z, -86, 315, 134, 122);
            relief += 5f * SoftHill(x, z, 82, 332, 136, 126);

            float fromValley = math.abs(x - PathCenterVoxel(z));
            float valleyMask = math.smoothstep(0f, 1f, math.saturate((fromValley - 10f) / 78f));
            float farRelease = math.saturate((z - 250f) / 140f);
            valleyMask = math.lerp(valleyMask, 0.70f + 0.30f * valleyMask, farRelease);
            return Mathf.RoundToInt(math.min(relief * valleyMask, 12f));
        }

        private static float SoftHill(int x, int z, int cx, int cz, int rx, int rz)
        {
            float dx = (x - cx) / (float)rx;
            float dz = (z - cz) / (float)rz;
            float q = dx * dx + dz * dz;
            if (q >= 1f) return 0f;
            float t = 1f - q;
            return t * t * (3f - 2f * t);
        }

        private static byte GroundToneMaterial(int x, int z)
        {
            if (z < 20) return TerrainTurfNear;
            if (z < 165) return TerrainTurfMid;
            if (z < 225)
            {
                float farBlend = math.saturate((z - 165f) / 60f);
                return Hash01(x, z) < farBlend ? TerrainTurfFar : TerrainTurfMid;
            }
            return TerrainTurfFar;
        }

        private static byte GroundToneCoating(int x, int z)
        {
            int fromPath = math.abs(x - PathCenterVoxel(z));
            if (z < 0 && fromPath > 14) return Coatings.Moss;
            return Coatings.None;
        }
    }
}
