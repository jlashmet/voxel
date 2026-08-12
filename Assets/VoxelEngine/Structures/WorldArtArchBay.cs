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
    /// Reusable load-bearing masonry bay. The opening, piers, imposts and archivolt are the primary
    /// architecture; wall infill is intentionally recessed so it can never visually fight the arch.
    /// Named sockets let procgen attach walls, bridges, ivy, rubble and upper traversal pieces.
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

            float joint = Mathf.Clamp(courseHeight * 0.055f, 0.020f, 0.040f);
            float springY = pierHeight;
            float innerRadius = halfOpening;
            float outerRadius = halfOpening + ringThickness;
            float pierCenterX = halfOpening + pierWidth * 0.5f;
            float baseHeight = courseHeight * 0.80f;
            float impostHeight = courseHeight * 0.52f;
            float impostWidth = pierWidth * 1.24f;
            float frontZ = -depth * 0.012f;

            BuildPlinth(root.Transform, name, -1, pierCenterX, pierWidth, baseHeight, depth,
                seed + 11, palette);
            BuildPlinth(root.Transform, name, 1, pierCenterX, pierWidth, baseHeight, depth,
                seed + 17, palette);

            BuildPier(root.Transform, name, -1, pierCenterX, pierWidth, baseHeight,
                springY, impostHeight, courseHeight, depth, joint, seed + 101, palette);
            BuildPier(root.Transform, name, 1, pierCenterX, pierWidth, baseHeight,
                springY, impostHeight, courseHeight, depth, joint, seed + 211, palette);

            // The impost top is exactly the spring line. This makes the load path visually obvious:
            // vertical pier -> projecting impost -> radial arch.
            float impostY = springY - impostHeight * 0.5f;
            WorldArtStoneKit.Ashlar(root.Transform, name + " left impost",
                new Vector3(-pierCenterX, impostY, frontZ - depth * 0.010f),
                new Vector3(impostWidth, impostHeight, depth * 1.035f),
                Mathf.Min(0.038f, impostHeight * 0.10f), seed + 307, palette);
            WorldArtStoneKit.Ashlar(root.Transform, name + " right impost",
                new Vector3(pierCenterX, impostY, frontZ - depth * 0.010f),
                new Vector3(impostWidth, impostHeight, depth * 1.035f),
                Mathf.Min(0.038f, impostHeight * 0.10f), seed + 311, palette);

            BuildArchivolt(root.Transform, name, springY, innerRadius, outerRadius,
                ringThickness, depth, frontZ, courseHeight, seed + 401, palette, damage);

            // Wall mass is a separate, recessed construction layer. There is intentionally no
            // decorative second wedge-ring: one authoritative archivolt keeps the silhouette calm.
            BuildSpandrels(root.Transform, name, pierCenterX, pierWidth, springY,
                outerRadius, courseHeight, depth, joint, seed + 1103, palette, damage);
            BuildSideReturns(root.Transform, name, pierCenterX, pierWidth, springY,
                courseHeight, depth, seed + 1301, palette, damage);
            BuildCap(root.Transform, name, pierCenterX, pierWidth,
                springY + outerRadius + courseHeight * 0.38f,
                courseHeight, depth, joint, seed + 1501, palette, damage);

            if (damage != WorldArtArchDamage.Intact)
                BuildFallenStone(root.Transform, name, halfOpening, courseHeight, depth,
                    seed + 1709, palette, damage);

            float bayHalfWidth = pierCenterX + pierWidth * 0.58f;
            root.AddSocket("opening", new Vector3(0f, springY + innerRadius * 0.34f, -depth * 0.5f));
            root.AddSocket("crown", new Vector3(0f, springY + outerRadius, -depth * 0.5f));
            root.AddSocket("keystone", new Vector3(0f, springY + outerRadius, -depth * 0.54f));
            root.AddSocket("left-base", new Vector3(-pierCenterX, 0f, 0f));
            root.AddSocket("right-base", new Vector3(pierCenterX, 0f, 0f));
            root.AddSocket("wall-left", new Vector3(-bayHalfWidth, springY * 0.48f, 0f),
                Quaternion.Euler(0f, 90f, 0f));
            root.AddSocket("wall-right", new Vector3(bayHalfWidth, springY * 0.48f, 0f),
                Quaternion.Euler(0f, -90f, 0f));
            root.AddSocket("ledge-top", new Vector3(0f,
                springY + outerRadius + courseHeight * 0.92f, 0f));
            root.AddSocket("bridge-out", new Vector3(0f, springY * 0.72f, -depth * 0.54f));
            root.AddSocket("rubble-base", new Vector3(0f, courseHeight * 0.15f, -depth * 0.55f));
            return root;
        }

        private static void BuildArchivolt(Transform parent, string name, float springY,
            float innerRadius, float outerRadius, float ringThickness, float depth, float frontZ,
            float courseHeight, int seed, WorldArtPalette palette, WorldArtArchDamage damage)
        {
            const int count = 23;
            int keyIndex = count / 2;
            float cell = 180f / count;

            for (int i = 0; i < count; i++)
            {
                if (i == keyIndex) continue;
                if (OmitVoussoir(i, count, damage)) continue;

                float aHigh = 180f - i * cell;
                float aLow = 180f - (i + 1) * cell;
                float gap = 0.48f + Hash(seed + i * 31) * 0.14f;
                float lo = aLow + gap * 0.5f;
                float hi = aHigh - gap * 0.5f;

                // Most stones are deliberately calm. Tiny face-depth changes break machine-perfect
                // specular continuity without turning the ring into procedural noise.
                float faceSet = (Hash(seed + 300 + i * 37) - 0.5f) * depth * 0.010f;
                float stoneDepth = depth * (0.992f + Hash(seed + 500 + i * 41) * 0.016f);
                WorldArtArchitecturalStone.Voussoir(parent,
                    name + " structural archivolt " + i,
                    new Vector3(0f, springY, frontZ + faceSet),
                    innerRadius, outerRadius, lo, hi, stoneDepth,
                    Mathf.Min(0.034f, ringThickness * 0.060f),
                    seed + 701 + i * 71, palette);
            }

            if (damage == WorldArtArchDamage.BrokenCrown) return;

            float keyHigh = 180f - keyIndex * cell;
            float keyLow = 180f - (keyIndex + 1) * cell;
            float keyGrow = cell * 0.10f;
            WorldArtPiece key = WorldArtArchitecturalStone.Voussoir(parent,
                name + " proud keystone",
                new Vector3(0f, springY + courseHeight * 0.012f, frontZ - depth * 0.024f),
                innerRadius - ringThickness * 0.012f,
                outerRadius + ringThickness * 0.12f,
                keyLow - keyGrow, keyHigh + keyGrow,
                depth * 1.045f, Mathf.Min(0.036f, ringThickness * 0.062f),
                seed + 947, palette);
            key.AddSocket("moss", new Vector3(0f, outerRadius + ringThickness * 0.10f,
                -depth * 0.51f));
        }

        private static void BuildPlinth(Transform parent, string name, int side,
            float pierCenterX, float pierWidth, float baseHeight, float depth,
            int seed, WorldArtPalette palette)
        {
            WorldArtStoneKit.Ashlar(parent, name + " plinth " + side,
                new Vector3(side * pierCenterX, baseHeight * 0.50f, 0.018f),
                new Vector3(pierWidth * 1.14f, baseHeight * 0.92f, depth * 1.08f),
                Mathf.Min(0.050f, baseHeight * 0.10f), seed, palette);
        }

        private static void BuildPier(Transform parent, string name, int side,
            float pierCenterX, float pierWidth, float baseHeight, float springY,
            float impostHeight, float courseHeight, float depth, float joint,
            int seed, WorldArtPalette palette)
        {
            float shaftBottom = baseHeight * 0.94f;
            float shaftTop = springY - impostHeight - joint * 0.75f;
            float available = Mathf.Max(courseHeight * 3f, shaftTop - shaftBottom);
            int courses = Mathf.Max(3, Mathf.RoundToInt(available / courseHeight));
            float actualCourse = available / courses;

            for (int row = 0; row < courses; row++)
            {
                float y = shaftBottom + actualCourse * (row + 0.5f);
                float seam = ((row & 1) == 0 ? -0.14f : 0.14f) * pierWidth;
                seam += (Hash(seed + row * 31) - 0.5f) * pierWidth * 0.018f;
                float leftEdge = -pierWidth * 0.5f;
                float rightEdge = pierWidth * 0.5f;
                float leftW = seam - leftEdge;
                float rightW = rightEdge - seam;
                float z = (Hash(seed + row * 47) - 0.5f) * depth * 0.010f;

                WorldArtStoneKit.Ashlar(parent,
                    name + " pier " + side + " course " + row + " A",
                    new Vector3(side * pierCenterX + leftEdge + leftW * 0.5f, y, z),
                    new Vector3(Mathf.Max(0.18f, leftW - joint), actualCourse - joint,
                        depth * 0.995f),
                    Mathf.Min(0.038f, actualCourse * 0.080f),
                    seed + row * 101 + 1, palette);
                WorldArtStoneKit.Ashlar(parent,
                    name + " pier " + side + " course " + row + " B",
                    new Vector3(side * pierCenterX + seam + rightW * 0.5f, y, z - 0.004f),
                    new Vector3(Mathf.Max(0.18f, rightW - joint), actualCourse - joint,
                        depth * 1.005f),
                    Mathf.Min(0.038f, actualCourse * 0.080f),
                    seed + row * 101 + 2, palette);
            }
        }

        private static void BuildSpandrels(Transform parent, string name,
            float pierCenterX, float pierWidth, float springY, float outerRadius,
            float courseHeight, float depth, float joint, int seed,
            WorldArtPalette palette, WorldArtArchDamage damage)
        {
            float bayHalf = pierCenterX + pierWidth * 0.58f;
            float topY = springY + outerRadius + courseHeight * 0.28f;
            int rows = Mathf.Max(2, Mathf.CeilToInt((topY - springY) / courseHeight));
            float recessedZ = depth * 0.17f;
            float recessedDepth = depth * 0.68f;

            for (int row = 0; row < rows; row++)
            {
                float y = springY + courseHeight * (row + 0.5f);
                float lowY = Mathf.Max(0f, y - courseHeight * 0.52f - springY);
                float archX = lowY < outerRadius
                    ? Mathf.Sqrt(Mathf.Max(0f, outerRadius * outerRadius - lowY * lowY))
                    : 0f;
                float inside = archX + joint * 1.8f;
                if (inside >= bayHalf - 0.20f) continue;

                for (int side = -1; side <= 1; side += 2)
                {
                    if (OmitSpandrel(side, row, rows, damage)) continue;
                    float run = bayHalf - inside;
                    BuildSpandrelRun(parent, name, side, row, inside, run, y,
                        courseHeight, pierWidth, recessedZ, recessedDepth,
                        joint, seed + row * 157 + side * 19, palette);
                }
            }
        }

        private static void BuildSpandrelRun(Transform parent, string name, int side, int row,
            float inside, float run, float y, float courseHeight, float pierWidth,
            float z, float depth, float joint, int seed, WorldArtPalette palette)
        {
            float preferred = pierWidth * 0.74f;
            int blocks = Mathf.Clamp(Mathf.CeilToInt(run / preferred), 1, 4);
            float blockW = run / blocks;
            for (int i = 0; i < blocks; i++)
            {
                float fromInner = (i + 0.5f) * blockW;
                float x = side * (inside + fromInner);
                WorldArtStoneKit.Ashlar(parent,
                    name + " recessed spandrel " + side + " row " + row + " stone " + i,
                    new Vector3(x, y, z + (Hash(seed + i * 17) - 0.5f) * 0.008f),
                    new Vector3(Mathf.Max(0.18f, blockW - joint), courseHeight - joint,
                        depth),
                    Mathf.Min(0.034f, courseHeight * 0.072f),
                    seed + i * 89, palette);
            }
        }

        private static void BuildSideReturns(Transform parent, string name,
            float pierCenterX, float pierWidth, float springY, float courseHeight,
            float depth, int seed, WorldArtPalette palette, WorldArtArchDamage damage)
        {
            int rows = Mathf.Max(3, Mathf.RoundToInt(springY / (courseHeight * 1.55f)));
            float sideX = pierCenterX + pierWidth * 0.54f;
            for (int side = -1; side <= 1; side += 2)
            {
                for (int row = 0; row < rows; row++)
                {
                    if ((damage == WorldArtArchDamage.BrokenLeftHaunch && side < 0 && row == rows - 1) ||
                        (damage == WorldArtArchDamage.BrokenRightHaunch && side > 0 && row == rows - 1))
                        continue;

                    WorldArtStoneKit.Ashlar(parent, name + " wall return " + side + " " + row,
                        new Vector3(side * sideX,
                            courseHeight * 0.78f + row * courseHeight * 1.55f,
                            depth * 0.38f),
                        new Vector3(pierWidth * 0.62f, courseHeight * 1.44f, depth * 0.52f),
                        Mathf.Min(0.036f, courseHeight * 0.075f),
                        seed + side * 101 + row * 61, palette);
                }
            }
        }

        private static void BuildCap(Transform parent, string name, float pierCenterX,
            float pierWidth, float y, float courseHeight, float depth, float joint,
            int seed, WorldArtPalette palette, WorldArtArchDamage damage)
        {
            float halfWidth = pierCenterX + pierWidth * 0.58f;
            const int blocks = 6;
            float blockW = halfWidth * 2f / blocks;
            for (int i = 0; i < blocks; i++)
            {
                if (damage == WorldArtArchDamage.BrokenCrown && (i == 3 || i == 4)) continue;
                float x = -halfWidth + blockW * (i + 0.5f);
                WorldArtStoneKit.Ashlar(parent, name + " crown course " + i,
                    new Vector3(x, y, depth * 0.11f),
                    new Vector3(blockW - joint, courseHeight * 0.72f, depth * 0.72f),
                    Mathf.Min(0.036f, courseHeight * 0.075f), seed + i * 97, palette);
            }
        }

        private static void BuildFallenStone(Transform parent, string name, float halfOpening,
            float courseHeight, float depth, int seed, WorldArtPalette palette,
            WorldArtArchDamage damage)
        {
            float side = damage == WorldArtArchDamage.BrokenRightHaunch ? 1f : -1f;
            WorldArtPiece fallen = WorldArtStoneKit.Ashlar(parent, name + " fallen structural stone",
                new Vector3(side * (halfOpening + courseHeight * 0.30f),
                    courseHeight * 0.20f, -depth * 0.62f),
                new Vector3(courseHeight * 0.95f, courseHeight * 0.62f, depth * 0.82f),
                Mathf.Min(0.040f, courseHeight * 0.08f), seed, palette);
            fallen.Transform.localRotation = Quaternion.Euler(-4f, side * 13f, side * 18f);
        }

        private static bool OmitVoussoir(int i, int count, WorldArtArchDamage damage)
        {
            if (damage == WorldArtArchDamage.BrokenLeftHaunch)
                return i == 2 || i == 3;
            if (damage == WorldArtArchDamage.BrokenRightHaunch)
                return i == count - 3 || i == count - 4;
            if (damage == WorldArtArchDamage.BrokenCrown)
            {
                int middle = count / 2;
                return i >= middle - 1 && i <= middle + 1;
            }
            return false;
        }

        private static bool OmitSpandrel(int side, int row, int rows, WorldArtArchDamage damage)
        {
            if (damage == WorldArtArchDamage.BrokenLeftHaunch && side < 0 && row >= rows - 2)
                return true;
            if (damage == WorldArtArchDamage.BrokenRightHaunch && side > 0 && row >= rows - 2)
                return true;
            if (damage == WorldArtArchDamage.BrokenCrown && row == rows - 1)
                return side > 0;
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
