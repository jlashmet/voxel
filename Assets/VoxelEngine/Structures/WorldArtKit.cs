using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Structures
{
    public enum WorldArtSurfaceRole
    {
        Rock,
        Turf,
        Stone,
        Moss,
        Water,
        Waterfall,
        Foam,
        Bark,
        Leaf,
        Roof,
        FlowerWarm,
        FlowerCool
    }

    /// <summary>
    /// Material-role palette for reusable world-art pieces. Pieces never know about a biome's
    /// concrete materials; the scene/biome supplies those roles when it instantiates the kit.
    /// </summary>
    public sealed class WorldArtPalette
    {
        private readonly Dictionary<WorldArtSurfaceRole, Material> _materials =
            new Dictionary<WorldArtSurfaceRole, Material>();

        public WorldArtPalette Set(WorldArtSurfaceRole role, Material material)
        {
            _materials[role] = material;
            return this;
        }

        public Material Get(WorldArtSurfaceRole role)
        {
            Material material;
            if (_materials.TryGetValue(role, out material) && material != null) return material;
            throw new InvalidOperationException("WorldArtPalette is missing material role " + role + ".");
        }
    }

    /// <summary>
    /// A reusable art-kit instance with named sockets. Sockets make generated pieces composable:
    /// a pool can sit on a ledge's top socket, a waterfall can connect two lip sockets, and a
    /// tower/tree can be attached to any generated terrain piece without knowing its dimensions.
    /// </summary>
    public sealed class WorldArtPiece
    {
        private readonly Dictionary<string, Transform> _sockets = new Dictionary<string, Transform>();

        public GameObject Root { get; private set; }
        public Transform Transform { get { return Root.transform; } }

        internal WorldArtPiece(GameObject root)
        {
            Root = root;
        }

        public Transform AddSocket(string name, Vector3 localPosition)
        {
            return AddSocket(name, localPosition, Quaternion.identity);
        }

        public Transform AddSocket(string name, Vector3 localPosition, Quaternion localRotation)
        {
            GameObject socket = new GameObject("Socket " + name);
            socket.transform.SetParent(Root.transform, false);
            socket.transform.localPosition = localPosition;
            socket.transform.localRotation = localRotation;
            _sockets[name] = socket.transform;
            return socket.transform;
        }

        public Transform Socket(string name)
        {
            Transform socket;
            if (_sockets.TryGetValue(name, out socket)) return socket;
            throw new KeyNotFoundException("World-art piece '" + Root.name + "' has no socket '" + name + "'.");
        }
    }

    /// <summary>
    /// Presentation-side reusable shape vocabulary for the illustrated voxel world. These meshes
    /// intentionally preserve chunky/faceted construction while softening silhouettes. Authoritative
    /// destructible equivalents live in WorldArtVoxelShapes; this kit is the render/lookdev layer.
    /// </summary>
    public static class WorldArtKit
    {
        public static WorldArtPiece CliffLedge(Transform parent, string name, Vector3 position,
            Vector3 size, int seed, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, position);

            GameObject body = MeshObject(piece.Transform, name + " cliff rock",
                CreateIrregularLedgeMesh(size, seed), palette.Get(WorldArtSurfaceRole.Rock));
            body.transform.localPosition = Vector3.zero;

            GameObject cap = MeshObject(piece.Transform, name + " grass turf cap",
                CreateTurfCapMesh(size, seed), palette.Get(WorldArtSurfaceRole.Turf));
            cap.transform.localPosition = Vector3.up * (size.y * 0.5f + 0.035f);
            cap.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            piece.AddSocket("top", Vector3.up * (size.y * 0.5f + 0.05f));
            piece.AddSocket("front-lip", new Vector3(0f, size.y * 0.5f + 0.05f, -size.z * 0.38f));
            piece.AddSocket("back-catch", new Vector3(0f, size.y * 0.5f + 0.05f, size.z * 0.34f));
            piece.AddSocket("left-edge", new Vector3(-size.x * 0.42f, size.y * 0.42f, 0f));
            piece.AddSocket("right-edge", new Vector3(size.x * 0.42f, size.y * 0.42f, 0f));
            return piece;
        }

        public static WorldArtPiece Pool(Transform parent, string name, Vector3 localPosition,
            float radiusX, float radiusZ, int seed, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, localPosition);
            GameObject water = MeshObject(piece.Transform, name + " turquoise water pool",
                CreateEllipseMesh(radiusX, radiusZ, seed), palette.Get(WorldArtSurfaceRole.Water));
            water.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            piece.AddSocket("surface", Vector3.up * 0.015f);
            piece.AddSocket("front-lip", new Vector3(0f, 0.015f, -radiusZ * 0.78f));
            piece.AddSocket("back", new Vector3(0f, 0.015f, radiusZ * 0.70f));
            return piece;
        }

        public static WorldArtPiece WaterfallBetween(Transform parent, string name, Transform from,
            Transform to, float width, int seed, WorldArtPalette palette)
        {
            Vector3 a = from.position;
            Vector3 b = to.position;
            WorldArtPiece piece = Piece(parent, name, Vector3.zero);
            GameObject ribbon = MeshObject(piece.Transform, name + " waterfall cascade",
                CreateRibbonMesh(a, b, width, seed), palette.Get(WorldArtSurfaceRole.Waterfall));
            ribbon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            FoamCluster(piece.Transform, name + " waterfall foam", b, width,
                palette.Get(WorldArtSurfaceRole.Foam), seed);
            piece.AddSocket("source", a);
            piece.AddSocket("impact", b);
            return piece;
        }

        public static WorldArtPiece RoundedArch(Transform parent, string name, Vector3 position,
            float halfOpening, int pierRows, Vector3 blockSize, float depth, bool broken,
            int seed, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, position);
            Material stone = palette.Get(WorldArtSurfaceRole.Stone);
            Material moss = palette.Get(WorldArtSurfaceRole.Moss);

            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < pierRows; row++)
                {
                    if (broken && side > 0 && row == pierRows - 1) continue;
                    float jitter = (Hash(seed + row * 19 + side * 7) - 0.5f) * 0.055f;
                    Vector3 p = new Vector3(side * halfOpening, row * blockSize.y, jitter);
                    GameObject block = BeveledBlockObject(piece.Transform, name + " ruin stone pier",
                        p, new Vector3(blockSize.x, blockSize.y * 0.94f, depth),
                        Mathf.Min(0.08f, blockSize.y * 0.16f), stone);
                    block.transform.localRotation = Quaternion.Euler(0f, 0f,
                        side * ((row & 1) == 0 ? 1.5f : -1.0f));
                }
            }

            float archY = pierRows * blockSize.y - blockSize.y * 0.22f;
            const int segments = 11;
            for (int i = 0; i <= segments; i++)
            {
                if (broken && (i == 1 || i == 9)) continue;
                float t = i / (float)segments;
                float a = Mathf.Lerp(Mathf.PI, 0f, t);
                Vector3 p = new Vector3(Mathf.Cos(a) * halfOpening,
                    archY + Mathf.Sin(a) * halfOpening, 0f);
                GameObject block = BeveledBlockObject(piece.Transform, name + " ruin stone arch",
                    p, new Vector3(blockSize.x * 1.02f, blockSize.y * 0.92f, depth),
                    Mathf.Min(0.08f, blockSize.y * 0.16f), stone);
                block.transform.localRotation = Quaternion.Euler(0f, 0f, -a * Mathf.Rad2Deg + 90f);
            }

            if (broken)
            {
                BeveledBlockObject(piece.Transform, name + " broken ruin crown",
                    new Vector3(-halfOpening, pierRows * blockSize.y + halfOpening * 0.94f, 0f),
                    new Vector3(blockSize.x * 1.06f, blockSize.y * 1.35f, depth),
                    0.08f, stone).transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
            }

            piece.AddSocket("opening", new Vector3(0f, archY * 0.45f, 0f));
            piece.AddSocket("crown", new Vector3(0f, archY + halfOpening, 0f));
            piece.AddSocket("left-pier", new Vector3(-halfOpening, archY * 0.58f, -depth * 0.45f));
            piece.AddSocket("right-pier", new Vector3(halfOpening, archY * 0.58f, -depth * 0.45f));
            piece.AddSocket("left-base", new Vector3(-halfOpening, 0f, 0f));
            piece.AddSocket("right-base", new Vector3(halfOpening, 0f, 0f));

            MossCluster(piece.Transform, name + " moss crown",
                new Vector3(-halfOpening * 0.70f, archY + halfOpening * 0.72f, -depth * 0.48f),
                blockSize.x * 0.72f, seed + 73, moss);
            return piece;
        }

        public static WorldArtPiece BeveledBlock(Transform parent, string name, Vector3 localPosition,
            Vector3 size, float bevel, WorldArtSurfaceRole role, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, localPosition);
            BeveledBlockObject(piece.Transform, name, Vector3.zero, size, bevel, palette.Get(role));
            piece.AddSocket("top", new Vector3(0f, size.y * 0.5f, 0f));
            return piece;
        }

        public static WorldArtPiece CastleTower(Transform parent, string name, Vector3 localPosition,
            float radius, float height, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, localPosition);
            Material stone = palette.Get(WorldArtSurfaceRole.Stone);
            Material roof = palette.Get(WorldArtSurfaceRole.Roof);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = name + " tower stone";
            body.transform.SetParent(piece.Transform, false);
            body.transform.localPosition = Vector3.up * (height * 0.5f);
            body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            body.GetComponent<Renderer>().sharedMaterial = stone;
            RemoveCollider(body);

            GameObject spire = MeshObject(piece.Transform, name + " roof spire",
                CreateConeMesh(radius * 1.32f, Mathf.Max(radius * 2.3f, 0.75f), 14), roof);
            spire.transform.localPosition = Vector3.up * height;

            piece.AddSocket("base", Vector3.zero);
            piece.AddSocket("roof", Vector3.up * height);
            piece.AddSocket("spire", Vector3.up * (height + Mathf.Max(radius * 2.3f, 0.75f)));
            piece.AddSocket("ivy", new Vector3(-radius * 0.72f, height * 0.55f, -radius * 0.70f));
            return piece;
        }

        public static WorldArtPiece StorybookTree(Transform parent, string name, Vector3 localPosition,
            float height, float canopyRadius, int seed, WorldArtPalette palette)
        {
            WorldArtPiece piece = Piece(parent, name, localPosition);
            Material bark = palette.Get(WorldArtSurfaceRole.Bark);
            Material leaf = palette.Get(WorldArtSurfaceRole.Leaf);

            Vector3 trunkTop = new Vector3(0f, height * 0.72f, 0f);
            CylinderBetween(piece.Transform, name + " bark trunk", Vector3.zero, trunkTop,
                Mathf.Max(0.10f, height * 0.035f), bark);

            Vector3[] branchEnds =
            {
                new Vector3(-canopyRadius * 0.55f, height * 0.82f, 0.08f),
                new Vector3(canopyRadius * 0.52f, height * 0.86f, -0.12f),
                new Vector3(-canopyRadius * 0.18f, height * 0.96f, 0.18f),
                new Vector3(canopyRadius * 0.20f, height * 0.98f, 0.10f)
            };
            for (int i = 0; i < branchEnds.Length; i++)
                CylinderBetween(piece.Transform, name + " bark branch", trunkTop * 0.82f,
                    branchEnds[i], Mathf.Max(0.06f, height * 0.020f), bark);

            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2f / 8f;
                float jitter = 0.78f + Hash(seed + i * 31) * 0.34f;
                Vector3 p = new Vector3(Mathf.Cos(a) * canopyRadius * 0.52f * jitter,
                    height * (0.86f + (i % 3) * 0.055f),
                    Mathf.Sin(a) * canopyRadius * 0.30f * jitter);
                Blob(piece.Transform, name + " leaf canopy", p,
                    new Vector3(canopyRadius * 0.70f, canopyRadius * 0.48f, canopyRadius * 0.56f), leaf);
            }

            piece.AddSocket("root", Vector3.zero);
            piece.AddSocket("crown", new Vector3(0f, height, 0f));
            piece.AddSocket("branch-left", branchEnds[0]);
            piece.AddSocket("branch-right", branchEnds[1]);
            return piece;
        }

        public static void Vine(Transform parent, string name, Vector3 startWorld, Vector3 endWorld,
            float thickness, int seed, WorldArtPalette palette)
        {
            Material moss = palette.Get(WorldArtSurfaceRole.Moss);
            const int segments = 9;
            Vector3 previous = startWorld;
            for (int i = 1; i <= segments; i++)
            {
                float t = i / (float)segments;
                Vector3 p = Vector3.Lerp(startWorld, endWorld, t);
                p.x += Mathf.Sin(t * Mathf.PI * 2.4f + seed) * thickness * 1.8f;
                p.z -= Mathf.Sin(t * Mathf.PI * 3.1f + seed * 0.7f) * thickness * 0.8f;
                CylinderBetween(parent, name + " vine", previous, p, thickness, moss, true);
                if ((i & 1) == 0)
                {
                    Blob(parent, name + " leaf", p + new Vector3((i % 4 == 0 ? -1f : 1f) * thickness * 2.4f, 0f, -thickness),
                        new Vector3(thickness * 3.8f, thickness * 1.8f, thickness * 2.3f), moss, true);
                }
                previous = p;
            }
        }

        public static void MossCluster(Transform parent, string name, Vector3 localPosition,
            float radius, int seed, Material material)
        {
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                float r = radius * (0.22f + Hash(seed + i * 17) * 0.22f);
                Blob(parent, name, localPosition + new Vector3(Mathf.Cos(a) * radius * 0.34f,
                    (i % 2) * radius * 0.10f, Mathf.Sin(a) * radius * 0.24f),
                    new Vector3(r * 1.9f, r, r * 1.45f), material);
            }
        }

        public static void FlowerPatch(Transform parent, string name, Vector3 centerWorld,
            float radius, int count, int seed, WorldArtPalette palette)
        {
            Material stem = palette.Get(WorldArtSurfaceRole.Moss);
            Material warm = palette.Get(WorldArtSurfaceRole.FlowerWarm);
            Material cool = palette.Get(WorldArtSurfaceRole.FlowerCool);
            for (int i = 0; i < count; i++)
            {
                float a = Hash(seed + i * 37) * Mathf.PI * 2f;
                float rr = Mathf.Sqrt(Hash(seed + i * 53 + 11)) * radius;
                Vector3 p = centerWorld + new Vector3(Mathf.Cos(a) * rr, 0f, Mathf.Sin(a) * rr * 0.62f);
                float h = 0.13f + Hash(seed + i * 71 + 5) * 0.16f;
                CylinderBetween(parent, name + " flower stem", p, p + Vector3.up * h, 0.012f, stem, true);
                Material petal = (i & 1) == 0 ? warm : cool;
                Blob(parent, name + " flower blossom", p + Vector3.up * (h + 0.025f),
                    new Vector3(0.075f, 0.045f, 0.070f), petal, true);
            }
        }

        private static WorldArtPiece Piece(Transform parent, string name, Vector3 localPosition)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            return new WorldArtPiece(root);
        }

        private static GameObject BeveledBlockObject(Transform parent, string name, Vector3 localPosition,
            Vector3 size, float bevel, Material material)
        {
            GameObject go = MeshObject(parent, name, CreateBeveledBoxMesh(size, bevel), material);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return go;
        }

        private static GameObject Blob(Transform parent, string name, Vector3 position, Vector3 scale,
            Material material, bool worldSpace = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, worldSpace);
            if (worldSpace) go.transform.position = position;
            else go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(go);
            return go;
        }

        private static void CylinderBetween(Transform parent, string name, Vector3 a, Vector3 b,
            float radius, Material material, bool worldSpace = false)
        {
            Vector3 d = b - a;
            float length = d.magnitude;
            if (length < 0.0001f) return;
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, worldSpace);
            Vector3 mid = (a + b) * 0.5f;
            if (worldSpace) go.transform.position = mid;
            else go.transform.localPosition = mid;
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            go.GetComponent<Renderer>().sharedMaterial = material;
            RemoveCollider(go);
        }

        private static void FoamCluster(Transform parent, string name, Vector3 centerWorld, float width,
            Material material, int seed)
        {
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-width * 0.46f, width * 0.46f, i / 6f);
                float z = (Hash(seed + i * 29) - 0.5f) * width * 0.22f;
                Blob(parent, name, centerWorld + new Vector3(x, 0.035f, z),
                    new Vector3(width * 0.16f, width * 0.045f, width * 0.10f), material, true);
            }
        }

        private static Mesh CreateIrregularLedgeMesh(Vector3 scale, int seed)
        {
            const int sides = 16;
            var vertices = new List<Vector3>(sides * 3 + 1);
            var triangles = new List<int>(sides * 15);
            for (int ring = 0; ring < 3; ring++)
            {
                float y = ring == 0 ? -scale.y * 0.50f : ring == 1 ? -scale.y * 0.04f : scale.y * 0.50f;
                float ringScale = ring == 0 ? 0.76f : ring == 1 ? 1.00f : 0.91f;
                for (int i = 0; i < sides; i++)
                {
                    float a = i * Mathf.PI * 2f / sides;
                    float radial = 0.88f + Hash(seed + i * 17) * 0.20f;
                    float depth = 0.91f + Hash(seed * 5 + i * 23) * 0.15f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * scale.x * 0.5f * ringScale * radial,
                        y, Mathf.Sin(a) * scale.z * 0.5f * ringScale * depth));
                }
            }
            for (int ring = 0; ring < 2; ring++)
            {
                int lower = ring * sides;
                int upper = (ring + 1) * sides;
                for (int i = 0; i < sides; i++)
                {
                    int j = (i + 1) % sides;
                    AddQuad(vertices, triangles, lower + i, upper + i, upper + j, lower + j);
                }
            }
            int bottom = vertices.Count;
            vertices.Add(new Vector3(0f, -scale.y * 0.50f, 0f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                triangles.Add(bottom); triangles.Add(j); triangles.Add(i);
            }
            return Mesh("WorldArtKit irregular ledge", vertices, triangles);
        }

        private static Mesh CreateTurfCapMesh(Vector3 scale, int seed)
        {
            const int sides = 16;
            var vertices = new List<Vector3>(sides + 1) { Vector3.zero };
            var triangles = new List<int>(sides * 3);
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                float radial = 0.88f + Hash(seed + i * 17) * 0.20f;
                float depth = 0.91f + Hash(seed * 5 + i * 23) * 0.15f;
                vertices.Add(new Vector3(Mathf.Cos(a) * scale.x * 0.5f * 0.93f * radial, 0f,
                    Mathf.Sin(a) * scale.z * 0.5f * 0.93f * depth));
            }
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(0); triangles.Add(next + 1); triangles.Add(i + 1);
            }
            return Mesh("WorldArtKit turf cap", vertices, triangles);
        }

        private static Mesh CreateEllipseMesh(float rx, float rz, int seed)
        {
            const int segments = 48;
            var vertices = new List<Vector3>(segments + 1) { Vector3.zero };
            var triangles = new List<int>(segments * 3);
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                float jitter = 0.96f + Hash(seed + i * 13) * 0.07f;
                vertices.Add(new Vector3(Mathf.Cos(a) * rx * jitter, 0f, Mathf.Sin(a) * rz * jitter));
            }
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                triangles.Add(0); triangles.Add(next + 1); triangles.Add(i + 1);
            }
            return Mesh("WorldArtKit ellipse pool", vertices, triangles);
        }

        private static Mesh CreateRibbonMesh(Vector3 a, Vector3 b, float width, int seed)
        {
            const int steps = 10;
            var vertices = new List<Vector3>((steps + 1) * 2);
            var triangles = new List<int>(steps * 6);
            Vector3 direction = b - a;
            Vector3 side = new Vector3(-direction.z, 0f, direction.x);
            if (side.sqrMagnitude < 0.0001f) side = Vector3.right;
            side.Normalize();
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * width * 0.10f;
                p += side * Mathf.Sin(t * Mathf.PI * 2f + seed * 0.17f) * width * 0.055f;
                float w = width * (0.90f + Mathf.Sin(t * Mathf.PI) * 0.14f);
                vertices.Add(p - side * w * 0.5f);
                vertices.Add(p + side * w * 0.5f);
            }
            for (int i = 0; i < steps; i++)
            {
                int k = i * 2;
                triangles.Add(k); triangles.Add(k + 3); triangles.Add(k + 1);
                triangles.Add(k); triangles.Add(k + 2); triangles.Add(k + 3);
            }
            return Mesh("WorldArtKit waterfall ribbon", vertices, triangles);
        }

        private static Mesh CreateConeMesh(float radius, float height, int sides)
        {
            var vertices = new List<Vector3>(sides + 2);
            var triangles = new List<int>(sides * 6);
            vertices.Add(new Vector3(0f, height, 0f));
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
            int bottom = vertices.Count;
            vertices.Add(Vector3.zero);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                triangles.Add(0); triangles.Add(i + 1); triangles.Add(next + 1);
                triangles.Add(bottom); triangles.Add(next + 1); triangles.Add(i + 1);
            }
            return Mesh("WorldArtKit cone", vertices, triangles);
        }

        private static Mesh CreateBeveledBoxMesh(Vector3 size, float bevel)
        {
            float hx = size.x * 0.5f, hy = size.y * 0.5f, hz = size.z * 0.5f;
            bevel = Mathf.Clamp(bevel, 0.001f, Mathf.Min(hx, Mathf.Min(hy, hz)) * 0.48f);
            var v = new List<Vector3>(96);
            var t = new List<int>(144);

            // Six inset planar faces.
            AddFace(v, t, new Vector3(hx, -hy + bevel, -hz + bevel), new Vector3(hx, hy - bevel, -hz + bevel),
                new Vector3(hx, hy - bevel, hz - bevel), new Vector3(hx, -hy + bevel, hz - bevel));
            AddFace(v, t, new Vector3(-hx, -hy + bevel, hz - bevel), new Vector3(-hx, hy - bevel, hz - bevel),
                new Vector3(-hx, hy - bevel, -hz + bevel), new Vector3(-hx, -hy + bevel, -hz + bevel));
            AddFace(v, t, new Vector3(-hx + bevel, hy, -hz + bevel), new Vector3(-hx + bevel, hy, hz - bevel),
                new Vector3(hx - bevel, hy, hz - bevel), new Vector3(hx - bevel, hy, -hz + bevel));
            AddFace(v, t, new Vector3(-hx + bevel, -hy, hz - bevel), new Vector3(-hx + bevel, -hy, -hz + bevel),
                new Vector3(hx - bevel, -hy, -hz + bevel), new Vector3(hx - bevel, -hy, hz - bevel));
            AddFace(v, t, new Vector3(-hx + bevel, -hy + bevel, hz), new Vector3(hx - bevel, -hy + bevel, hz),
                new Vector3(hx - bevel, hy - bevel, hz), new Vector3(-hx + bevel, hy - bevel, hz));
            AddFace(v, t, new Vector3(hx - bevel, -hy + bevel, -hz), new Vector3(-hx + bevel, -hy + bevel, -hz),
                new Vector3(-hx + bevel, hy - bevel, -hz), new Vector3(hx - bevel, hy - bevel, -hz));

            // Twelve chamfered edges.
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AddFace(v, t,
                    new Vector3(-hx + bevel, sy * hy, sz * (hz - bevel)),
                    new Vector3(hx - bevel, sy * hy, sz * (hz - bevel)),
                    new Vector3(hx - bevel, sy * (hy - bevel), sz * hz),
                    new Vector3(-hx + bevel, sy * (hy - bevel), sz * hz));

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                AddFace(v, t,
                    new Vector3(sx * hx, -hy + bevel, sz * (hz - bevel)),
                    new Vector3(sx * (hx - bevel), -hy + bevel, sz * hz),
                    new Vector3(sx * (hx - bevel), hy - bevel, sz * hz),
                    new Vector3(sx * hx, hy - bevel, sz * (hz - bevel)));

            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                AddFace(v, t,
                    new Vector3(sx * hx, sy * (hy - bevel), -hz + bevel),
                    new Vector3(sx * (hx - bevel), sy * hy, -hz + bevel),
                    new Vector3(sx * (hx - bevel), sy * hy, hz - bevel),
                    new Vector3(sx * hx, sy * (hy - bevel), hz - bevel));

            // Eight clipped corners.
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Vector3 px = new Vector3(sx * hx, sy * (hy - bevel), sz * (hz - bevel));
                Vector3 py = new Vector3(sx * (hx - bevel), sy * hy, sz * (hz - bevel));
                Vector3 pz = new Vector3(sx * (hx - bevel), sy * (hy - bevel), sz * hz);
                AddTriangle(v, t, px, py, pz);
            }
            return Mesh("WorldArtKit beveled block", v, t);
        }

        private static void AddFace(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 center = (a + b + c + d) * 0.25f;
            if (Vector3.Dot(normal, center) < 0f)
            {
                Vector3 tmp = b; b = d; d = tmp;
            }
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
        }

        private static void AddTriangle(List<Vector3> vertices, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 center = (a + b + c) / 3f;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), center) < 0f)
            {
                Vector3 tmp = b; b = c; c = tmp;
            }
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }

        private static void AddQuad(List<Vector3> vertices, List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        private static Mesh Mesh(string name, List<Vector3> vertices, List<int> triangles)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float Hash(int value)
        {
            unchecked
            {
                uint x = (uint)value;
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return (x & 0x00ffffffu) / 16777215f;
            }
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);
        }
    }
}
