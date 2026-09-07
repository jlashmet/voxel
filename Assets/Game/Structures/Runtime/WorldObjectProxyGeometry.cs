using System;
using System.Collections.Generic;
using Game.Structures.Api;
using UnityEngine;

namespace Game.Structures.Runtime
{
    public readonly struct WorldObjectProxyGeometryData
    {
        public readonly Vector3[] Positions;
        public readonly int[] Indices;

        public WorldObjectProxyGeometryData(Vector3[] positions, int[] indices)
        {
            Positions = positions ?? Array.Empty<Vector3>();
            Indices = indices ?? Array.Empty<int>();
        }

        public bool IsWellFormed => Positions.Length >= 8 && Indices.Length >= 12 && Indices.Length % 3 == 0;
    }

    /// <summary>
    /// Reusable production geometry for mechanism proxies whose construction must be readable even
    /// before bespoke authored assets exist. Geometry is normalized to the descriptor bounds; runtime
    /// pose, interaction and collision remain owned by <see cref="WorldObjectPresentationPlanner"/>.
    /// </summary>
    public static class WorldObjectProxyGeometry
    {
        public static bool TryBuild(WorldObjectKind kind, out WorldObjectProxyGeometryData geometry)
        {
            var builder = new Builder();
            switch (kind)
            {
                case WorldObjectKind.Door:
                case WorldObjectKind.SecretDoor:
                    AddDoor(builder);
                    break;
                case WorldObjectKind.Trapdoor:
                    AddTrapdoor(builder);
                    break;
                default:
                    geometry = default;
                    return false;
            }

            geometry = builder.Build();
            return geometry.IsWellFormed;
        }

        public static bool TryCreateMesh(WorldObjectKind kind, out Mesh mesh)
        {
            mesh = null;
            if (!TryBuild(kind, out WorldObjectProxyGeometryData geometry))
                return false;

            mesh = new Mesh
            {
                name = $"WorldObjectProxy_{kind}",
                hideFlags = HideFlags.DontSave,
                vertices = geometry.Positions,
                triangles = geometry.Indices,
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return true;
        }

        private static void AddDoor(Builder b)
        {
            const float rail = 0.115f;
            const float frameDepth = 0.34f;
            const float panelDepth = 0.15f;
            b.AddBox(new Vector3(-0.5f, -0.5f, -frameDepth * 0.5f), new Vector3(rail, 1f, frameDepth));
            b.AddBox(new Vector3(0.5f - rail, -0.5f, -frameDepth * 0.5f), new Vector3(rail, 1f, frameDepth));
            b.AddBox(new Vector3(-0.5f + rail, -0.5f, -frameDepth * 0.5f), new Vector3(1f - rail * 2f, rail, frameDepth));
            b.AddBox(new Vector3(-0.5f + rail, 0.5f - rail, -frameDepth * 0.5f), new Vector3(1f - rail * 2f, rail, frameDepth));
            b.AddBox(new Vector3(-0.5f + rail * 1.35f, -0.5f + rail * 1.35f, -panelDepth * 0.5f),
                new Vector3(1f - rail * 2.7f, 1f - rail * 2.7f, panelDepth));
            b.AddBox(new Vector3(-0.5f + rail, -0.055f, -frameDepth * 0.55f),
                new Vector3(1f - rail * 2f, 0.11f, frameDepth * 1.10f));
            b.AddBox(new Vector3(0.27f, -0.02f, -0.27f), new Vector3(0.09f, 0.12f, 0.18f));
        }

        private static void AddTrapdoor(Builder b)
        {
            const float rail = 0.12f;
            const float frameDepth = 0.34f;
            const float panelDepth = 0.15f;
            b.AddBox(new Vector3(-0.5f, -frameDepth * 0.5f, -0.5f), new Vector3(rail, frameDepth, 1f));
            b.AddBox(new Vector3(0.5f - rail, -frameDepth * 0.5f, -0.5f), new Vector3(rail, frameDepth, 1f));
            b.AddBox(new Vector3(-0.5f + rail, -frameDepth * 0.5f, -0.5f), new Vector3(1f - rail * 2f, frameDepth, rail));
            b.AddBox(new Vector3(-0.5f + rail, -frameDepth * 0.5f, 0.5f - rail), new Vector3(1f - rail * 2f, frameDepth, rail));
            b.AddBox(new Vector3(-0.5f + rail * 1.35f, -panelDepth * 0.5f, -0.5f + rail * 1.35f),
                new Vector3(1f - rail * 2.7f, panelDepth, 1f - rail * 2.7f));
            b.AddBox(new Vector3(-0.055f, -frameDepth * 0.55f, -0.5f + rail),
                new Vector3(0.11f, frameDepth * 1.10f, 1f - rail * 2f));
            b.AddBox(new Vector3(0.23f, -0.27f, 0.27f), new Vector3(0.12f, 0.18f, 0.09f));
        }

        private sealed class Builder
        {
            private readonly List<Vector3> _positions = new List<Vector3>(64);
            private readonly List<int> _indices = new List<int>(96);

            public WorldObjectProxyGeometryData Build() =>
                new WorldObjectProxyGeometryData(_positions.ToArray(), _indices.ToArray());

            public void AddBox(Vector3 min, Vector3 size)
            {
                Vector3 max = min + new Vector3(
                    Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), Mathf.Max(0.01f, size.z));
                int v = _positions.Count;
                _positions.Add(new Vector3(min.x, min.y, min.z));
                _positions.Add(new Vector3(max.x, min.y, min.z));
                _positions.Add(new Vector3(max.x, max.y, min.z));
                _positions.Add(new Vector3(min.x, max.y, min.z));
                _positions.Add(new Vector3(min.x, min.y, max.z));
                _positions.Add(new Vector3(max.x, min.y, max.z));
                _positions.Add(new Vector3(max.x, max.y, max.z));
                _positions.Add(new Vector3(min.x, max.y, max.z));
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
