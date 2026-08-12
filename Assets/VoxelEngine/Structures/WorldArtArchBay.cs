using UnityEngine;

namespace VoxelEngine.Structures
{
    public enum WorldArtArchDamage
    {
        Intact,
        BrokenLeftHaunch,
        BrokenRightHaunch,
        BrokenCrown
    }

    /// <summary>
    /// Reusable architectural arch assembly. This deliberately sits above individual ashlar and
    /// voussoir primitives: it models the load-bearing hierarchy of a masonry bay (plinth, piers,
    /// imposts, archivolt, backing/spandrel mass and cap) and exposes semantic attachment sockets.
    /// </summary>
    public static class WorldArtArchBay
    {
        public static WorldArtPiece Build(Transform parent, string name, Vector3 localPosition,
            float halfOpening, float pierHeight, float pierWidth, float courseHeight,
            float ringThickness, float depth, int seed, WorldArtPalette palette,
            WorldArtArchDamage damage = WorldArtArchDamage.Intact)
        {
            GameObject rootObject = new GameObject(name);
            rootObject.transform.SetParent(parent, false);
            rootObject.transform.localPosition = localPosition;
            WorldArtPiece root = new WorldArtPiece(rootObject);

            float joint = Mathf.Clamp(courseHeight * 0.055f, 0.018f, 0.045f);
            float pierCenterX = halfOpening + pierWidth * 0.5f;
            float baseHeight = courseHeight * 0.72f;
            float impostHeight = courseHeight * 0.52f;
            float impostWidth = pierWidth * 1.22f;
            float springY = pierHeight + baseHeight * 0.15f;
            float innerRadius = halfOpening;
            float outerRadius = halfOpening + ringThickness;
            float frontZ = -depth * 0.10f;
            float backingZ = depth * 0.26f;
            float backingDepth = depth * 0.62f;

            BuildPlinth(root.Transform, name, -1, pierCenterX, pierWidth, baseHeight, depth,
                seed + 11, palette);
            BuildPlinth(root.Transform, name, 1, pierCenterX, pierWidth, baseHeight, depth,
                seed + 17, palette);

            int courses = Mathf.Max(3, Mathf.RoundToInt(pierHeight / courseHeight));
            BuildPier(root.Transform, name, -1, pierCenterX, pierWidth, courseHeight, courses,
                baseHeight * 0.55f, depth, joint, seed + 101, palette);
            BuildPier(root.Transform, name, 1, pierCenterX, pierWidth, courseHeight, courses,
                baseHeight * 0.55f, depth, joint, seed + 211, palette);

            float impostY = springY - impostHeight * 0.36f;
            WorldArtStoneKit.Ashlar(root.Transform, name + " left impost",
                new Vector3(-pierCenterX, impostY, frontZ - 0.018f),
                new Vector3(impostWidth, impostHeight, depth * 1.07f),
                Mathf.Min(0.045f, impostHeight * 0.09f), seed + 307, palette);
            WorldArtStoneKit.Ashlar(root.Transform, name + " right impost",
                new Vector3(pierCenterX, impostY, frontZ - 0.018f),
                new Vector3(impostWidth, impostHeight, depth * 1.07f),
                Mathf.Min(0.045f, impostHeight * 0.09f), seed + 311, palette);

            // The visible structural ring. Many narrow radial joints make the arch read as
            // purpose-cut masonry rather than rotated rectangular blocks.
            const int voussoirCount = 17;
            int keyIndex = voussoirCount / 2;
            for (int i = 0; i < voussoirCount; i++)
            {
                if (i == keyIndex) continue;
                if (OmitVoussoir(i, voussoirCount, damage)) continue;

                float a0 = Mathf.Lerp(180f, 0f, i / (float)voussoirCount);
                float a1 = Mathf.Lerp(180f, 0f, (i + 1) / (float)voussoirCount);
                float gap = 0.72f + Hash(seed + i * 43) * 0.32f;
                float lo = Mathf.Min(a0, a1) + gap * 0.5f;
                float hi = Mathf.Max(a0, a1) - gap * 0.5f;
                float faceSet = (Hash(seed + 401 + i * 29) - 0.5f) * depth * 0.035f;

                WorldArtPiece stone = WorldArtStoneKit.Voussoir(root.Transform,
                    name + " archivolt voussoir " + i,
                    new Vector3(0f, springY, frontZ + faceSet),
                    innerRadius, outerRadius, lo, hi,
                    depth * (1.00f + (Hash(seed + i * 31) - 0.5f) * 0.035f),
                    seed + 503 + i * 71, palette);
                stone.Transform.localRotation *= Quaternion.Euler(
                    0f,
                    (Hash(seed + 601 + i * 17) - 0.5f) * 0.45f,
                    (Hash(seed + 607 + i * 19) - 0.5f) * 0.22f);
            }

            // A shallow backing ring gives the archivolt wall thickness and a darker recessed edge.
            // It is deliberately offset behind the face so it reads as construction, not trim.
            const int backingCount = 19;
            float backingInner = outerRadius + joint * 0.80f;
            float backingOuter = outerRadius + ringThickness * 0.52f;
            for (int i = 0; i < backingCount; i++)
            {
                if (OmitBacking(i, backingCount, damage)) continue;
                float a0 = Mathf.Lerp(180f, 0f, i / (float)backingCount);
                float a1 = Mathf.Lerp(180f, 0f, (i + 1) / (float)backingCount);
                float lo = Mathf.Min(a0, a1) + 0.42f;
                float hi = Mathf.Max(a0, a1) - 0.42f;
                WorldArtStoneKit.Voussoir(root.Transform, name + " backing ring " + i,
                    new Vector3(0f, springY, backingZ), backingInner, backingOuter,
                    lo, hi, backingDepth, seed + 809 + i * 37, palette);
            }

            // Keystone is intentionally more massive and proud than its neighbors, but remains a
            // radial stone so the load path stays visually believable.
            if (damage != WorldArtArchDamage.BrokenCrown)
            {
                float keyHalfAngle = 6.4f;
                WorldArtPiece key = WorldArtStoneKit.Voussoir(root.Transform, name + " keystone",
                    new Vector3(0f, springY + courseHeight * 0.035f, frontZ - depth * 0.045f),
                    innerRadius * 0.992f, outerRadius + ringThickness * 0.13f,
                    90f - keyHalfAngle, 90f + keyHalfAngle,
                    depth * 1.095f, seed + 947, palette);
                key.AddSocket("moss", new Vector3(0f, outerRadius + ringThickness * 0.10f,
                    -depth * 0.53f));
            }

            BuildSpandrels(root.Transform, name, halfOpening, pierCenterX, pierWidth,
                springY, outerRadius, ringThickness, courseHeight, backingZ, backingDepth,
                joint, seed + 1103, palette, damage);

            BuildSideReturns(root.Transform, name, pierCenterX, pierWidth, springY,
                courseHeight, depth, seed + 1301, palette, damage);

            BuildCap(root.Transform, name, pierCenterX, pierWidth, springY + outerRadius,
                courseHeight, depth, joint, seed + 1501, palette, damage);

            if (damage != WorldArtArchDamage.Intact)
                BuildFallStone(root.Transform, name, halfOpening, courseHeight, depth,
                    seed + 1709, palette, damage);

            float bayHalfWidth = pierCenterX + pierWidth * 0.62f;
            root.AddSocket("opening", new Vector3(0f, springY + innerRadius * 0.34f, -depth * 0.5f));
            root.AddSocket("crown", new Vector3(0f, springY + outerRadius, -depth * 0.5f));
            root.AddSocket("keystone", new Vector3(0f, springY + outerRadius, -depth * 0.56f));
            root.AddSocket("left-base", new Vector3(-pierCenterX, 0f, 0f));
            root.AddSocket("right-base", new Vector3(pierCenterX, 0f, 0f));
            root.AddSocket("wall-left", new Vector3(-bayHalfWidth, springY * 0.48f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            root.AddSocket("wall-right", new Vector3(bayHalfWidth, springY * 0.48f, 0f),
                Quaternion.Euler(0f, -90f, 0f));
            root.AddSocket("ledge-top", new Vector3(0f,
                springY + outerRadius + courseHeight * 0.58f, 0f));
            root.AddSocket("bridge-out", new Vector3(0f, springY * 0.76f, -depth * 0.55f));
            root.AddSocket("rubble-base", new Vector3(0f, courseHeight * 0.16f, -depth * 0.56f));
            return root;
        }

        private static void BuildPlinth(Transform parent, string name, int side, float pierCenterX,
            float pierWidth, float baseHeight, float depth, int seed, WorldArtPalette palette)
        {
            WorldArtStoneKit.Ashlar(parent, name + " plinth " + side,
                new Vector3(side * pierCenterX, baseHeight * 0.05f, 0.025f),
                new Vector3(pierWidth * 1.12f, baseHeight, depth * 1.10f),
                Mathf.Min(0.055f, baseHeight * 0.10f), seed, palette);
        }

        private static void BuildPier(Transform parent, string name, int side, float pierCenterX,
            float pierWidth, float courseHeight, int courses, float startY, float depth,
            float joint, int seed, WorldArtPalette palette)
        {
            for (int row = 0; row < courses; row++)
            {
                float y = startY + row * courseHeight;
                float seam = ((row & 1) == 0 ? -0.18f : 0.18f) * pierWidth;
                seam += (Hash(seed + row * 31) - 0.5f) * pierWidth * 0.045f;
                float leftEdge = -pierWidth * 0.5f;
                float rightEdge = pierWidth * 0.5f;
                float leftW = seam - leftEdge;
                float rightW = rightEdge - seam;
                float z = (Hash(seed + row * 47) - 0.5f) * 0.024f;

                WorldArtStoneKit.Ashlar(parent, name + " pier " + side + " course " + row + " A",
                    new Vector3(side * pierCenterX + leftEdge + leftW * 0.5f, y, z),
                    new Vector3(leftW - joint, courseHeight - joint, depth * 0.985f),
                    Mathf.Min(0.045f, courseHeight * 0.085f), seed + row * 101 + 1, palette);
                WorldArtStoneKit.Ashlar(parent, name + " pier " + side + " course " + row + " B",
                    new Vector3(side * pierCenterX + seam + rightW * 0.5f, y, z - 0.008f),
                    new Vector3(rightW - joint, courseHeight - joint, depth * 1.005f),
                    Mathf.Min(0.045f, courseHeight * 0.085f), seed + row * 101 + 2, palette);
            }
        }

        private static void BuildSpandrels(Transform parent, string name, float halfOpening,
            float pierCenterX, float pierWidth, float springY, float outerRadius,
            float ringThickness, float courseHeight, float z, float depth, float joint,
            int seed, WorldArtPalette palette, WorldArtArchDamage damage)
        {
            float bayHalf = pierCenterX + pierWidth * 0.58f;
            float topY = springY + outerRadius + courseHeight * 0.34f;
            int rows = Mathf.Max(2, Mathf.CeilToInt((topY - springY) / courseHeight));

            for (int row = 0; row < rows; row++)
            {
                float y = springY + courseHeight * (row + 0.42f);
                float dy = y - springY;
                float archX = dy < outerRadius
                    ? Mathf.Sqrt(Mathf.Max(0f, outerRadius * outerRadius - dy * dy))
                    : 0f;
                float inside = Mathf.Min(bayHalf - 0.18f, archX + ringThickness * 0.48f);
                if (inside >= bayHalf - 0.12f) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    if (OmitSpandrel(side, row, rows, damage)) continue;
                    float width = bayHalf - inside;
                    float x = side * (inside + width * 0.5f);
                    WorldArtStoneKit.Ashlar(parent,
                        name + " spandrel " + side + " row " + row,
                        new Vector3(x, y, z + (Hash(seed + side * 17 + row * 23) - 0.5f) * 0.018f),
                        new Vector3(Mathf.Max(0.18f, width - joint), courseHeight - joint,
                            depth * (0.98f + Hash(seed + row * 37) * 0.025f)),
                        Mathf.Min(0.040f, courseHeight * 0.08f),
                        seed + side * 71 + row * 113, palette);
                }
            }
        }

        private static void BuildSideReturns(Transform parent, string name, float pierCenterX,
            float pierWidth, float springY, float courseHeight, float depth, int seed,
            WorldArtPalette palette, WorldArtArchDamage damage)
        {
            int rows = Mathf.Max(3, Mathf.RoundToInt(springY / (courseHeight * 1.45f)));
            float sideX = pierCenterX + pierWidth * 0.48f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < rows; row++)
                {
                    if ((damage == WorldArtArchDamage.BrokenLeftHaunch && side < 0 && row >= rows - 1) ||
                        (damage == WorldArtArchDamage.BrokenRightHaunch && side > 0 && row >= rows - 1))
                        continue;
                    WorldArtStoneKit.Ashlar(parent, name + " return " + side + " " + row,
                        new Vector3(side * sideX,
                            courseHeight * 0.70f + row * courseHeight * 1.45f,
                            depth * 0.40f),
                        new Vector3(pierWidth * 0.68f, courseHeight * 1.35f, depth * 0.62f),
                        Mathf.Min(0.045f, courseHeight * 0.08f), seed + side * 101 + row * 61, palette);
                }
            }
        }

