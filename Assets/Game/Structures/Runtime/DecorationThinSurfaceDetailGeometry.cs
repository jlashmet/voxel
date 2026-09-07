using System;
using System.Collections.Generic;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Raised construction detail for thin wall-mounted artwork/sign surfaces. The base thin-surface
    /// batch remains the authoritative presentation plane; this adds reusable frame/face construction
    /// for painting-family surfaces instead of asking individual scenes to decorate flat quads.
    /// Positions remain in voxel space so every presentation consumer uses the same authored bounds.
    /// </summary>
    public static class DecorationThinSurfaceDetailGeometry
    {
        public static bool TryBuild(
            DecorationPlacement[] placements,
            in DecorationContext context,
            out DecorationProceduralGeometry geometry)
        {
            geometry = default;
            if (placements == null || !context.IsWellFormed)
                return false;

            DecorationPresentationProfile profile = DecorationContextProfiles.ResolvePresentation(in context);
            var builder = new Builder(profile.PrimaryMaterial);
            int detailed = 0;
            for (int i = 0; i < placements.Length; i++)
            {
                DecorationPlacement placement = placements[i];
                if (!placement.IsWellFormed ||
                    placement.Backend != DecorationRenderBackend.ThinSurface ||
                    placement.Family != DecorationPropFamily.Painting ||
                    math.abs(placement.Facing.y) != 0)
                    continue;

                AddFramedWallSurface(builder, in placement);
                detailed++;
            }

            if (detailed == 0)
                return false;
            geometry = builder.Build();
            return geometry.IsWellFormed;
        }

        private static void AddFramedWallSurface(Builder b, in DecorationPlacement placement)
        {
            DecorationBounds bounds = placement.Bounds;
            float3 min = bounds.Min;
            float3 max = bounds.MaxExclusive;
            float3 size = max - min;
            float rail = math.max(0.55f, math.min(size.x, size.y) * 0.085f);
            float relief = math.max(0.18f, math.min(0.42f, math.max(1f, math.min(size.x, size.z)) * 0.18f));

            if (math.abs(placement.Facing.z) == 1)
            {
                float z = placement.Facing.z > 0 ? min.z + 0.08f : max.z - 0.08f;
                z += placement.Facing.z * relief * 0.5f;
                float depthMin = z - relief * 0.5f;
                b.AddBox(new float3(min.x, min.y, depthMin), new float3(size.x, rail, relief));
                b.AddBox(new float3(min.x, max.y - rail, depthMin), new float3(size.x, rail, relief));
                b.AddBox(new float3(min.x, min.y + rail, depthMin), new float3(rail, math.max(0.2f, size.y - rail * 2f), relief));
                b.AddBox(new float3(max.x - rail, min.y + rail, depthMin), new float3(rail, math.max(0.2f, size.y - rail * 2f), relief));

                float emblemW = math.max(rail, size.x * 0.18f);
                float emblemH = math.max(rail, size.y * 0.42f);
                float cx = (min.x + max.x) * 0.5f;
                float cy = (min.y + max.y) * 0.5f;
                b.AddBox(new float3(cx - emblemW * 0.5f, cy - emblemH * 0.5f, depthMin - placement.Facing.z * 0.03f),
                    new float3(emblemW, emblemH, relief + 0.06f));
                b.AddBox(new float3(min.x + rail * 1.7f, cy - rail * 0.35f, depthMin - placement.Facing.z * 0.02f),
                    new float3(math.max(0.2f, size.x - rail * 3.4f), rail * 0.7f, relief + 0.04f));
            }
            else
            {
                float x = placement.Facing.x > 0 ? min.x + 0.08f : max.x - 0.08f;
                x += placement.Facing.x * relief * 0.5f;
                float depthMin = x - relief * 0.5f;
                b.AddBox(new float3(depthMin, min.y, min.z), new float3(relief, rail, size.z));
                b.AddBox(new float3(depthMin, max.y - rail, min.z), new float3(relief, rail, size.z));
                b.AddBox(new float3(depthMin, min.y + rail, min.z), new float3(relief, math.max(0.2f, size.y - rail * 2f), rail));
                b.AddBox(new float3(depthMin, min.y + rail, max.z - rail), new float3(relief, math.max(0.2f, size.y - rail * 2f), rail));

                float emblemW = math.max(rail, size.z * 0.18f);
                float emblemH = math.max(rail, size.y * 0.42f);
                float cz = (min.z + max.z) * 0.5f;
                float cy = (min.y + max.y) * 0.5f;
                b.AddBox(new float3(depthMin - placement.Facing.x * 0.03f, cy - emblemH * 0.5f, cz - emblemW * 0.5f),
                    new float3(relief + 0.06f, emblemH, emblemW));
                b.AddBox(new float3(depthMin - placement.Facing.x * 0.02f, cy - rail * 0.35f, min.z + rail * 1.7f),
                    new float3(relief + 0.04f, rail * 0.7f, math.max(0.2f, size.z - rail * 3.4f)));
            }
        }

        private sealed class Builder
        {
            private readonly List<float3> _positions = new List<float3>(64);
            private readonly List<int> _indices = new List<int>(96);
            private readonly byte _material;

            public Builder(byte material) => _material = material;

            public DecorationProceduralGeometry Build() =>
                new DecorationProceduralGeometry(_positions.ToArray(), _indices.ToArray(), _material);

            public void AddBox(float3 min, float3 size)
            {
                float3 max = min + math.max(size, new float3(0.05f));
                int v = _positions.Count;
                _positions.Add(new float3(min.x, min.y, min.z));
                _positions.Add(new float3(max.x, min.y, min.z));
                _positions.Add(new float3(max.x, max.y, min.z));
                _positions.Add(new float3(min.x, max.y, min.z));
                _positions.Add(new float3(min.x, min.y, max.z));
                _positions.Add(new float3(max.x, min.y, max.z));
                _positions.Add(new float3(max.x, max.y, max.z));
                _positions.Add(new float3(min.x, max.y, max.z));
                AddQuad(v + 0, v + 3, v + 2, v + 1);
                AddQuad(v + 4, v + 5, v + 6, v + 7);
                AddQuad(v + 0, v + 4, v + 7, v + 3);
                AddQuad(v + 1, v + 2, v + 6, v + 5);
                AddQuad(v + 0, v + 1, v + 5, v + 4);
                AddQuad(v + 3, v + 7, v + 6, v + 2);
            }

            private void AddQuad(int a, int b, int c, int d)
            {
                _indices.Add(a); _indices.Add(b); _indices.Add(c);
                _indices.Add(a); _indices.Add(c); _indices.Add(d);
            }
        }
    }
}
