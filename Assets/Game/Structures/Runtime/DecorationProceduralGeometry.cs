using System;
using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Renderer-neutral triangle payload for decoration presentation geometry. Positions are in
    /// voxel-space world coordinates and material identity remains the stable game material id.
    /// Presentation modules may upload this payload to their normal mesh renderer without owning
    /// decoration-shape policy.
    /// </summary>
    public readonly struct DecorationProceduralGeometry
    {
        public readonly float3[] Positions;
        public readonly int[] Indices;
        public readonly byte MaterialId;

        public DecorationProceduralGeometry(float3[] positions, int[] indices, byte materialId)
        {
            Positions = positions ?? Array.Empty<float3>();
            Indices = indices ?? Array.Empty<int>();
            MaterialId = materialId;
        }

        public bool IsWellFormed =>
            Positions != null && Positions.Length >= 3 &&
            Indices != null && Indices.Length >= 3 && (Indices.Length % 3) == 0 &&
            MaterialId != GameMaterialIds.Empty;
    }

    /// <summary>
    /// Canonical production consumer for procedural decoration requests. It deliberately consumes
    /// the semantic request types emitted by the owning catalogues instead of inferring shapes from
    /// showcase ids. The output is data-only so runtime presentation and validation share exactly
    /// the same geometry policy.
    /// </summary>
    public static class DecorationProceduralGeometryBuilder
    {
        public static bool TryBuild(
            in DecorationProceduralMeshRequest request,
            out DecorationProceduralGeometry geometry)
        {
            geometry = default;
            if (!request.Id.IsWellFormed || !request.Bounds.IsWellFormed)
                return false;

            var mesh = new Builder(MaterialForFamily(request.Family));
            DecorationBounds b = request.Bounds;
            float3 min = b.Min;
            float3 max = b.MaxExclusive;
            float3 size = max - min;
            float3 centre = (min + max) * 0.5f;
            float thin = math.max(0.25f, math.min(size.x, size.z) * 0.12f);

            switch (request.Family)
            {
                case DecorationPropFamily.Chandelier:
                {
                    float stemRadius = math.max(0.25f, math.min(size.x, size.z) * 0.06f);
                    mesh.AddCylinder(new float3(centre.x, min.y, centre.z), stemRadius, math.max(1f, size.y * 0.72f), 8);
                    float armY = min.y + size.y * 0.28f;
                    float armLength = math.max(0.5f, math.min(size.x, size.z) * 0.34f);
                    mesh.AddBox(new float3(centre.x - armLength, armY, centre.z - thin * 0.5f), new float3(armLength * 2f, thin, thin));
                    mesh.AddBox(new float3(centre.x - thin * 0.5f, armY, centre.z - armLength), new float3(thin, thin, armLength * 2f));
                    break;
                }
                case DecorationPropFamily.StandingLamp:
                {
                    float stemRadius = math.max(0.2f, math.min(size.x, size.z) * 0.08f);
                    mesh.AddCylinder(new float3(centre.x, min.y, centre.z), stemRadius, math.max(1f, size.y * 0.72f), 8);
                    float capY = min.y + size.y * 0.70f;
                    mesh.AddFrustum(new float3(centre.x, capY, centre.z),
                        math.max(stemRadius * 2f, math.min(size.x, size.z) * 0.18f),
                        math.max(stemRadius * 3f, math.min(size.x, size.z) * 0.46f),
                        math.max(0.5f, size.y * 0.26f), 10);
                    break;
                }
                case DecorationPropFamily.Curtain:
                case DecorationPropFamily.Tapestry:
                {
                    int folds = math.clamp((int)math.round(size.x / math.max(1f, size.z)), 3, 9);
                    float step = size.x / folds;
                    float depth = math.max(0.3f, size.z * 0.65f);
                    for (int i = 0; i < folds; i++)
                    {
                        float x = min.x + step * i;
                        float z = min.z + ((i & 1) == 0 ? 0f : math.max(0f, size.z - depth));
                        mesh.AddBox(new float3(x, min.y, z), new float3(step * 0.9f, size.y, depth));
                    }
                    break;
                }
                default:
                {
                    // Generic semantic fallback is still presentation mesh geometry, never voxel
                    // compatibility boxes. A framed body keeps unknown future procedural families
                    // visible while preserving their canonical bounds/material identity.
                    float insetX = math.min(size.x * 0.14f, math.max(0.25f, thin));
                    float insetZ = math.min(size.z * 0.14f, math.max(0.25f, thin));
                    mesh.AddBox(min + new float3(insetX, 0f, insetZ),
                        new float3(math.max(0.25f, size.x - insetX * 2f), size.y, math.max(0.25f, size.z - insetZ * 2f)));
                    break;
                }
            }

            geometry = mesh.ToGeometry();
            return geometry.IsWellFormed;
        }

        public static bool TryBuild(
            in MineCaveMeshRequest request,
            out DecorationProceduralGeometry geometry)
        {
            geometry = default;
            if (!request.Id.IsWellFormed || !request.Bounds.IsWellFormed ||
                request.Kind != MineCaveDecorationKind.Rope)
                return false;

            DecorationBounds b = request.Bounds;
            float3 start = new float3(
                (b.Min.x + b.MaxExclusive.x) * 0.5f,
                b.MaxExclusive.y,
                (b.Min.z + b.MaxExclusive.z) * 0.5f);
            float3 end = new float3(start.x, b.Min.y, start.z);
            float sag = math.max(0.35f, b.Size.y * (0.08f + (request.Variant & 3u) * 0.015f));
            var mesh = new Builder(GameMaterialIds.Wood);
            mesh.AddRope(start, end, sag, math.max(0.18f, math.min(b.Size.x, b.Size.z) * 0.16f), 8);
            geometry = mesh.ToGeometry();
            return geometry.IsWellFormed;
        }

        public static bool TryBuild(
            in NaturalCaveMeshRequest request,
            out DecorationProceduralGeometry geometry)
        {
            geometry = default;
            if (!request.Id.IsWellFormed || !request.Bounds.IsWellFormed)
                return false;

            DecorationBounds b = request.Bounds;
            float3 min = b.Min;
            float3 max = b.MaxExclusive;
            float3 size = max - min;
            float3 centre = (min + max) * 0.5f;
            var mesh = new Builder(request.Kind == NaturalCaveDecorationKind.Mushroom
                ? GameMaterialIds.Moss
                : GameMaterialIds.DarkStone);

            switch (request.Kind)
            {
                case NaturalCaveDecorationKind.Root:
                {
                    float r = math.max(0.2f, math.min(size.x, size.z) * 0.14f);
                    float3 top = new float3(centre.x, max.y, centre.z);
                    float3 bottom = new float3(centre.x, min.y, centre.z);
                    mesh.AddRope(top, bottom, math.max(0.4f, size.x * 0.32f), r, 8);
                    break;
                }
                case NaturalCaveDecorationKind.Mushroom:
                {
                    float stemHeight = math.max(0.5f, size.y * 0.58f);
                    float stemRadius = math.max(0.18f, math.min(size.x, size.z) * 0.14f);
                    mesh.AddCylinder(new float3(centre.x, min.y, centre.z), stemRadius, stemHeight, 8);
                    mesh.AddFrustum(new float3(centre.x, min.y + stemHeight, centre.z),
                        math.max(0.2f, math.min(size.x, size.z) * 0.08f),
                        math.max(0.4f, math.min(size.x, size.z) * 0.50f),
                        math.max(0.4f, size.y - stemHeight), 12);
                    break;
                }
                case NaturalCaveDecorationKind.Bones:
                {
                    float thickness = math.max(0.18f, math.min(size.x, size.z) * 0.10f);
                    float y = min.y + size.y * 0.5f;
                    mesh.AddBox(new float3(min.x, y - thickness * 0.5f, centre.z - thickness * 0.5f),
                        new float3(size.x, thickness, thickness));
                    mesh.AddBox(new float3(centre.x - thickness * 0.5f, y - thickness * 0.5f, min.z),
                        new float3(thickness, thickness, size.z));
                    break;
                }
                default:
                    return false;
            }

            geometry = mesh.ToGeometry();
            return geometry.IsWellFormed;
        }

        private static byte MaterialForFamily(DecorationPropFamily family)
        {
            switch (family)
            {
                case DecorationPropFamily.Rug:
                case DecorationPropFamily.Tapestry:
                case DecorationPropFamily.Banner:
                case DecorationPropFamily.Curtain:
                    return GameMaterialIds.Cloth;
                case DecorationPropFamily.Shield:
                case DecorationPropFamily.WeaponRack:
                case DecorationPropFamily.ArmorStand:
                    return GameMaterialIds.Gold;
                case DecorationPropFamily.Fireplace:
                case DecorationPropFamily.Campfire:
                    return GameMaterialIds.DarkStone;
                default:
                    return GameMaterialIds.Wood;
            }
        }

        private sealed class Builder
        {
            private readonly List<float3> _positions = new List<float3>(128);
            private readonly List<int> _indices = new List<int>(192);
            private readonly byte _material;

            public Builder(byte material) => _material = material;

            public DecorationProceduralGeometry ToGeometry() =>
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

            public void AddCylinder(float3 baseCentre, float radius, float height, int segments)
            {
                segments = math.clamp(segments, 6, 24);
                int bottom = _positions.Count;
                for (int i = 0; i < segments; i++)
                {
                    float angle = math.PI * 2f * i / segments;
                    _positions.Add(baseCentre + new float3(math.cos(angle) * radius, 0f, math.sin(angle) * radius));
                }
                int top = _positions.Count;
                for (int i = 0; i < segments; i++)
                    _positions.Add(_positions[bottom + i] + new float3(0f, height, 0f));
                int bottomCentre = _positions.Count;
                _positions.Add(baseCentre);
                int topCentre = _positions.Count;
                _positions.Add(baseCentre + new float3(0f, height, 0f));
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    AddQuad(bottom + i, bottom + next, top + next, top + i);
                    AddTriangle(bottomCentre, bottom + next, bottom + i);
                    AddTriangle(topCentre, top + i, top + next);
                }
            }

            public void AddFrustum(float3 baseCentre, float topRadius, float bottomRadius, float height, int segments)
            {
                segments = math.clamp(segments, 6, 24);
                int bottom = _positions.Count;
                for (int i = 0; i < segments; i++)
                {
                    float angle = math.PI * 2f * i / segments;
                    _positions.Add(baseCentre + new float3(math.cos(angle) * bottomRadius, 0f, math.sin(angle) * bottomRadius));
                }
                int top = _positions.Count;
                for (int i = 0; i < segments; i++)
                {
                    float angle = math.PI * 2f * i / segments;
                    _positions.Add(baseCentre + new float3(math.cos(angle) * topRadius, height, math.sin(angle) * topRadius));
                }
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    AddQuad(bottom + i, bottom + next, top + next, top + i);
                }
            }

            public void AddRope(float3 start, float3 end, float sag, float radius, int segments)
            {
                segments = math.clamp(segments, 4, 24);
                float3 previous = start;
                for (int i = 1; i <= segments; i++)
                {
                    float t = i / (float)segments;
                    float3 current = math.lerp(start, end, t);
                    current.x += math.sin(t * math.PI) * sag;
                    AddSegment(previous, current, radius);
                    previous = current;
                }
            }

            private void AddSegment(float3 a, float3 b, float radius)
            {
                float3 d = b - a;
                float len = math.length(d);
                if (len <= 0.0001f) return;
                float3 n = d / len;
                float3 axis = math.abs(n.y) < 0.9f ? new float3(0f, 1f, 0f) : new float3(1f, 0f, 0f);
                float3 right = math.normalize(math.cross(n, axis)) * radius;
                float3 up = math.normalize(math.cross(right, n)) * radius;
                int v = _positions.Count;
                _positions.Add(a - right - up);
                _positions.Add(a + right - up);
                _positions.Add(a + right + up);
                _positions.Add(a - right + up);
                _positions.Add(b - right - up);
                _positions.Add(b + right - up);
                _positions.Add(b + right + up);
                _positions.Add(b - right + up);
                AddQuad(v + 0, v + 1, v + 5, v + 4);
                AddQuad(v + 1, v + 2, v + 6, v + 5);
                AddQuad(v + 2, v + 3, v + 7, v + 6);
                AddQuad(v + 3, v + 0, v + 4, v + 7);
            }

            private void AddQuad(int a, int b, int c, int d)
            {
                AddTriangle(a, b, c);
                AddTriangle(a, c, d);
            }

            private void AddTriangle(int a, int b, int c)
            {
                _indices.Add(a);
                _indices.Add(b);
                _indices.Add(c);
            }
        }
    }
}