        private static void BuildCap(Transform parent, string name, float pierCenterX,
            float pierWidth, float crownY, float courseHeight, float depth, float joint,
            int seed, WorldArtPalette palette, WorldArtArchDamage damage)
        {
            float halfWidth = pierCenterX + pierWidth * 0.58f;
            const int blocks = 5;
            float blockW = halfWidth * 2f / blocks;
            for (int i = 0; i < blocks; i++)
            {
                bool missing = damage == WorldArtArchDamage.BrokenCrown && (i == 2 || i == 3);
                if (missing) continue;
                float x = -halfWidth + blockW * (i + 0.5f);
                float y = crownY + courseHeight * 0.38f +
                    (Hash(seed + i * 41) - 0.5f) * courseHeight * 0.06f;
                WorldArtStoneKit.Ashlar(parent, name + " top cap " + i,
                    new Vector3(x, y, depth * 0.12f),
                    new Vector3(blockW - joint, courseHeight * 0.68f, depth * 0.88f),
                    Mathf.Min(0.045f, courseHeight * 0.08f), seed + i * 79, palette);
            }
        }

        private static void BuildFallStone(Transform parent, string name, float halfOpening,
            float courseHeight, float depth, int seed, WorldArtPalette palette,
            WorldArtArchDamage damage)
        {
            float side = damage == WorldArtArchDamage.BrokenRightHaunch ? 1f : -1f;
            if (damage == WorldArtArchDamage.BrokenCrown) side = 0.30f;
            WorldArtPiece fallen = WorldArtStoneKit.Ashlar(parent, name + " fallen structural stone",
                new Vector3(side * (halfOpening + courseHeight * 0.55f),
                    courseHeight * 0.18f, -depth * 0.62f),
                new Vector3(courseHeight * 1.05f, courseHeight * 0.72f, depth * 0.76f),
                Mathf.Min(0.05f, courseHeight * 0.10f), seed, palette);
            fallen.Transform.localRotation = Quaternion.Euler(-5f, side * 11f, side * 17f);
        }

