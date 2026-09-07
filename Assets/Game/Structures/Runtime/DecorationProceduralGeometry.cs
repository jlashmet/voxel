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
    /// Canonical production consumer for procedural decoration requests. Registered decoration
    /// requests retain their canonical stable-id encoding in Variant, so the consumer resolves the
    /// owning catalogue recipe and renders its shared semantic shape grammar. Unsupported or
    /// malformed requests fail explicitly; there is deliberately no generic substitute cube.
    /// </summary>
    public static class DecorationProceduralGeometryBuilder
    {
        public static bool TryBuild(
            in DecorationProceduralMeshRequest request,
            out DecorationProceduralGeometry geometry)
        {
            geometry = default;
            if (!request.Id.IsWellFormed || !request.Bounds.IsWellFormed ||
                !TryResolveRegisteredShape(request.Variant, out DecorationContentShape shape))
                return false;

            var mesh = new Builder(MaterialForFamily(request.Family));
            if (!TryAddShape(mesh, shape, in request.Bounds, request.Variant))
                return false;

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
                    mesh.AddRope(
                        new float3(centre.x, max.y, centre.z),
                        new float3(centre.x, min.y, centre.z),
                        math.max(0.4f, size.x * 0.32f), r, 8);
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

        private static bool TryResolveRegisteredShape(uint variant, out DecorationContentShape shape)
        {
            shape = default;
            if ((variant & 0xC0000000u) != 0xC0000000u)
                return false;

            ushort stableId = (ushort)((variant & 0x3FF00000u) >> 20);
            if (stableId == 0 || stableId > DecorationShowcaseCatalog.RegisteredDecorationCount)
                return false;

            if (stableId <= 114)
            {
                DecorationContentRecipe recipe = DecorationContentCatalog.Recipe((DecorationContentKind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 200)
            {
                DecorationExpandedContentRecipe recipe = DecorationExpansion200Catalog.Recipe((DecorationExpandedContentKind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 260)
            {
                DecorationExpansion260Recipe recipe = DecorationExpansion260Catalog.Recipe((DecorationExpansion260Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 300)
            {
                DecorationExpansion300Recipe recipe = DecorationExpansion300Catalog.Recipe((DecorationExpansion300Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 320)
            {
                DecorationExpansion320Recipe recipe = DecorationExpansion320Catalog.Recipe((DecorationExpansion320Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 340)
            {
                DecorationExpansion340Recipe recipe = DecorationExpansion340Catalog.Recipe((DecorationExpansion340Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 360)
            {
                DecorationExpansion360Recipe recipe = DecorationExpansion360Catalog.Recipe((DecorationExpansion360Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 380)
            {
                DecorationExpansion380Recipe recipe = DecorationExpansion380Catalog.Recipe((DecorationExpansion380Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }
            if (stableId <= 400)
            {
                DecorationExpansion400Recipe recipe = DecorationExpansion400Catalog.Recipe((DecorationExpansion400Kind)stableId);
                if (!recipe.IsWellFormed) return false;
                shape = recipe.Shape;
                return true;
            }

            GuildSignatureRecipe guildRecipe = GuildSignatureDecorationCatalog.Recipe((GuildSignatureKind)stableId);
            if (!guildRecipe.IsWellFormed) return false;
            shape = guildRecipe.Shape;
            return true;
        }

        private static bool TryAddShape(
            Builder mesh,
            DecorationContentShape shape,
            in DecorationBounds bounds,
            uint variant)
        {
            float3 min = bounds.Min;
            float3 max = bounds.MaxExclusive;
            float3 size = max - min;
            float3 centre = (min + max) * 0.5f;
            float thin = math.max(0.2f, math.min(size.x, size.z) * 0.10f);
            float post = math.max(0.2f, math.min(size.x, size.z) * 0.12f);

            switch (shape)
            {
                case DecorationContentShape.WorkSurface:
                case DecorationContentShape.Counter:
                {
                    float top = math.max(0.4f, size.y * 0.20f);
                    mesh.AddBox(new float3(min.x, max.y - top, min.z), new float3(size.x, top, size.z));
                    float legHeight = math.max(0.2f, size.y - top);
                    float leg = math.max(0.2f, math.min(size.x, size.z) * 0.12f);
                    mesh.AddBox(min, new float3(leg, legHeight, leg));
                    mesh.AddBox(new float3(max.x - leg, min.y, min.z), new float3(leg, legHeight, leg));
                    mesh.AddBox(new float3(min.x, min.y, max.z - leg), new float3(leg, legHeight, leg));
                    mesh.AddBox(new float3(max.x - leg, min.y, max.z - leg), new float3(leg, legHeight, leg));
                    return true;
                }
                case DecorationContentShape.Machine:
                {
                    float baseHeight = math.max(0.4f, size.y * 0.35f);
                    mesh.AddBox(min, new float3(size.x, baseHeight, size.z));
                    float radius = math.max(0.3f, math.min(size.x, size.z) * 0.24f);
                    mesh.AddCylinder(new float3(centre.x, min.y + baseHeight, centre.z), radius,
                        math.max(0.4f, size.y - baseHeight), 10);
                    return true;
                }
                case DecorationContentShape.Hearth:
                {
                    float baseHeight = math.max(0.4f, size.y * 0.34f);
                    mesh.AddBox(min, new float3(size.x, baseHeight, size.z));
                    float radius = math.max(0.25f, math.min(size.x, size.z) * 0.18f);
                    mesh.AddFrustum(new float3(centre.x, min.y + baseHeight, centre.z),
                        radius * 0.55f, radius, math.max(0.4f, size.y - baseHeight), 10);
                    return true;
                }
                case DecorationContentShape.WheelMachine:
                {
                    float standHeight = math.max(0.4f, size.y * 0.38f);
                    mesh.AddBox(min, new float3(size.x, standHeight, size.z));
                    float radius = math.max(0.3f, math.min(size.x, size.z) * 0.36f);
                    mesh.AddCylinder(new float3(centre.x, min.y + standHeight, centre.z), radius,
                        math.max(0.2f, size.y - standHeight), 12);
                    return true;
                }
                case DecorationContentShape.Tub:
                case DecorationContentShape.Trough:
                case DecorationContentShape.Well:
                case DecorationContentShape.Fountain:
                {
                    float radius = math.max(0.35f, math.min(size.x, size.z) * 0.46f);
                    float wallHeight = math.max(0.4f, size.y * 0.55f);
                    mesh.AddFrustum(new float3(centre.x, min.y, centre.z), radius * 0.88f, radius, wallHeight, 12);
                    if (shape == DecorationContentShape.Fountain)
                        mesh.AddCylinder(new float3(centre.x, min.y + wallHeight, centre.z),
                            math.max(0.18f, radius * 0.18f), math.max(0.3f, size.y - wallHeight), 8);
                    return true;
                }
                case DecorationContentShape.WallRack:
                case DecorationContentShape.Rack:
                {
                    float rail = math.max(0.18f, thin);
                    mesh.AddBox(min, new float3(rail, size.y, rail));
                    mesh.AddBox(new float3(max.x - rail, min.y, min.z), new float3(rail, size.y, rail));
                    int shelves = math.clamp((int)math.round(size.y / math.max(2f, size.y / 3f)), 2, 4);
                    for (int i = 1; i <= shelves; i++)
                    {
                        float y = min.y + size.y * i / (shelves + 1f);
                        mesh.AddBox(new float3(min.x, y, min.z), new float3(size.x, rail, size.z));
                    }
                    return true;
                }
                case DecorationContentShape.Stack:
                {
                    int layers = math.clamp((int)math.round(size.y / math.max(1f, math.min(size.x, size.z) * 0.35f)), 2, 5);
                    float layerHeight = size.y / layers;
                    for (int i = 0; i < layers; i++)
                    {
                        float inset = (((variant >> (i * 2)) & 1u) == 0u ? 0f : math.min(size.x, size.z) * 0.08f);
                        mesh.AddBox(new float3(min.x + inset, min.y + layerHeight * i, min.z + inset),
                            new float3(math.max(0.2f, size.x - inset * 2f), layerHeight,
                                math.max(0.2f, size.z - inset * 2f)));
                    }
                    return true;
                }
                case DecorationContentShape.Coffin:
                {
                    float lower = math.max(0.3f, size.y * 0.60f);
                    mesh.AddBox(min, new float3(size.x, lower, size.z));
                    mesh.AddFrustum(new float3(centre.x, min.y + lower, centre.z),
                        math.min(size.x, size.z) * 0.30f, math.min(size.x, size.z) * 0.46f,
                        math.max(0.2f, size.y - lower), 8);
                    return true;
                }
                case DecorationContentShape.Pedestal:
                case DecorationContentShape.Monument:
                {
                    float baseHeight = math.max(0.3f, size.y * 0.20f);
                    mesh.AddBox(min, new float3(size.x, baseHeight, size.z));
                    float radius = math.max(0.25f, math.min(size.x, size.z) * 0.28f);
                    mesh.AddCylinder(new float3(centre.x, min.y + baseHeight, centre.z), radius,
                        math.max(0.3f, size.y - baseHeight), shape == DecorationContentShape.Monument ? 10 : 8);
                    return true;
                }
                case DecorationContentShape.Stall:
                {
                    float postSize = math.max(0.2f, math.min(size.x, size.z) * 0.08f);
                    mesh.AddBox(min, new float3(postSize, size.y, postSize));
                    mesh.AddBox(new float3(max.x - postSize, min.y, min.z), new float3(postSize, size.y, postSize));
                    mesh.AddBox(new float3(min.x, min.y, max.z - postSize), new float3(postSize, size.y, postSize));
                    mesh.AddBox(new float3(max.x - postSize, min.y, max.z - postSize), new float3(postSize, size.y, postSize));
                    mesh.AddBox(new float3(min.x, max.y - postSize, min.z), new float3(size.x, postSize, size.z));
                    return true;
                }
                case DecorationContentShape.Hanging:
                {
                    float ropeLength = math.max(0.3f, size.y * 0.58f);
                    mesh.AddRope(new float3(centre.x, max.y, centre.z),
                        new float3(centre.x, max.y - ropeLength, centre.z),
                        math.max(0.15f, size.x * 0.10f), post, 7);
                    float bodyHeight = math.max(0.25f, size.y - ropeLength);
                    mesh.AddFrustum(new float3(centre.x, min.y, centre.z),
                        math.max(0.2f, math.min(size.x, size.z) * 0.22f),
                        math.max(0.3f, math.min(size.x, size.z) * 0.42f), bodyHeight, 10);
                    return true;
                }
                case DecorationContentShape.Sign:
                case DecorationContentShape.Canopy:
                {
                    float thickness = math.max(0.16f, math.min(size.x, size.z) * 0.12f);
                    mesh.AddBox(new float3(min.x, min.y, centre.z - thickness * 0.5f),
                        new float3(size.x, size.y, thickness));
                    return true;
                }
                case DecorationContentShape.Post:
                case DecorationContentShape.LampPost:
                {
                    float radius = math.max(0.22f, math.min(size.x, size.z) * 0.18f);
                    float stemHeight = shape == DecorationContentShape.LampPost ? math.max(0.4f, size.y * 0.76f) : size.y;
                    mesh.AddCylinder(new float3(centre.x, min.y, centre.z), radius, stemHeight, 10);
                    if (shape == DecorationContentShape.LampPost)
                        mesh.AddFrustum(new float3(centre.x, min.y + stemHeight, centre.z), radius * 0.8f,
                            math.max(radius * 1.6f, math.min(size.x, size.z) * 0.36f),
                            math.max(0.25f, size.y - stemHeight), 10);
                    return true;
                }
                case DecorationContentShape.Restraint:
                {
                    float beam = math.max(0.2f, math.min(size.y, math.min(size.x, size.z)) * 0.16f);
                    float midY = min.y + size.y * 0.58f;
                    mesh.AddBox(new float3(min.x, midY, min.z), new float3(size.x, beam, size.z));
                    mesh.AddBox(min, new float3(beam, size.y, beam));
                    mesh.AddBox(new float3(max.x - beam, min.y, max.z - beam), new float3(beam, size.y, beam));
                    return true;
                }
                case DecorationContentShape.Cage:
                {
                    float bar = math.max(0.16f, math.min(size.x, size.z) * 0.06f);
                    mesh.AddBox(min, new float3(size.x, bar, size.z));
                    mesh.AddBox(new float3(min.x, max.y - bar, min.z), new float3(size.x, bar, size.z));
                    int bars = 5;
                    for (int i = 0; i < bars; i++)
                    {
                        float t = i / (bars - 1f);
                        float x = math.lerp(min.x, max.x - bar, t);
                        mesh.AddBox(new float3(x, min.y, min.z), new float3(bar, size.y, bar));
                        mesh.AddBox(new float3(x, min.y, max.z - bar), new float3(bar, size.y, bar));
                    }
                    return true;
                }
                case DecorationContentShape.Cart:
                {
                    float bodyHeight = math.max(0.4f, size.y * 0.55f);
                    mesh.AddBox(new float3(min.x, min.y + size.y * 0.25f, min.z),
                        new float3(size.x, bodyHeight, size.z));
                    float wheelRadius = math.max(0.25f, math.min(size.y, size.z) * 0.18f);
                    mesh.AddCylinder(new float3(min.x + size.x * 0.22f, min.y, centre.z), wheelRadius,
                        math.max(0.18f, thin), 10);
                    mesh.AddCylinder(new float3(max.x - size.x * 0.22f, min.y, centre.z), wheelRadius,
                        math.max(0.18f, thin), 10);
                    return true;
                }
                default:
                    return false;
            }
        }

        private static byte MaterialForFamily(DecorationPropFamily family)
        {
            switch (family)
            {
                case DecorationPropFamily.Rug:
                case DecorationPropFamily.Banner:
                case DecorationPropFamily.Curtain:
                    return GameMaterialIds.Cloth;
                case DecorationPropFamily.WeaponRack:
                case DecorationPropFamily.Chandelier:
                case DecorationPropFamily.Lantern:
                case DecorationPropFamily.Candle:
                    return GameMaterialIds.Gold;
                case DecorationPropFamily.Fireplace:
                case DecorationPropFamily.Campfire:
                case DecorationPropFamily.Fountain:
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
