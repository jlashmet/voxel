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
        public int ArchivoltProjection;
        public int MasonryEdgeRadius;
        public byte StoneMaterial;
        public byte JointMaterial;
        public byte EmptyMaterial;
        public uint Seed;
        public WorldArtVoxelArchDamage Damage;

        public static WorldArtVoxelArchSpec Hero(int3 baseCentre, byte stone, byte empty, uint seed,
                                                  byte jointMaterial = 0)
        {
            return new WorldArtVoxelArchSpec
            {
                BaseCentre = baseCentre,
                HalfOpening = 16,
                PierHeight = 35,
                PierWidth = 11,
                CourseHeight = 5,
                RingThickness = 6,
                Depth = 10,
                ImpostHeight = 3,
                JointDepth = 1,
                ArchivoltProjection = 3,
                MasonryEdgeRadius = 0,
                StoneMaterial = stone,
                JointMaterial = jointMaterial == 0 ? stone : jointMaterial,
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
    /// Reusable voxel-only arch component assembled from architectural primitives. The visible
    /// archivolt is a bond of true trapezoidal voussoir masses over a continuous rear ring.
    /// </summary>
    public static class WorldArtVoxelArchitecture
    {
        private const int HeroVoussoirCount = 11;

        public static WorldArtVoxelArchSockets ArchBay(ref VoxelBrush brush, in WorldArtVoxelArchSpec spec)
        {
            int halfOpening = math.max(4, spec.HalfOpening);
            int pierHeight = math.max(8, spec.PierHeight);
            int pierWidth = math.max(4, spec.PierWidth);
            int courseHeight = math.max(3, spec.CourseHeight);
            int ringThickness = math.max(3, spec.RingThickness);
            int depth = math.max(4, spec.Depth);
            int impostHeight = math.max(2, spec.ImpostHeight);
            int jointDepth = math.max(1, spec.JointDepth);
            int archivoltProjection = math.clamp(spec.ArchivoltProjection > 0 ? spec.ArchivoltProjection : 2, 1, depth - 2);
            int masonryEdgeRadius = math.max(0, spec.MasonryEdgeRadius);
            byte jointMaterial = spec.JointMaterial == 0 ? spec.StoneMaterial : spec.JointMaterial;
            int outerRadius = halfOpening + ringThickness;
            int frontZ = spec.BaseCentre.z - depth / 2;
            int springY = spec.BaseCentre.y + pierHeight;
            int pierCentreOffset = halfOpening + (pierWidth + 1) / 2;

            BuildPlinth(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth, courseHeight, depth, masonryEdgeRadius, spec.StoneMaterial);
            BuildPlinth(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth, courseHeight, depth, masonryEdgeRadius, spec.StoneMaterial);
            BuildPier(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth, pierHeight, courseHeight, impostHeight, depth, jointDepth, masonryEdgeRadius, spec.StoneMaterial, jointMaterial, spec.Seed + 101u);
            BuildPier(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth, pierHeight, courseHeight, impostHeight, depth, jointDepth, masonryEdgeRadius, spec.StoneMaterial, jointMaterial, spec.Seed + 211u);
            BuildImpost(ref brush, spec.BaseCentre, -1, pierCentreOffset, pierWidth, halfOpening, springY, impostHeight, depth, masonryEdgeRadius, spec.StoneMaterial);
            BuildImpost(ref brush, spec.BaseCentre, 1, pierCentreOffset, pierWidth, halfOpening, springY, impostHeight, depth, masonryEdgeRadius, spec.StoneMaterial);

            BuildStructuralRing(ref brush, spec.BaseCentre.x, springY, spec.BaseCentre.z,
                halfOpening, outerRadius, depth, spec.StoneMaterial, spec.Damage);
            BuildFrontVoussoirs(ref brush, spec.BaseCentre.x, springY, frontZ,
                halfOpening, outerRadius, archivoltProjection + 1, spec.StoneMaterial,
                jointMaterial, spec.Damage);

            BuildBackingMass(ref brush, spec.BaseCentre, pierCentreOffset, pierWidth,
                springY, outerRadius, courseHeight, depth, jointDepth, archivoltProjection,
                spec.StoneMaterial, jointMaterial, spec.Seed + 1103u, spec.Damage);

            if (spec.Damage != WorldArtVoxelArchDamage.Intact)
                BuildRubble(ref brush, spec.BaseCentre, pierCentreOffset, courseHeight, depth,
                    spec.StoneMaterial, spec.Seed + 1709u, spec.Damage);

            int bayHalf = pierCentreOffset + (pierWidth + 1) / 2;
            return new WorldArtVoxelArchSockets
            {
                Opening = new int3(spec.BaseCentre.x, springY + halfOpening / 3, frontZ - 1),
                Crown = new int3(spec.BaseCentre.x, springY + outerRadius, frontZ - 2),
                LeftBase = new int3(spec.BaseCentre.x - pierCentreOffset, spec.BaseCentre.y, spec.BaseCentre.z),
                RightBase = new int3(spec.BaseCentre.x + pierCentreOffset, spec.BaseCentre.y, spec.BaseCentre.z),
                WallLeft = new int3(spec.BaseCentre.x - bayHalf, spec.BaseCentre.y + pierHeight / 2, spec.BaseCentre.z),
                WallRight = new int3(spec.BaseCentre.x + bayHalf, spec.BaseCentre.y + pierHeight / 2, spec.BaseCentre.z),
                LedgeTop = new int3(spec.BaseCentre.x, springY + outerRadius + 2, spec.BaseCentre.z),
                RubbleBase = new int3(spec.BaseCentre.x, spec.BaseCentre.y + 1, frontZ - 2)
            };
        }

        private static void BuildPlinth(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int courseHeight, int depth, int edgeRadius, byte material)
        {
            int width = pierWidth + 3;
            int height = math.max(3, courseHeight - 1);
            int3 min = new int3(origin.x + side * pierOffset - width / 2, origin.y, origin.z - depth / 2 - 1);
            WorldArtPrimitives.RoundedBox(ref brush, min, new int3(width, height, depth + 2), edgeRadius, material);
        }

        private static void BuildPier(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int pierHeight, int courseHeight,
            int impostHeight, int depth, int jointDepth, int edgeRadius,
            byte material, byte jointMaterial, uint seed)
        {
            int plinthHeight = math.max(3, courseHeight - 1);
            int shaftHeight = math.max(courseHeight * 2, pierHeight - plinthHeight - impostHeight);
            int leftX = origin.x + side * pierOffset - pierWidth / 2;
            int shaftY = origin.y + plinthHeight;
            int frontZ = origin.z - depth / 2;
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(leftX, shaftY, frontZ), new int3(pierWidth, shaftHeight, depth), edgeRadius, material);

            int rows = math.max(2, shaftHeight / courseHeight);
            for (int row = 1; row < rows; row++)
            {
                int jointY = shaftY + row * courseHeight;
                if (jointY >= shaftY + shaftHeight - 1) break;
                for (int x = leftX + 1; x < leftX + pierWidth - 1; x++)
                for (int d = 0; d < jointDepth; d++)
                    brush.Set(x, jointY, frontZ + d, jointMaterial);
            }

            for (int row = 0; row < rows; row++)
            {
                int rowY = shaftY + row * courseHeight;
                int rowTop = math.min(shaftY + shaftHeight, rowY + courseHeight);
                int shift = (row & 1) == 0 ? -1 : 1;
                if ((Hash(seed + (uint)(row * 37)) & 15u) == 0u) shift = 0;
                int seamX = math.clamp(leftX + pierWidth / 2 + shift, leftX + 3, leftX + pierWidth - 4);
                for (int y = rowY + 1; y < rowTop - 1; y++)
                for (int d = 0; d < jointDepth; d++)
                    brush.Set(seamX, y, frontZ + d, jointMaterial);
            }
        }

        private static void BuildImpost(ref VoxelBrush brush, int3 origin, int side,
            int pierOffset, int pierWidth, int halfOpening, int springY, int height, int depth,
            int edgeRadius, byte material)
        {
            int pierLeft = origin.x + side * pierOffset - pierWidth / 2;
            int pierRight = pierLeft + pierWidth;
            int innerFace = side < 0 ? pierRight : pierLeft;
            int outerFace = side < 0 ? pierLeft : pierRight;
            int minX = side < 0 ? outerFace - 2 : innerFace - 1;
            int maxX = side < 0 ? innerFace + 1 : outerFace + 2;
            int openingEdge = origin.x + side * halfOpening;
            if (side < 0) maxX = math.min(maxX, openingEdge + 1);
            else minX = math.max(minX, openingEdge - 1);
            WorldArtPrimitives.RoundedBox(ref brush,
                new int3(minX, springY - height, origin.z - depth / 2 - 1),
                new int3(maxX - minX, height, depth + 2), edgeRadius, material);
        }

        private static void BuildStructuralRing(ref VoxelBrush brush, int cx, int springY, int cz,
            int innerRadius, int outerRadius, int depth, byte material, WorldArtVoxelArchDamage damage)
        {
            int structuralFront = cz - depth / 2 + math.max(3, depth / 2);
            float inner2 = (innerRadius - 0.1f) * (innerRadius - 0.1f);
            float outer2 = (outerRadius + 0.05f) * (outerRadius + 0.05f);
            for (int z = structuralFront; z < cz + (depth + 1) / 2; z++)
            for (int y = 0; y <= outerRadius; y++)
            for (int x = -outerRadius; x <= outerRadius; x++)
            {
                float d2 = x * x + y * y;
                if (d2 < inner2 || d2 > outer2) continue;
                if (OmitRingVoxel(x, y, outerRadius, damage)) continue;
                brush.Set(cx + x, springY + y, z, material);
            }
        }

        /// <summary>
        /// Cut-stone voussoir primitive. Each stone is evaluated in its own radial/tangent frame,
        /// yielding planar intrados/extrados chords and radial bed faces instead of an annulus slice.
        /// The centre stone is the keystone: same primitive, slightly wider and one voxel prouder.
        /// </summary>
        private static void BuildFrontVoussoirs(ref VoxelBrush brush, int cx, int springY, int frontZ,
            int innerRadius, int outerRadius, int faceDepth, byte stoneMaterial, byte jointMaterial,
            WorldArtVoxelArchDamage damage)
        {
            faceDepth = math.max(2, faceDepth);
            float sector = math.PI / HeroVoussoirCount;
            int keystone = HeroVoussoirCount / 2;

            for (int i = 0; i < HeroVoussoirCount; i++)
            {
                float mid = (i + 0.5f) * sector;
                float ca = math.cos(mid);
                float sa = math.sin(mid);
                float tx = -sa;
                float ty = ca;
                bool isKey = i == keystone;

                float innerPlane = innerRadius - (isKey ? 0.5f : 0.15f);
                float outerPlane = outerRadius + (isKey ? 0.8f : 0.15f);
                float halfAngle = sector * 0.5f;
                float innerHalfWidth = math.tan(halfAngle) * innerPlane - 0.45f;
                float outerHalfWidth = math.tan(halfAngle) * outerPlane - 0.55f;
                if (isKey)
                {
                    innerHalfWidth += 0.35f;
                    outerHalfWidth += 0.55f;
                }

                int localDepth = faceDepth + (isKey ? 1 : ((i & 1) == 0 ? 0 : -1));
                localDepth = math.max(2, localDepth);

                for (int y = 0; y <= outerRadius + 2; y++)
                for (int x = -outerRadius - 2; x <= outerRadius + 2; x++)
                {
                    float radial = x * ca + y * sa;
                    if (radial < innerPlane || radial > outerPlane) continue;
                    float tangent = x * tx + y * ty;
                    float t = math.saturate((radial - innerPlane) / math.max(0.001f, outerPlane - innerPlane));
                    float halfWidth = math.lerp(innerHalfWidth, outerHalfWidth, t);
                    if (math.abs(tangent) > halfWidth) continue;
                    if (OmitRingVoxel(x, y, outerRadius, damage)) continue;
                    for (int z = frontZ; z < frontZ + localDepth; z++)
                        brush.Set(cx + x, springY + y, z, stoneMaterial);
                }
            }

            // Dark backing at the radial beds gives the physical recessed pass a coherent mortar bed.
            for (int boundary = 1; boundary < HeroVoussoirCount; boundary++)
            {
                float angle = math.PI * boundary / HeroVoussoirCount;
                float ca = math.cos(angle);
                float sa = math.sin(angle);
                for (int r = innerRadius - 1; r <= outerRadius + 1; r++)
                {
                    int x = (int)math.round(ca * r);
                    int y = (int)math.round(sa * r);
                    if (OmitRingVoxel(x, y, outerRadius, damage)) continue;
                    brush.Set(cx + x, springY + y, frontZ + math.max(1, faceDepth - 1), jointMaterial);
                }
            }
        }

        private static void BuildBackingMass(ref VoxelBrush brush, int3 origin,
            int pierOffset, int pierWidth, int springY, int outerRadius, int courseHeight,
            int depth, int jointDepth, int faceRecess, byte material, byte jointMaterial, uint seed,
            WorldArtVoxelArchDamage damage)
        {
            int bayHalf = pierOffset + (pierWidth + 1) / 2;
            faceRecess = math.clamp(faceRecess + 1, 2, depth - 2);
            int backingDepth = math.max(2, depth - faceRecess);
            int frontZ = origin.z - depth / 2 + faceRecess;
            int topOffset = outerRadius + 1;
            int outer2 = (outerRadius + 1) * (outerRadius + 1);

            for (int y = 0; y <= topOffset; y++)
            for (int x = -bayHalf; x <= bayHalf; x++)
            {
                if (x * x + y * y <= outer2) continue;
                if (OmitBackingVoxel(x, y, outerRadius, damage)) continue;
                for (int z = frontZ; z < frontZ + backingDepth; z++)
                    brush.Set(origin.x + x, springY + y, z, material);
            }

            for (int y = courseHeight; y < topOffset; y += courseHeight)
            for (int x = -bayHalf + 2; x <= bayHalf - 2; x++)
            {
                if (x * x + y * y <= outer2) continue;
                if (OmitBackingVoxel(x, y, outerRadius, damage)) continue;
                for (int d = 0; d < jointDepth; d++)
                    brush.Set(origin.x + x, springY + y, frontZ + d, jointMaterial);
            }

            int rows = math.max(1, topOffset / courseHeight);
            for (int row = 0; row < rows; row++)
            {
                int rowY = row * courseHeight;
                int rowTop = math.min(topOffset + 1, rowY + courseHeight);
                int seam = ((row & 1) == 0 ? -bayHalf / 2 : bayHalf / 2);
                seam += (int)(Hash(seed + (uint)(row * 53)) % 3u) - 1;
                for (int y = rowY + 1; y < rowTop - 1; y++)
                {
                    if (seam * seam + y * y <= outer2) continue;
                    if (OmitBackingVoxel(seam, y, outerRadius, damage)) continue;
                    for (int d = 0; d < jointDepth; d++)
                        brush.Set(origin.x + seam, springY + y, frontZ + d, jointMaterial);
                }
            }
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

        private static bool OmitBackingVoxel(int x, int y, int outerRadius, WorldArtVoxelArchDamage damage)
        {
            if (damage == WorldArtVoxelArchDamage.BrokenLeftHaunch)
                return x < -outerRadius / 2 && y > outerRadius / 3;
            if (damage == WorldArtVoxelArchDamage.BrokenRightHaunch)
                return x > outerRadius / 2 && y > outerRadius / 3;
            if (damage == WorldArtVoxelArchDamage.BrokenCrown)
                return y > outerRadius - 4 && x > -4 && x < 8;
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