        private static bool OmitVoussoir(int i, int count, WorldArtArchDamage damage)
        {
            if (damage == WorldArtArchDamage.BrokenLeftHaunch)
                return i == 2 || i == 3;
            if (damage == WorldArtArchDamage.BrokenRightHaunch)
                return i == count - 3 || i == count - 4;
            if (damage == WorldArtArchDamage.BrokenCrown)
                return i == count / 2 - 1 || i == count / 2 + 1;
            return false;
        }

        private static bool OmitBacking(int i, int count, WorldArtArchDamage damage)
        {
            if (damage == WorldArtArchDamage.BrokenLeftHaunch)
                return i <= 4 && i >= 2;
            if (damage == WorldArtArchDamage.BrokenRightHaunch)
                return i >= count - 5 && i <= count - 3;
            if (damage == WorldArtArchDamage.BrokenCrown)
                return i >= count / 2 - 2 && i <= count / 2 + 2;
            return false;
        }

        private static bool OmitSpandrel(int side, int row, int rows, WorldArtArchDamage damage)
        {
            if (damage == WorldArtArchDamage.BrokenLeftHaunch && side < 0 && row >= rows - 2)
                return true;
            if (damage == WorldArtArchDamage.BrokenRightHaunch && side > 0 && row >= rows - 2)
                return true;
            if (damage == WorldArtArchDamage.BrokenCrown && row == rows - 1)
                return true;
            return false;
        }

        private static float Hash(int n)
        {
            unchecked
            {
                uint x = (uint)n;
                x ^= x >> 16;
                x *= 0x7feb352d;
                x ^= x >> 15;
                x *= 0x846ca68b;
                x ^= x >> 16;
                return (x & 0x00ffffff) / 16777215f;
            }
        }
    }
}
