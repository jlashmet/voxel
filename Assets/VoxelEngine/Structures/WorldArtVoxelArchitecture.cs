using Unity.Mathematics;

namespace VoxelEngine.Structures
{
    public enum WorldArtVoxelArchDamage
    {
        Intact,
        BrokenLeftHaunch,
        BrokenRightHaunch,
        BrokenCrown
    }

    /// <summary>
    /// Integer voxel dimensions for a reusable masonry arch bay. One unit is one world voxel;
    /// in the current engine that is 10 cm. All geometry is written through VoxelBrush so the
    /// resulting architecture is ordinary destructible world data, not presentation geometry.
    /// </summary>
    public struct WorldArtVoxelArchSpec
    {
        public int3 BaseCentre;
        public int HalfOpening;
        public int PierHeight;
        public int PierWidth;
        public int CourseHeight;
        public int RingThickness;
        public int Depth;
        public int ImpostHeight;
        public int JointDepth;
        public byte StoneMaterial;
        public byte EmptyMaterial;
        public uint Seed;
        public WorldArtVoxelArchDamage Damage;

        public static WorldArtVoxelArchSpec Hero(int3 baseCentre, byte stone, byte empty, uint seed)
        {
            return new WorldArtVoxelArchSpec
            {
                BaseCentre = baseCentre,
                HalfOpening = 16,        // 3.2 m clear span.
                PierHeight = 36,         // 3.6 m to spring line.
                PierWidth = 11,          // 1.1 m load-bearing pier.
                CourseHeight = 5,        // 50 cm ashlar courses.
                RingThickness = 6,       // 60 cm structural archivolt.
                Depth = 10,              // 1 m wall depth.
                ImpostHeight = 3,        // 30 cm projecting spring course.
                JointDepth = 1,          // 10 cm shallow front-face joints only.
                StoneMaterial = stone,
                EmptyMaterial = empty,
                Seed = seed,
                Damage = WorldArtVoxelArchDamage.Intact
            };
        }
    }

    public struct WorldArtVoxelArchSockets
    {
        public int3 Opening;
        public int3 Crown;
        public int3 LeftBase;
        public int3 RightBase;
        public int3 WallLeft;
        public int3 WallRight;
        public int3 LedgeTop;
        public int3 RubbleBase;
    }

    /// <summary>
    /// Voxel-only architectural construction. The arch is deliberately built as continuous
    /// load-bearing mass, then shallow joints are carved into its front face. At 10 cm voxels this
    /// produces legible cut-stone rhythm without the huge structural gaps caused by disconnected
    /// voxel wedges.
    /// </summary>
    public static class WorldArtVoxelArchitecture
    {
        public static WorldArtVoxelArchSockets ArchBay(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec)
        {
            int halfOpening = math.max(4, spec.HalfOpening);
            int pierHeight = math.max(8, spec.PierHeight);
            int pierWidth = math.max(4, spec.PierWidth);
            int courseHeight = math.max(3, spec.CourseHeight);
            int ringThickness = math.max(3, spec.RingThickness);
            int depth = math.max(4, spec.Depth);
            int impostHeight = math.max(2, spec.ImpostHeight);
            int outerRadius = halfOpening + ringThickness;
            int z0 = spec.BaseCentre.z - depth / 2;
            int frontZ = z0;
            int springY = spec.BaseCentre.y + pierHeight;
            int pierCentreOffset = halfOpening + (pierWidth + 1) / 2;

            BuildPlinth(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth,
                courseHeight, depth, spec.StoneMaterial, spec.Seed + 11u);
            BuildPlinth(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth,
                courseHeight, depth, spec.StoneMaterial, spec.Seed + 17u);

            BuildPier(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth,
                pierHeight, courseHeight, impostHeight, depth, spec.StoneMaterial, spec.Seed + 101u);
            BuildPier(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth,
                pierHeight, courseHeight, impostHeight, depth, spec.StoneMaterial, spec.Seed + 211u);

            BuildImpost(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth,
                springY, impostHeight, depth, spec.StoneMaterial);
            BuildImpost(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth,
                springY, impostHeight, depth, spec.StoneMaterial);

            BuildStructuralRing(ref brush, spec.BaseCentre.x, springY, spec.BaseCentre.z,
                halfOpening, outerRadius, depth, spec.StoneMaterial, spec.Damage);

            CarveArchivoltJoints(ref brush, spec.BaseCentre.x, springY, frontZ,
                halfOpening, outerRadius, math.max(1, spec.JointDepth), spec.EmptyMaterial,
                spec.Seed, spec.Damage);

            BuildKeystone(ref brush, spec.BaseCentre.x, springY, spec.BaseCentre.z,
                halfOpening, outerRadius, depth, spec.StoneMaterial, spec.Damage);

            BuildBackingMass(ref brush, spec.BaseCentre, pierCentreOffset, pierWidth,
                springY, outerRadius, courseHeight, depth, spec.StoneMaterial, spec.Damage);

            if (spec.Damage != WorldArtVoxelArchDamage.Intact)
                BuildRubble(ref brush, spec.BaseCentre, pierCentreOffset, courseHeight,
                    depth, spec.StoneMaterial, spec.Seed + 1709u, spec.Damage);

            int bayHalf = pierCentreOffset + (pierWidth + 1) / 2;
            return new WorldArtVoxelArchSockets
            {
                Opening = new int3(spec.BaseCentre.x, springY + halfOpening / 3, frontZ - 1),
                Crown = new int3(spec.BaseCentre.x, springY + outerRadius, frontZ - 1),
                LeftBase = new int3(spec.BaseCentre.x - pierCentreOffset, spec.BaseCentre.y, spec.BaseCentre.z),
                RightBase = new int3(spec.BaseCentre.x + pierCentreOffset, spec.BaseCentre.y, spec.BaseCentre.z),
                WallLeft = new int3(spec.BaseCentre.x - bayHalf, springY / 2, spec.BaseCentre.z),
                WallRight = new int3(spec.BaseCentre.x + bayHalf, springY / 2, spec.BaseCentre.z),
                LedgeTop = new int3(spec.BaseCentre.x, springY + outerRadius + courseHeight, spec.BaseCentre.z),
                RubbleBase = new int3(spec.BaseCentre.x, spec.BaseCentre.y + 1, frontZ - 2)
            };
        }

