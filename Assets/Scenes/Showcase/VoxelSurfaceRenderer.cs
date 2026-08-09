using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Occupancy;
using VoxelEngine.Core.Storage;
using VoxelEngine.Structures;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Builds a triangle mesh per resident region, one submesh object per material, and
    /// rebuilds only the regions the world marked dirty.
    ///
    /// This is deliberately *not* the engine's renderer. The shipping path is the compute
    /// raymarch in <c>VoxelEngine.Rendering</c>, which is still being brought up; the showcase
    /// needs the world on screen today, so it walks the same brick storage the raymarch will
    /// and builds geometry from it. Per-region meshes are what make that viable while
    /// streaming: loading a region rebuilds one mesh, not the world.
    ///
    /// Faces are greedy-meshed within each brick — coplanar faces of the same material merge
    /// into one quad — which is the difference between a few thousand triangles for a flat
    /// hillside and a quarter of a million.
    /// </summary>
    public sealed class VoxelSurfaceRenderer : IDisposable
    {
        /// <summary>Edge length of one voxel in metres (device-matrix.md: 10 cm).</summary>
        public const float VoxelSize = 0.1f;

        private const int MaterialCount = 15; // empty + engine palette + castle materials
        private const int E = VoxelDimensions.BrickEdge;

        private sealed class RegionSurface
        {
            public GameObject Root;
            public readonly Mesh[] Meshes = new Mesh[MaterialCount];
            public readonly MeshRenderer[] Renderers = new MeshRenderer[MaterialCount];
            public int Faces;
            public int Vertices;
        }

        private readonly GameObject _root;
        private readonly Material[] _materials;
        private readonly Dictionary<int3, RegionSurface> _surfaces = new();

        // Scratch, reused across every region rebuild so meshing does not churn the heap.
        private readonly List<Vector3>[] _verts = new List<Vector3>[MaterialCount];
        private readonly List<Vector3>[] _normals = new List<Vector3>[MaterialCount];
        private readonly List<int>[] _tris = new List<int>[MaterialCount];
        private readonly byte[] _brickMaterials = new byte[E * E * E];
        private readonly byte[] _mask = new byte[E * E];

        private bool _castShadows;

        public GameObject Root => _root;

        /// <summary>
        /// Hides the mesh path wholesale. Used when the GPU raymarch is driving, where leaving
        /// these renderers on would draw the world a second time.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
        }
        public int RegionMeshCount => _surfaces.Count;
        public int FaceCount { get; private set; }
        public int VertexCount { get; private set; }

        /// <summary>Milliseconds spent meshing in the most recent call.</summary>
        public double LastRebuildMs { get; private set; }

        /// <summary>Regions still waiting for a mesh rebuild.</summary>
        public int PendingRebuilds { get; private set; }

        public bool CastShadows
        {
            get => _castShadows;
            set
            {
                if (_castShadows == value) return;
                _castShadows = value;
                var mode = value ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (var s in _surfaces.Values)
                    foreach (var r in s.Renderers)
                        if (r != null) r.shadowCastingMode = mode;
            }
        }

        public VoxelSurfaceRenderer()
        {
            // A scene root at the origin, deliberately not parented to the driver: the driver
            // lives on the camera, and parenting the world to it would make the world fly along.
            _root = new GameObject("Voxel Surface") { hideFlags = HideFlags.DontSave };

            _materials = BuildMaterials();

            for (int m = 1; m < MaterialCount; m++)
            {
                _verts[m] = new List<Vector3>(4096);
                _normals[m] = new List<Vector3>(4096);
                _tris[m] = new List<int>(6144);
            }
        }

        // -- driving -------------------------------------------------------------

        /// <summary>
        /// Meshes dirty regions for up to <paramref name="budgetMs"/> milliseconds and drops
        /// meshes for regions that are no longer resident. The work resumes mid-region across
        /// frames, so a fly-through costs a slice per frame instead of a stall per region.
        /// </summary>
        public void Sync(ShowcaseWorld world, double budgetMs)
        {
            DropEvicted(ref world.Table);

            var deadline = Time.realtimeSinceStartupAsDouble + budgetMs * 0.001;
            var start = Time.realtimeSinceStartupAsDouble;
            bool didWork = false;

            while (Time.realtimeSinceStartupAsDouble < deadline)
            {
                if (!_mesh.Active && !BeginNextRegion(world)) break;

                didWork = true;
                if (StepRegion(world)) FinishRegion();
            }

            if (didWork)
            {
                LastRebuildMs = (Time.realtimeSinceStartupAsDouble - start) * 1000.0;
                RecomputeTotals();
            }

            PendingRebuilds = world.DirtyRegions.Count + (_mesh.Active ? 1 : 0);
        }

        /// <summary>
        /// In-progress meshing of one region. Like generation, the unit of work has to be
        /// smaller than the unit of data: a region takes hundreds of milliseconds to mesh, so
        /// the brick loop resumes from a cursor and stops wherever the budget runs out.
        /// </summary>
        private struct MeshState
        {
            public bool Active;
            public int3 Coord;
            public int Cursor;   // brick index within the region
            public int Faces;
        }

        private MeshState _mesh;

        /// <summary>Bricks visited per slice. Most are empty and cost a single pointer test.</summary>
        private const int BricksPerSlice = 8192;

        private bool BeginNextRegion(ShowcaseWorld world)
        {
            while (world.DirtyRegions.Count > 0)
            {
                int3 rc = default;
                foreach (var c in world.DirtyRegions) { rc = c; break; }
                world.DirtyRegions.Remove(rc);

                if (!world.Table.IsResident(rc))
                {
                    RemoveSurface(rc);
                    continue;
                }

                for (int m = 1; m < MaterialCount; m++)
                {
                    _verts[m].Clear();
                    _normals[m].Clear();
                    _tris[m].Clear();
                }

                _mesh = new MeshState { Active = true, Coord = rc, Cursor = 0, Faces = 0 };
                return true;
            }

            return false;
        }

        /// <summary>Advances meshing by one slice. Returns true when the region is complete.</summary>
        private bool StepRegion(ShowcaseWorld world)
        {
            ref var table = ref world.Table;
            ref var pool = ref world.Pool;

            // The region can be evicted or edited mid-build; if it is gone, abandon the work
            // rather than meshing a region that no longer exists.
            if (!table.TryGetRegion(_mesh.Coord, out var region))
            {
                _mesh = default;
                return false;
            }

            int3 originVoxel = _mesh.Coord * ShowcaseWorld.RegionVoxelEdge;
            int total = VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge * VoxelDimensions.RegionEdge;
            int end = math.min(_mesh.Cursor + BricksPerSlice, total);

            for (int i = _mesh.Cursor; i < end; i++)
            {
                int bx = i & VoxelDimensions.RegionEdgeMask;
                int by = (i >> VoxelDimensions.RegionEdgeLog2) & VoxelDimensions.RegionEdgeMask;
                int bz = i >> (VoxelDimensions.RegionEdgeLog2 * 2);

                var brick = region.GetBrick(bx, by, bz);
                if (brick.IsEmpty) continue;

                // Interior rejection by brick reference alone: six uniform solid neighbours mean
                // no face of this brick can be exposed. Six pointer reads instead of 384 voxel
                // probes, and it is why underground rock costs nothing to mesh.
                if (brick.IsUniform && NeighboursAllSolid(ref table, region, bx, by, bz))
                    continue;

                LoadBrickMaterials(in pool, brick);
                _mesh.Faces += EmitBrick(ref table, in pool, originVoxel + new int3(bx, by, bz) * E);
            }

            _mesh.Cursor = end;
            return _mesh.Cursor >= total;
        }

        private void FinishRegion()
        {
            var surface = GetOrCreateSurface(_mesh.Coord);
            surface.Faces = _mesh.Faces;
            surface.Vertices = 0;

            for (int m = 1; m < MaterialCount; m++)
            {
                var mesh = surface.Meshes[m];
                mesh.Clear();

                if (_verts[m].Count == 0)
                {
                    surface.Renderers[m].enabled = false;
                    continue;
                }

                mesh.SetVertices(_verts[m]);
                mesh.SetNormals(_normals[m]);
                mesh.SetTriangles(_tris[m], 0);
                mesh.RecalculateBounds();

                surface.Vertices += _verts[m].Count;
                surface.Renderers[m].enabled = true;
            }

            _mesh = default;
        }

        private void DropEvicted(ref RegionTable table)
        {
            List<int3> gone = null;

            foreach (var kv in _surfaces)
                if (!table.IsResident(kv.Key))
                    (gone ??= new List<int3>()).Add(kv.Key);

            if (gone == null) return;
            foreach (var rc in gone) RemoveSurface(rc);
        }

        private void RemoveSurface(int3 rc)
        {
            if (!_surfaces.TryGetValue(rc, out var s)) return;

            foreach (var m in s.Meshes) DestroyObject(m);
            DestroyObject(s.Root);
            _surfaces.Remove(rc);
        }

        private void RecomputeTotals()
        {
            int faces = 0, verts = 0;
            foreach (var s in _surfaces.Values)
            {
                faces += s.Faces;
                verts += s.Vertices;
            }

            FaceCount = faces;
            VertexCount = verts;
        }

        // -- meshing -------------------------------------------------------------

        /// <summary>Copies a brick's 512 materials into scratch so meshing avoids per-voxel map lookups.</summary>
        private void LoadBrickMaterials(in BrickPool pool, BrickRef brick)
        {
            if (brick.IsUniform)
            {
                var m = brick.UniformMaterial;
                for (int i = 0; i < _brickMaterials.Length; i++) _brickMaterials[i] = m;
                return;
            }

            int offset = pool.VoxelOffset(brick.PoolIndex);
            var voxels = pool.Voxels;
            for (int i = 0; i < _brickMaterials.Length; i++)
                _brickMaterials[i] = voxels[offset + i];
        }

        private static bool NeighboursAllSolid(ref RegionTable table, in Region region, int bx, int by, int bz)
        {
            return BrickSolid(ref table, region, bx + 1, by, bz)
                && BrickSolid(ref table, region, bx - 1, by, bz)
                && BrickSolid(ref table, region, bx, by + 1, bz)
                && BrickSolid(ref table, region, bx, by - 1, bz)
                && BrickSolid(ref table, region, bx, by, bz + 1)
                && BrickSolid(ref table, region, bx, by, bz - 1);
        }

        /// <summary>
        /// True when a neighbouring brick is *entirely* solid. A mixed brick answers false even
        /// though it may be nearly full — the per-voxel pass then decides, which is correct and
        /// costs only the bricks that actually border a surface.
        ///
        /// A neighbour in a non-resident region answers false, so the edge of the streamed world
        /// is meshed as a wall rather than silently opening into nothing.
        /// </summary>
        private static bool BrickSolid(ref RegionTable table, in Region region, int bx, int by, int bz)
        {
            if ((uint)bx < VoxelDimensions.RegionEdge &&
                (uint)by < VoxelDimensions.RegionEdge &&
                (uint)bz < VoxelDimensions.RegionEdge)
            {
                var r = region.GetBrick(bx, by, bz);
                return r.IsUniform && r.UniformMaterial != VoxelDimensions.MaterialEmpty;
            }

            var neighbourRegion = region.Coord + new int3(
                bx < 0 ? -1 : bx >= VoxelDimensions.RegionEdge ? 1 : 0,
                by < 0 ? -1 : by >= VoxelDimensions.RegionEdge ? 1 : 0,
                bz < 0 ? -1 : bz >= VoxelDimensions.RegionEdge ? 1 : 0);

            if (!table.TryGetRegion(neighbourRegion, out var other)) return false;

            var brick = other.GetBrick(bx & VoxelDimensions.RegionEdgeMask,
                                       by & VoxelDimensions.RegionEdgeMask,
                                       bz & VoxelDimensions.RegionEdgeMask);
            return brick.IsUniform && brick.UniformMaterial != VoxelDimensions.MaterialEmpty;
        }

        /// <summary>Voxel index strides inside a brick: x is 1, y is 8, z is 64.</summary>
        private static readonly int[] s_Strides = { 1, E, E * E };

        /// <summary>Greedy-meshes all six face directions of one brick. Returns quads emitted.</summary>
        private int EmitBrick(ref RegionTable table, in BrickPool pool, int3 brickBase)
        {
            int faces = 0;

            for (int axis = 0; axis < 3; axis++)
            {
                int axisA = (axis + 1) % 3;
                int axisB = (axis + 2) % 3;

                for (int sign = -1; sign <= 1; sign += 2)
                for (int layer = 0; layer < E; layer++)
                {
                    BuildMask(ref table, in pool, brickBase, axis, axisA, axisB, sign, layer);
                    faces += MergeMask(brickBase, axis, axisA, axisB, sign, layer);
                }
            }

            return faces;
        }

        /// <summary>
        /// Fills the 8x8 face mask for one layer: a cell carries a material when that voxel is
        /// solid and the voxel across the face is not.
        ///
        /// Index arithmetic is done with precomputed strides rather than per-cell coordinate
        /// composition — this is the innermost loop of meshing and runs 3072 times per brick.
        /// </summary>
        private void BuildMask(ref RegionTable table, in BrickPool pool, int3 brickBase,
                               int axis, int axisA, int axisB, int sign, int layer)
        {
            int strideAxis = s_Strides[axis];
            int strideA = s_Strides[axisA];
            int strideB = s_Strides[axisB];

            int neighbourLayer = layer + sign;
            bool crossesBrick = (uint)neighbourLayer >= E;
            int layerBase = layer * strideAxis;

            for (int b = 0; b < E; b++)
            {
                int rowBase = layerBase + b * strideB;

                for (int a = 0; a < E; a++)
                {
                    int index = rowBase + a * strideA;
                    byte material = _brickMaterials[index];

                    if (material == VoxelDimensions.MaterialEmpty)
                    {
                        _mask[a + b * E] = 0;
                        continue;
                    }

                    bool neighbourSolid;

                    if (!crossesBrick)
                    {
                        neighbourSolid = _brickMaterials[index + sign * strideAxis]
                                         != VoxelDimensions.MaterialEmpty;
                    }
                    else
                    {
                        // Only the two outer layers of each direction land here, and they are
                        // the ones that must agree with the neighbouring brick or region.
                        var local = int3.zero;
                        local[axis] = neighbourLayer;
                        local[axisA] = a;
                        local[axisB] = b;
                        neighbourSolid = VoxelAccess.IsSolid(ref table, in pool, brickBase + local);
                    }

                    _mask[a + b * E] = neighbourSolid ? (byte)0 : ClampMaterial(material);
                }
            }
        }

        private static byte ClampMaterial(byte material) =>
            material < MaterialCount ? material : ShowcaseWorld.MatStone;

        /// <summary>
        /// Merges the mask into as few rectangles as possible and emits one quad each — the
        /// greedy meshing step. A flat 8x8 face of one material becomes a single quad.
        /// </summary>
        private int MergeMask(int3 brickBase, int axis, int axisA, int axisB, int sign, int layer)
        {
            int quads = 0;

            for (int b = 0; b < E; b++)
            for (int a = 0; a < E; a++)
            {
                byte material = _mask[a + b * E];
                if (material == 0) continue;

                int width = 1;
                while (a + width < E && _mask[a + width + b * E] == material) width++;

                int height = 1;
                bool extend = true;
                while (b + height < E && extend)
                {
                    for (int k = 0; k < width; k++)
                    {
                        if (_mask[a + k + (b + height) * E] == material) continue;
                        extend = false;
                        break;
                    }

                    if (extend) height++;
                }

                for (int hb = 0; hb < height; hb++)
                for (int ha = 0; ha < width; ha++)
                    _mask[a + ha + (b + hb) * E] = 0;

                EmitQuad(material, brickBase, axis, axisA, axisB, sign, layer, a, b, width, height);
                quads++;
            }

            return quads;
        }

        private void EmitQuad(byte material, int3 brickBase, int axis, int axisA, int axisB,
                              int sign, int layer, int a, int b, int width, int height)
        {
            // Face plane sits on the far side of the voxel when the normal points positive.
            int planeVoxel = brickBase[axis] + layer + (sign > 0 ? 1 : 0);
            int a0 = brickBase[axisA] + a;
            int b0 = brickBase[axisB] + b;

            var p0 = Corner(axis, axisA, axisB, planeVoxel, a0, b0);
            var p1 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0);
            var p2 = Corner(axis, axisA, axisB, planeVoxel, a0 + width, b0 + height);
            var p3 = Corner(axis, axisA, axisB, planeVoxel, a0, b0 + height);

            var n = Vector3.zero;
            n[axis] = sign;

            var verts = _verts[material];
            var normals = _normals[material];
            var tris = _tris[material];

            int i = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(p2); verts.Add(p3);
            normals.Add(n); normals.Add(n); normals.Add(n); normals.Add(n);

            // Orient the winding from the geometry rather than assuming the axis order agrees
            // with the normal — a flipped quad is simply invisible, which is a miserable bug
            // to chase visually.
            bool flip = Vector3.Dot(Vector3.Cross(p1 - p0, p2 - p0), n) < 0f;

            if (flip)
            {
                tris.Add(i + 0); tris.Add(i + 2); tris.Add(i + 1);
                tris.Add(i + 0); tris.Add(i + 3); tris.Add(i + 2);
            }
            else
            {
                tris.Add(i + 0); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i + 0); tris.Add(i + 2); tris.Add(i + 3);
            }
        }

        private static Vector3 Corner(int axis, int axisA, int axisB, int plane, int a, int b)
        {
            var v = Vector3.zero;
            v[axis] = plane * VoxelSize;
            v[axisA] = a * VoxelSize;
            v[axisB] = b * VoxelSize;
            return v;
        }

        // -- scene objects -------------------------------------------------------

        private RegionSurface GetOrCreateSurface(int3 regionCoord)
        {
            if (_surfaces.TryGetValue(regionCoord, out var existing)) return existing;

            var surface = new RegionSurface
            {
                Root = new GameObject($"Region {regionCoord.x},{regionCoord.y},{regionCoord.z}")
                {
                    hideFlags = HideFlags.DontSave,
                },
            };

            surface.Root.transform.SetParent(_root.transform, false);

            for (int m = 1; m < MaterialCount; m++)
            {
                var go = new GameObject(ShowcaseWorld.MaterialNames[m]) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(surface.Root.transform, false);

                surface.Meshes[m] = new Mesh
                {
                    name = $"Surface.{regionCoord.x}.{regionCoord.z}.{ShowcaseWorld.MaterialNames[m]}",
                    indexFormat = IndexFormat.UInt32,
                    hideFlags = HideFlags.DontSave,
                };

                go.AddComponent<MeshFilter>().sharedMesh = surface.Meshes[m];
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = _materials[m];
                mr.shadowCastingMode = _castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                surface.Renderers[m] = mr;
            }

            _surfaces.Add(regionCoord, surface);
            return surface;
        }

        /// <summary>
        /// Picks a shader that matches the *active* pipeline.
        ///
        /// URP is installed, but this project has no Render Pipeline Asset assigned in Graphics
        /// Settings, so it runs the built-in pipeline — and a URP shader there renders solid
        /// magenta. Choosing off <see cref="GraphicsSettings.currentRenderPipeline"/> makes the
        /// showcase correct either way instead of correct only once someone assigns the asset.
        /// </summary>
        private static Material[] BuildMaterials()
        {
            bool srp = GraphicsSettings.currentRenderPipeline != null;

            var shader = srp
                ? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")
                : Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");

            if (shader == null)
                Debug.LogWarning("VoxelShowcase: no usable surface shader found; voxels will not draw.");

            var colours = new[]
            {
                Color.magenta,                              // empty — never drawn
                new Color(0.43f, 0.45f, 0.48f),             // cool weathered limestone
                new Color(0.46f, 0.29f, 0.14f),             // wood
                new Color(0.82f, 0.72f, 0.46f),             // sand
                new Color(0.78f, 0.48f, 0.18f),             // warm lit glass
                new Color(0.15f, 0.15f, 0.17f),             // bedrock
                new Color(0.23f, 0.25f, 0.28f),             // structural / cave stone
                new Color(0.24f, 0.26f, 0.32f),             // slate
                new Color(0.46f, 0.24f, 0.18f),             // tile
                new Color(0.62f, 0.12f, 0.14f),             // cloth
                new Color(0.31f, 0.44f, 0.20f),             // grass
                new Color(0.10f, 0.43f, 0.56f),             // water
                new Color(0.80f, 0.66f, 0.26f),             // gold
                new Color(0.38f, 0.31f, 0.24f),             // dirt
                new Color(0.32f, 0.40f, 0.24f),             // moss
                new Color(0.16f, 0.19f, 0.18f),             // dark leaded window glass
            };

            var materials = new Material[colours.Length];
            for (int i = 0; i < colours.Length; i++)
            {
                materials[i] = new Material(shader)
                {
                    name = $"VoxelShowcase.{ShowcaseWorld.MaterialNames[i]}",
                    hideFlags = HideFlags.DontSave,
                };

                // Built-in Standard reads _Color; URP Lit reads _BaseColor. Setting a property
                // the shader does not declare is a no-op, so both are safe to set.
                materials[i].SetColor("_Color", colours[i]);
                materials[i].SetColor("_BaseColor", colours[i]);
                bool glazing = i == ShowcaseWorld.MatGlass || i == Mat.LitWindow;
                materials[i].SetFloat("_Glossiness", glazing ? 0.7f : 0.05f);
                materials[i].SetFloat("_Smoothness", glazing ? 0.7f : 0.05f);
                materials[i].SetFloat("_Metallic", 0f);
            }

            return materials;
        }

        public void Dispose()
        {
            var coords = new List<int3>(_surfaces.Keys);
            foreach (var rc in coords) RemoveSurface(rc);

            if (_materials != null)
                foreach (var m in _materials) DestroyObject(m);

            DestroyObject(_root);
        }

        /// <summary>
        /// Destroy is deferred and illegal outside play mode, but the driver is
        /// <c>ExecuteAlways</c> and disposes on every domain reload, so edit mode takes the
        /// immediate path. Safe because everything here is built at runtime with
        /// <see cref="HideFlags.DontSave"/> and is never a project asset.
        /// </summary>
        private static void DestroyObject(UnityEngine.Object o)
        {
            if (o == null) return;

            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }
}
