using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Clean single-owner hero sculpt for Dragon A. This authoring intentionally starts from an
    /// empty object instead of inheriting V3-V6 corrective geometry. Every major silhouette is owned
    /// here: body, neck, skull, horns, limbs, wings, armor and tail. Smooth implicit volumes are
    /// sampled directly into the canonical 10 cm voxel grid.
    /// </summary>
    public static class DragonStatueConceptV7CleanHeroAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Plate = GameMaterialIds.Dirt;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Membrane = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            AuthorCoreBody(a, o);
            AuthorNeck(a, o);
            AuthorHead(a, o);
            AuthorRearLeg(a, o, -1);
            AuthorRearLeg(a, o, 1);
            AuthorForeleg(a, o, -1);
            AuthorForeleg(a, o, 1);
            AuthorWing(a, o, -1);
            AuthorWing(a, o, 1);
            AuthorTail(a, o);
            AuthorVentralArmor(a, o);
            AuthorDorsalSpines(a, o);
            AuthorSurfaceDetail(a, o);
            AuthorPatina(a, o);
        }

        private static void AuthorCoreBody(IStructureAuthoringSession a, int3 o)
        {
            // Compact crouched body: broad pelvis/abdomen behind a narrower lifted chest. The body
            // axis advances toward -Z as it rises, matching the reference's proud seated posture.
            FillEllipsoid(a, o, new float3(0, 37, 18), new float3(31, 21, 34), Body);
            FillEllipsoid(a, o, new float3(0, 52, 4), new float3(27, 25, 29), Body);
            FillEllipsoid(a, o, new float3(0, 67, -10), new float3(23, 22, 22), Body);

            // Integrated haunches carry most of the seated mass.
            FillEllipsoid(a, o, new float3(-27, 31, 23), new float3(20, 18, 23), Body);
            FillEllipsoid(a, o, new float3(27, 31, 23), new float3(20, 18, 23), Body);

            // Shoulder caps keep the front legs attached without widening the entire belly.
            FillEllipsoid(a, o, new float3(-19, 65, -13), new float3(12, 14, 13), Body);
            FillEllipsoid(a, o, new float3(19, 65, -13), new float3(12, 14, 13), Body);
        }

        private static void AuthorNeck(IStructureAuthoringSession a, int3 o)
        {
            // Four overlapping segments establish a visible S: forward at the base, slightly back in
            // the middle, then forward again under the skull. Radii stay substantial at the shoulder.
            VoxelLine(a, o, new float3(0, 67, -11), new float3(-2, 80, -21), 17.5f, 15.5f, Body);
            VoxelLine(a, o, new float3(-2, 79, -21), new float3(1, 92, -29), 15.5f, 13.5f, Body);
            VoxelLine(a, o, new float3(1, 91, -29), new float3(-2, 104, -39), 13.5f, 11.5f, Body);
            VoxelLine(a, o, new float3(-2, 103, -39), new float3(-5, 115, -48), 11.5f, 10.0f, Body);

            // Lateral neck muscles create planar breaks that survive smoothing.
            float3[] centers =
            {
                new float3(0, 78, -20),
                new float3(0, 90, -29),
                new float3(-2, 101, -38),
                new float3(-5, 111, -46),
            };
            float[] radius = { 14.5f, 12.5f, 10.5f, 8.5f };
            for (int i = 0; i < centers.Length; i++)
            {
                float3 c = centers[i];
                float r = radius[i];
                FillEllipsoid(a, o, c + new float3(-r * .72f, 0, .8f), new float3(4.4f, 6.0f, 4.5f), Body);
                FillEllipsoid(a, o, c + new float3(r * .72f, 0, .8f), new float3(4.4f, 6.0f, 4.5f), Body);
            }
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Broad low cranium with pronounced cheek masses. The skull is deliberately larger than
            // V6 so it balances the shoulder/wing silhouette at medium distance.
            FillEllipsoid(a, o, new float3(-5, 120, -59), new float3(18, 13, 15), Body);
            FillEllipsoid(a, o, new float3(-18, 114, -62), new float3(8, 9, 9), Body);
            FillEllipsoid(a, o, new float3(8, 114, -62), new float3(8, 9, 9), Body);

            // Low wedge muzzle, sampled slice by slice so it cannot become a rounded horse snout.
            for (int z = -87; z <= -65; z++)
            {
                float t = (z + 87) / 22f;
                int rx = (int)math.round(math.lerp(6f, 12f, t));
                int ry = (int)math.round(math.lerp(3.5f, 6.5f, t));
                int cy = (int)math.round(math.lerp(116f, 119f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, ry, Body);
            }

            // Angular nose plate and true nostril cuts.
            FillEllipsoid(a, o, new float3(-5, 117, -85), new float3(7, 4, 4), Body);
            Box(a, o, new int3(-10, 117, -89), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 117, -89), new int3(3, 2, 3), Empty);
            FillRun(a, o, -11, 1, 121, -80, Shadow);
            FillRun(a, o, -10, 0, 122, -77, Shadow);

            // Carve an open mouth but preserve a substantial hinge at the rear.
            for (int z = -84; z <= -66; z++)
            {
                float t = (z + 84) / 18f;
                int half = (int)math.round(math.lerp(4.5f, 9.5f, t));
                for (int y = 108; y <= 113; y++)
                    FillRun(a, o, -5 - half, -5 + half, y, z, Empty);
            }

            // Tapered lower jaw, then hinge/cheek volumes reconnect it to the skull.
            for (int z = -84; z <= -63; z++)
            {
                float t = (z + 84) / 21f;
                int rx = (int)math.round(math.lerp(5f, 11f, t));
                int ry = (int)math.round(math.lerp(2.5f, 4.5f, t));
                int cy = (int)math.round(math.lerp(104f, 108f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, math.max(2, ry), Shadow);
            }
            FillEllipsoid(a, o, new float3(-16, 109, -62), new float3(7, 7, 8), Body);
            FillEllipsoid(a, o, new float3(6, 109, -62), new float3(7, 7, 8), Body);
            FillEllipsoid(a, o, new float3(-5, 108, -63), new float3(12, 5, 7), Body);

            // Brows and recessed glowing eyes.
            VoxelLine(a, o, new float3(-5, 129, -67), new float3(-20, 127, -61), 4.0f, 1.4f, Shadow);
            VoxelLine(a, o, new float3(-5, 129, -67), new float3(10, 127, -61), 4.0f, 1.4f, Shadow);
            CarveOval(a, o, new int3(-15, 122, -70), new int3(4, 4, 4));
            CarveOval(a, o, new int3(5, 122, -70), new int3(4, 4, 4));
            FillEllipsoid(a, o, new float3(-15, 122, -72), new float3(2.0f, 1.6f, 1.6f), Eye);
            FillEllipsoid(a, o, new float3(5, 122, -72), new float3(2.0f, 1.6f, 1.6f), Eye);

            // Two dominant crown horns. They sweep backward with a single continuous gesture instead
            // of branching upward like antlers.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 h0 = new float3(-5 + 10*s, 132, -53);
                float3 h1 = new float3(-5 + 16*s, 139, -42);
                float3 h2 = new float3(-5 + 23*s, 142, -28);
                float3 h3 = new float3(-5 + 28*s, 138, -13);
                VoxelLine(a, o, h0, h1, 4.2f, 3.2f, Horn);
                VoxelLine(a, o, h1, h2, 3.2f, 1.8f, Horn);
                VoxelLine(a, o, h2, h3, 1.8f, .16f, Horn);

                // One short post-orbital horn and two cheek spikes establish layered dragon anatomy
                // without creating a second antler branch.
                VoxelLine(a, o,
                    new float3(-5 + 13*s, 126, -57),
                    new float3(-5 + 25*s, 130, -44),
                    2.5f, .14f, Horn);
                VoxelLine(a, o,
                    new float3(-5 + 15*s, 117, -62),
                    new float3(-5 + 27*s, 119, -51),
                    2.1f, .14f, Horn);
                VoxelLine(a, o,
                    new float3(-5 + 14*s, 111, -60),
                    new float3(-5 + 23*s, 108, -50),
                    1.8f, .14f, Horn);
            }

            // Sparse teeth maintain visible mouth negative space.
            int[] toothZ = { -70, -76, -82 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -5 + side * (5.0f + i * 1.1f);
                    float len = i == 1 ? 5.5f : 4.2f;
                    VoxelLine(a, o,
                        new float3(x, 114, toothZ[i]),
                        new float3(x + .35f * side, 114 - len, toothZ[i] - 1),
                        1.1f, .12f, Horn);
                }
            }
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(20*s, 65, -13);
            float3 elbow = new float3(28*s, 44, -27);
            float3 wrist = new float3(26*s, 22, -40);
            float3 palm = new float3(26*s, 8, -52);

            FillEllipsoid(a, o, shoulder, new float3(11.5f, 13.0f, 12.0f), Body);
            VoxelLine(a, o, shoulder, elbow, 9.5f, 7.2f, Body);
            FillEllipsoid(a, o, elbow, new float3(7.8f, 8.5f, 8.8f), Body);
            VoxelLine(a, o, elbow, wrist, 7.0f, 4.8f, Body);
            FillEllipsoid(a, o, wrist, new float3(5.4f, 5.8f, 6.8f), Shadow);
            VoxelLine(a, o, wrist, palm, 4.8f, 4.0f, Body);
            FillEllipsoid(a, o, palm, new float3(9.5f, 5.0f, 10.5f), Body);

            float[] offsets = { -7.8f, -2.6f, 2.6f, 7.8f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float x = 26*s + offsets[i] * s;
                float extra = (i == 1 || i == 2) ? 2.5f : 0f;
                float3 knuckle = new float3(x, 7, -56);
                float3 toe = new float3(x + 1.0f*s, 4.5f, -66 - extra);
                float3 claw = new float3(x + 2.7f*s, 1.6f, -76 - extra);
                VoxelLine(a, o, knuckle, toe, 2.5f, 1.55f, Body);
                VoxelLine(a, o, toe, claw, 1.55f, .12f, Horn);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(27*s, 33, 22);
            float3 knee = new float3(40*s, 24, 5);
            float3 hock = new float3(37*s, 13, -15);
            float3 foot = new float3(37*s, 7, -30);

            FillEllipsoid(a, o, hip, new float3(18.5f, 17.0f, 21.0f), Body);
            VoxelLine(a, o, hip, knee, 12.0f, 8.3f, Body);
            FillEllipsoid(a, o, knee, new float3(9.5f, 9.0f, 10.5f), Body);
            VoxelLine(a, o, knee, hock, 7.8f, 5.5f, Body);
            FillEllipsoid(a, o, hock, new float3(6.2f, 6.5f, 7.5f), Body);
            VoxelLine(a, o, hock, foot, 5.5f, 4.5f, Body);
            FillEllipsoid(a, o, foot, new float3(12.5f, 5.2f, 14.5f), Body);

            float[] offsets = { -8.0f, -2.7f, 2.7f, 8.0f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float x = 37*s + offsets[i] * s;
                float extra = (i == 1 || i == 2) ? 2f : 0f;
                float3 knuckle = new float3(x, 6.5f, -34);
                float3 toe = new float3(x + .8f*s, 4.2f, -44 - extra);
                float3 claw = new float3(x + 2.1f*s, 1.5f, -54 - extra);
                VoxelLine(a, o, knuckle, toe, 2.5f, 1.5f, Body);
                VoxelLine(a, o, toe, claw, 1.5f, .12f, Horn);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            bool heroSide = side > 0;
            float reach = heroSide ? 1.0f : .74f;
            float zShift = heroSide ? 0f : -10f;

            float3 root = new float3(18*s, 73, 9 + zShift);
            float3 elbow = new float3(49*reach*s, heroSide ? 105 : 98, 14 + zShift);
            float3 wrist = new float3(82*reach*s, heroSide ? 133 : 119, 10 + zShift);
            float3 archTip = new float3(112*reach*s, heroSide ? 145 : 130, -3 + zShift);

            float3[] tips = heroSide
                ? new[]
                {
                    new float3(116*s, 121, 2),
                    new float3(112*s, 97, 0),
                    new float3(102*s, 74, -2),
                    new float3(88*s, 55, 1),
                    new float3(69*s, 44, 7),
                    new float3(48*s, 46, 10),
                }
                : new[]
                {
                    new float3(86*s, 108, -6),
                    new float3(81*s, 87, -8),
                    new float3(72*s, 69, -8),
                    new float3(59*s, 56, -5),
                    new float3(44*s, 49, 0),
                    new float3(33*s, 50, 4),
                };

            // Inner membrane joins the back/shoulder to the fan.
            FillTriangle(a, o, root, elbow, tips[5], 1.15f, Membrane);
            FillTriangle(a, o, elbow, wrist, tips[5], 1.05f, Membrane);
            FillTriangle(a, o, wrist, archTip, tips[0], 1.0f, Membrane);

            // Each bay owns an explicit inward notch. This directly creates a scalloped outer edge;
            // there is no rectangular sheet to carve after the fact.
            for (int i = 0; i < tips.Length - 1; i++)
            {
                float3 mid = (tips[i] + tips[i + 1]) * .5f;
                float inward = math.lerp(.54f, .68f, i / (float)(tips.Length - 2));
                float3 notch = math.lerp(wrist, mid, inward);
                notch += new float3(-2.0f*s, -1.0f, 0);
                FillTriangle(a, o, wrist, tips[i], notch, 1.0f, Membrane);
                FillTriangle(a, o, wrist, notch, tips[i + 1], 1.0f, Membrane);
            }

            // Arched leading arm.
            VoxelLine(a, o, root, elbow, 7.0f, 5.0f, Body);
            VoxelLine(a, o, elbow, wrist, 5.0f, 3.2f, Body);
            VoxelLine(a, o, wrist, archTip, 3.2f, .16f, Horn);

            // Five curved structural fingers. The bend points bow away from straight radial lines.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float3 end = tips[i];
                float3 bend = math.lerp(wrist, end, .50f);
                bend += new float3((heroSide ? 6.0f : 4.0f) * s, 4.5f - 1.1f*i, 2.0f);
                float r0 = math.lerp(2.7f, 1.7f, t);
                VoxelLine(a, o, wrist, bend, r0, r0 * .62f, Body);
                VoxelLine(a, o, bend, end, r0 * .62f, .24f, Horn);
            }

            // A few warm membrane creases add depth without turning the wing into a rib cage.
            for (int i = 1; i < 5; i += 2)
            {
                float3 start = math.lerp(wrist, tips[i], .68f);
                VoxelLine(a, o, start, tips[i], .65f, .16f, Plate);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Open foreground sweep. The tip stays to the hero/right side of the front feet rather
            // than completing a ring across the statue.
            float3[] p =
            {
                new float3(15, 37, 39),
                new float3(46, 30, 51),
                new float3(76, 21, 49),
                new float3(101, 12, 34),
                new float3(113, 7, 11),
                new float3(110, 5, -15),
                new float3(99, 4, -37),
                new float3(84, 3, -55),
                new float3(67, 2.5f, -68),
                new float3(53, 2.5f, -77),
            };
            float[] r = { 16.5f, 14.5f, 12.5f, 10.2f, 8.0f, 6.3f, 4.9f, 3.5f, 2.1f, .20f };
            for (int i = 0; i < p.Length - 1; i++)
                VoxelLine(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Large diminishing dorsal blades make the tail read as designed anatomy at a glance.
            for (int i = 2; i < p.Length - 1; i++)
            {
                float t = (i - 2) / (float)(p.Length - 4);
                float h = math.lerp(8.0f, 3.0f, t);
                float r0 = math.lerp(2.0f, .75f, t);
                VoxelLine(a, o,
                    p[i] + new float3(0, 1, 0),
                    p[i] + new float3(0, h, -1.5f),
                    r0, .12f, Horn);
            }

            // Forked blade tip, inspired by the reference tail fin.
            float3 tip = p[p.Length - 1];
            VoxelLine(a, o, tip, tip + new float3(-11, 2, -5), 2.0f, .12f, Horn);
            VoxelLine(a, o, tip, tip + new float3(-8, 5, 2), 1.7f, .12f, Horn);
        }

        private static void AuthorVentralArmor(IStructureAuthoringSession a, int3 o)
        {
            // Seven large overlapping shields. Ellipsoidal plates provide broad surfaces while a
            // tapered lower point establishes overlap direction. They deliberately do not tile like ribs.
            Shield(a, o, new float3(-5, 105, -50), new float3(8.5f, 5.0f, 3.2f));
            Shield(a, o, new float3(-4, 95, -43), new float3(10.0f, 5.8f, 3.3f));
            Shield(a, o, new float3(-2, 85, -36), new float3(11.5f, 6.3f, 3.5f));
            Shield(a, o, new float3(0, 74, -30), new float3(13.5f, 6.8f, 3.8f));
            Shield(a, o, new float3(0, 63, -26), new float3(15.0f, 7.0f, 4.0f));
            Shield(a, o, new float3(0, 52, -23), new float3(16.0f, 7.0f, 4.0f));
            Shield(a, o, new float3(0, 41, -20), new float3(14.5f, 6.5f, 3.8f));
        }

        private static void Shield(IStructureAuthoringSession a, int3 o, float3 center, float3 radius)
        {
            FillEllipsoid(a, o, center, radius, Plate);
            VoxelLine(a, o,
                center + new float3(0, -1.5f, -2.4f),
                center + new float3(0, -radius.y - 3.0f, -3.2f),
                math.max(2.0f, radius.x * .28f), .18f, Plate);
        }

        private static void AuthorDorsalSpines(IStructureAuthoringSession a, int3 o)
        {
            // Crown-to-back crest, spaced widely enough to read as individual fins.
            float3[] bases =
            {
                new float3(-5, 127, -48),
                new float3(-4, 116, -39),
                new float3(-2, 105, -30),
                new float3(0, 94, -21),
                new float3(1, 83, -12),
                new float3(0, 72, -2),
                new float3(0, 61, 10),
                new float3(0, 49, 20),
            };
            for (int i = 0; i < bases.Length; i++)
            {
                float t = i / (float)(bases.Length - 1);
                float h = math.lerp(8.5f, 5.5f, t);
                VoxelLine(a, o, bases[i], bases[i] + new float3(0, h, 4.0f), 2.0f, .13f, Horn);
            }
        }

        private static void AuthorSurfaceDetail(IStructureAuthoringSession a, int3 o)
        {
            // Visible-side neck scales. Geometry uses the body material so light, not checkerboard
            // color, carries the tertiary form.
            for (int row = 0; row < 7; row++)
            {
                float y = 83 + row * 6.0f;
                float z = -18 - row * 4.8f;
                float x = 12.5f - row * .65f;
                for (int j = 0; j < 3; j++)
                {
                    float yy = y + (j - 1) * 2.2f;
                    FillEllipsoid(a, o, new float3(x, yy, z - 7.0f), new float3(3.2f, 2.5f, 1.4f), Body);
                }
            }

            // Shoulder and haunch scale islands on both sides.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                for (int i = 0; i < 7; i++)
                {
                    float angle = math.radians(-58 + i * 18);
                    float3 shoulder = new float3(
                        20*s + math.sin(angle) * 11*s,
                        64 + math.cos(angle) * 10,
                        -18 - i * .8f);
                    FillEllipsoid(a, o, shoulder, new float3(3.3f, 2.6f, 1.5f), Body);

                    float3 haunch = new float3(
                        29*s + math.sin(angle) * 14*s,
                        31 + math.cos(angle) * 11,
                        12 - i * .4f);
                    FillEllipsoid(a, o, haunch, new float3(3.6f, 2.8f, 1.6f), Body);
                }
            }

            // Dark crease accents under major joints and armor edges.
            FillEllipsoid(a, o, new float3(20, 56, -21), new float3(4.5f, 3.0f, 2.0f), Shadow);
            FillEllipsoid(a, o, new float3(-20, 56, -21), new float3(4.5f, 3.0f, 2.0f), Shadow);
            FillEllipsoid(a, o, new float3(35, 20, -8), new float3(4.0f, 2.5f, 2.0f), Shadow);
            FillEllipsoid(a, o, new float3(-35, 20, -8), new float3(4.0f, 2.5f, 2.0f), Shadow);
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            // Sparse moss follows sheltered seams rather than becoming random freckles.
            FillEllipsoid(a, o, new float3(8, 94, -29), new float3(3.0f, 1.8f, 2.0f), Moss);
            FillEllipsoid(a, o, new float3(17, 68, -5), new float3(3.5f, 1.8f, 2.5f), Moss);
            FillEllipsoid(a, o, new float3(30, 37, 30), new float3(4.0f, 1.8f, 3.0f), Moss);
            FillEllipsoid(a, o, new float3(76, 22, 47), new float3(3.5f, 1.6f, 2.5f), Moss);
            FillEllipsoid(a, o, new float3(88, 78, 2), new float3(3.0f, 1.5f, 2.0f), Moss);
        }

        private static void FillRun(
            IStructureAuthoringSession a, int3 o, int x0, int x1, int y, int z, byte material)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            for (int x = x0; x <= x1; x++)
                a.Set(o.x + x, o.y + y, o.z + z, material);
        }

        private static void FillOvalSliceXY(
            IStructureAuthoringSession a, int3 o, int cx, int cy, int z, int rx, int ry, byte material)
        {
            float sx = math.max(1, rx);
            float sy = math.max(1, ry);
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x + .5f - cx) / sx;
                float dy = (y + .5f - cy) / sy;
                if (dx * dx + dy * dy <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void FillEllipsoid(
            IStructureAuthoringSession a, int3 o, float3 c, float3 r, byte material)
        {
            int3 min = (int3)math.floor(c - r - 1);
            int3 max = (int3)math.ceil(c + r + 1);
            float3 safe = math.max(r, new float3(.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + .5f, y + .5f, z + .5f) - c) / safe;
                if (math.dot(q, q) <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void CarveOval(IStructureAuthoringSession a, int3 o, int3 c, int3 r)
        {
            for (int y = c.y - r.y; y <= c.y + r.y; y++)
            for (int z = c.z - r.z; z <= c.z + r.z; z++)
            for (int x = c.x - r.x; x <= c.x + r.x; x++)
            {
                float dx = (x + .5f - c.x) / math.max(1f, r.x);
                float dy = (y + .5f - c.y) / math.max(1f, r.y);
                float dz = (z + .5f - c.z) / math.max(1f, r.z);
                if (dx * dx + dy * dy + dz * dz <= 1f)
                    a.Set(o.x + x, o.y + y, o.z + z, Empty);
            }
        }

        private static void VoxelLine(
            IStructureAuthoringSession a, int3 o, float3 p0, float3 p1, float r0, float r1, byte material)
        {
            float3 axis = p1 - p0;
            float len = math.length(axis);
            int steps = math.max(1, (int)math.ceil(len * 1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float3 p = math.lerp(p0, p1, t);
                float r = math.lerp(r0, r1, t);
                for (int y = (int)math.floor(p.y - r); y <= (int)math.ceil(p.y + r); y++)
                for (int z = (int)math.floor(p.z - r); z <= (int)math.ceil(p.z + r); z++)
                for (int x = (int)math.floor(p.x - r); x <= (int)math.ceil(p.x + r); x++)
                {
                    float3 d = new float3(x + .5f, y + .5f, z + .5f) - p;
                    if (math.dot(d, d) <= r * r)
                        a.Set(o.x + x, o.y + y, o.z + z, material);
                }
            }
        }

        private static void FillTriangle(
            IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc, float thick, byte material)
        {
            float3 n = math.normalizesafe(math.cross(vb - va, vc - va), new float3(0, 0, 1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb, vc)) - thick - 1);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb, vc)) + thick + 1);
            float3 v0 = vb - va;
            float3 v1 = vc - va;
            float d00 = math.dot(v0, v0);
            float d01 = math.dot(v0, v1);
            float d11 = math.dot(v1, v1);
            float den = d00 * d11 - d01 * d01;
            if (math.abs(den) < .0001f) return;

            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 p = new float3(x + .5f, y + .5f, z + .5f);
                float dist = math.dot(p - va, n);
                if (math.abs(dist) > thick) continue;
                float3 v2 = (p - n * dist) - va;
                float d20 = math.dot(v2, v0);
                float d21 = math.dot(v2, v1);
                float v = (d11 * d20 - d01 * d21) / den;
                float w = (d00 * d21 - d01 * d20) / den;
                float u = 1 - v - w;
                if (u >= 0 && v >= 0 && w >= 0)
                    a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            int3 max = min + size;
            for (int y = min.y; y < max.y; y++)
            for (int z = min.z; z < max.z; z++)
            for (int x = min.x; x < max.x; x++)
                a.Set(o.x + x, o.y + y, o.z + z, material);
        }
    }
}