        private static void BuildPlinth(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int courseHeight, int depth, byte material, uint seed)
        {
            int width = pierWidth + 2;
            int height = math.max(3, courseHeight - 1);
            int3 min = new int3(
                origin.x + side * pierOffset - width / 2,
                origin.y,
                origin.z - depth / 2 - 1);
            WorldArtPrimitives.RoundedBox(ref brush, min,
                new int3(width, height, depth + 2), 1, material);
        }

        private static void BuildPier(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int pierHeight, int courseHeight,
            int impostHeight, int depth, byte material, uint seed)
        {
            int shaftBottom = math.max(3, courseHeight - 1);
            int shaftTop = pierHeight - impostHeight;
            int available = math.max(courseHeight * 2, shaftTop - shaftBottom);
            int rows = math.max(2, available / courseHeight);
            int actualCourse = math.max(3, available / rows);

            for (int row = 0; row < rows; row++)
            {
                int y = origin.y + shaftBottom + row * actualCourse;
                int seamShift = ((row & 1) == 0 ? -1 : 1);
                if ((Hash(seed + (uint)(row * 37)) & 7u) == 0u) seamShift = 0;

                int leftW = math.max(3, pierWidth / 2 + seamShift);
                int rightW = math.max(3, pierWidth - leftW - 1);
                int leftX = origin.x + side * pierOffset - pierWidth / 2;
                int faceStep = ((Hash(seed + (uint)(row * 53)) >> 5) & 3u) == 0u ? 1 : 0;
                int z = origin.z - depth / 2 - faceStep;

                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(leftX, y, z),
                    new int3(leftW, actualCourse - 1, depth + faceStep), 1, material);
                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(leftX + leftW + 1, y, origin.z - depth / 2),
                    new int3(rightW, actualCourse - 1, depth), 1, material);
            }
        }

        private static void BuildImpost(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int springY, int height, int depth, byte material)
        {
            int width = pierWidth + 3;
            int3 min = new int3(origin.x + side * pierOffset - width / 2,
                springY - height, origin.z - depth / 2 - 1);
            WorldArtPrimitives.RoundedBox(ref brush, min,
                new int3(width, height, depth + 2), 1, material);
        }

        private static void BuildStructuralRing(ref VoxelBrush brush, int cx, int springY, int cz,
            int innerRadius, int outerRadius, int depth, byte material, WorldArtVoxelArchDamage damage)
        {
            int z0 = cz - depth / 2;
            int inner2 = (innerRadius - 1) * (innerRadius - 1);
            int outer2 = outerRadius * outerRadius;

            for (int z = z0; z < z0 + depth; z++)
            for (int y = 0; y <= outerRadius; y++)
            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                int d2 = x * x + y * y;
                if (d2 < inner2 || d2 > outer2) continue;
                if (OmitRingVoxel(x, y, outerRadius, damage)) continue;
                brush.Set(cx + x, springY + y, z, material);
            }
        }

        private static void CarveArchivoltJoints(ref VoxelBrush brush, int cx, int springY, int frontZ,
            int innerRadius, int outerRadius, int depth, byte empty, uint seed,
            WorldArtVoxelArchDamage damage)
        {
            const int stoneCount = 17;
            for (int boundary = 1; boundary < stoneCount; boundary++)
            {
                float angle = math.PI * boundary / stoneCount;
                float ca = math.cos(angle);
                float sa = math.sin(angle);
                int inner = innerRadius + 1;
                int outer = outerRadius - 1;
                for (int r = inner; r <= outer; r++)
                {
                    int x = (int)math.round(ca * r);
                    int y = (int)math.round(sa * r);
                    if (OmitRingVoxel(x, y, outerRadius, damage)) continue;
                    for (int d = 0; d < depth; d++)
                        brush.Set(cx + x, springY + y, frontZ + d, empty);
                }
            }
        }

        private static void BuildKeystone(ref VoxelBrush brush, int cx, int springY, int cz,
            int innerRadius, int outerRadius, int depth, byte material, WorldArtVoxelArchDamage damage)
        {
            if (damage == WorldArtVoxelArchDamage.BrokenCrown) return;

            int width = 5;
            int radial = outerRadius - innerRadius + 2;
            int3 min = new int3(cx - width / 2,
                springY + innerRadius - 1,
                cz - depth / 2 - 1);
            WorldArtPrimitives.RoundedBox(ref brush, min,
                new int3(width, radial + 2, depth + 2), 1, material);
        }

        private static void BuildBackingMass(ref VoxelBrush brush, int3 origin,
            int pierOffset, int pierWidth, int springY, int outerRadius, int courseHeight,
            int depth, byte material, WorldArtVoxelArchDamage damage)
        {
            int bayHalf = pierOffset + (pierWidth + 1) / 2;
            int rearZ = origin.z + depth / 2 - math.max(3, depth / 3);
            int backingDepth = math.max(3, depth / 3);
            int top = springY + outerRadius + courseHeight;

            // Side shoulders continue the pier mass upward behind the visible archivolt.
            int shoulderWidth = math.max(4, bayHalf - outerRadius + 2);
            if (damage != WorldArtVoxelArchDamage.BrokenLeftHaunch)
                brush.FillBulk(new int3(origin.x - bayHalf, springY, rearZ),
                    new int3(shoulderWidth, top - springY, backingDepth), material);
            if (damage != WorldArtVoxelArchDamage.BrokenRightHaunch)
                brush.FillBulk(new int3(origin.x + bayHalf - shoulderWidth, springY, rearZ),
                    new int3(shoulderWidth, top - springY, backingDepth), material);

            // A restrained cap gives the ruin a finished architectural termination.
            if (damage != WorldArtVoxelArchDamage.BrokenCrown)
                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(origin.x - bayHalf, top - courseHeight + 1, rearZ - 1),
                    new int3(bayHalf * 2, courseHeight - 1, backingDepth + 2), 1, material);
        }

        private static void BuildRubble(ref VoxelBrush brush, int3 origin, int pierOffset,
            int courseHeight, int depth, byte material, uint seed, WorldArtVoxelArchDamage damage)
        {
            int side = damage == WorldArtVoxelArchDamage.BrokenRightHaunch ? 1 : -1;
            for (int i = 0; i < 5; i++)
            {
                uint h = Hash(seed + (uint)(i * 97));
                int x = origin.x + side * (pierOffset + 2 + i * 2) + (int)(h & 1u);
                int z = origin.z - depth / 2 - 2 - (int)((h >> 3) & 3u);
                int sx = 3 + (int)((h >> 6) & 1u);
                int sy = 2 + (int)((h >> 8) & 1u);
                int sz = 3 + (int)((h >> 10) & 1u);
                WorldArtPrimitives.RoundedBox(ref brush,
                    new int3(x - sx / 2, origin.y, z), new int3(sx, sy, sz), 1, material);
            }
        }

        private static bool OmitRingVoxel(int x, int y, int outerRadius, WorldArtVoxelArchDamage damage)
        {
            if (damage == WorldArtVoxelArchDamage.Intact) return false;
            if (damage == WorldArtVoxelArchDamage.BrokenCrown)
                return y > outerRadius - 5 && x > -2 && x < 7;
            if (damage == WorldArtVoxelArchDamage.BrokenLeftHaunch)
                return x < -outerRadius / 2 && y > outerRadius / 3 && y < outerRadius * 4 / 5;
            if (damage == WorldArtVoxelArchDamage.BrokenRightHaunch)
                return x > outerRadius / 2 && y > outerRadius / 3 && y < outerRadius * 4 / 5;
            return false;
        }

        private static uint Hash(uint x)
        {
            x ^= x >> 16;
            x *= 0x7feb352du;
            x ^= x >> 15;
            x *= 0x846ca68bu;
            x ^= x >> 16;
            return x;
        }
    }
}
