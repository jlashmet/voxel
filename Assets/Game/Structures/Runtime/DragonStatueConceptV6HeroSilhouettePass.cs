using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V6 hero-silhouette rebuild after production review of V5. This pass deliberately replaces
    /// entire failed regions instead of decorating them: head/crown, neck, exposed wings, foreground
    /// tail, complete forelimbs, distal rear legs/feet, and ventral armor.
    /// </summary>
    public static class DragonStatueConceptV6HeroSilhouettePass
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Scale = GameMaterialIds.Stone;
        private const byte Warm = GameMaterialIds.Dirt;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            RebuildNeckAndHead(a, o);
            RebuildWings(a, o);

            // Clear every failed distal-limb generation before the final tail/limb authoring. The
            // old fixes overlap spatially, so doing all destructive work first avoids pass-order damage.
            ClearFailedLimbs(a, o);
            RebuildTail(a, o);
            RebuildRearFeet(a, o);
            RebuildForelimbs(a, o);
            RebuildVentralArmor(a, o);
            AddPrimarySurfaceBreakup(a, o);
        }

        private static void RebuildNeckAndHead(IStructureAuthoringSession a, int3 o)
        {
            // Remove the vertical V3 neck, all later head generations, and the horn/cheek envelope.
            // A single large ownership clear prevents isolated strips from older passes surviving.
            ClearBox(a, o, new int3(-30, 76, -55), new int3(60, 50, 61));
            ClearBox(a, o, new int3(-50, 100, -97), new int3(100, 64, 99));

            // Long but powerful S-neck. The side profile changes direction twice and broadens into
            // the shoulder girdle instead of standing on the torso like a pipe.
            VoxelLine(a, o, new float3(0, 71, -3), new float3(3, 84, -10), 20.0f, 18.0f, Body);
            VoxelLine(a, o, new float3(3, 83, -10), new float3(-1, 98, -25), 18.0f, 15.0f, Body);
            VoxelLine(a, o, new float3(-1, 97, -25), new float3(-6, 111, -39), 15.0f, 12.0f, Body);
            VoxelLine(a, o, new float3(-6, 110, -39), new float3(-6, 121, -49), 12.0f, 10.0f, Body);

            // Side muscle/scute masses make the neck read as layered anatomy, not one smooth tube.
            float3[] neckCenters =
            {
                new float3(0, 82, -10),
                new float3(1, 91, -18),
                new float3(-2, 101, -28),
                new float3(-5, 111, -39),
            };
            float[] neckX = { 16.0f, 14.0f, 12.0f, 9.5f };
            for (int i = 0; i < neckCenters.Length; i++)
            {
                float3 c = neckCenters[i];
                FillEllipsoid(a, o, c + new float3(-neckX[i], 0, 1), new float3(5.5f, 6.5f, 5.0f), Body);
                FillEllipsoid(a, o, c + new float3(neckX[i], 0, 1), new float3(5.5f, 6.5f, 5.0f), Body);
            }

            // Compact angular rear skull, broad at the cheek and brow.
            for (int z = -65; z <= -48; z++)
            {
                float t = (z + 65) / 17f;
                int rx = (int)math.round(math.lerp(12f, 16f, t));
                int ry = (int)math.round(math.lerp(8f, 11f, t));
                int cy = (int)math.round(math.lerp(120f, 124f, t));
                FillOvalSliceXY(a, o, -6, cy, z, rx, ry, Body);
            }
            FillEllipsoid(a, o, new float3(-18, 116, -58), new float3(8, 9, 9), Body);
            FillEllipsoid(a, o, new float3(6, 116, -58), new float3(8, 9, 9), Body);

            // Upper muzzle is a low wedge that widens into the skull.
            for (int z = -92; z <= -64; z++)
            {
                float t = (z + 92) / 28f;
                int rx = (int)math.round(math.lerp(5f, 11f, t));
                int ry = (int)math.round(math.lerp(3f, 6f, t));
                int cy = (int)math.round(math.lerp(116f, 120f, t));
                FillOvalSliceXY(a, o, -6, cy, z, rx, ry, Body);
            }

            // Open mouth cavity. The hinge remains solid at the rear so the jaw is connected.
            for (int z = -88; z <= -64; z++)
            {
                float t = (z + 88) / 24f;
                int half = (int)math.round(math.lerp(4f, 9f, t));
                for (int y = 108; y <= 113; y++)
                    FillRun(a, o, -6 - half, -6 + half, y, z, Empty);
            }

            // Lower jaw: tapered chin with broad rear hinge masses.
            for (int z = -88; z <= -62; z++)
            {
                float t = (z + 88) / 26f;
                int rx = (int)math.round(math.lerp(5f, 11f, t));
                int ry = (int)math.round(math.lerp(2.5f, 4.5f, t));
                int cy = (int)math.round(math.lerp(104f, 108f, t));
                FillOvalSliceXY(a, o, -6, cy, z, rx, math.max(2, ry), Shadow);
            }
            FillEllipsoid(a, o, new float3(-17, 109, -60), new float3(7, 7, 8), Body);
            FillEllipsoid(a, o, new float3(5, 109, -60), new float3(7, 7, 8), Body);
            FillEllipsoid(a, o, new float3(-6, 108, -60), new float3(12, 5, 7), Body);

            // Brow shelves, recessed eyes and nose plane.
            VoxelLine(a, o, new float3(-7, 129, -63), new float3(-22, 127, -57), 4.2f, 1.2f, Shadow);
            VoxelLine(a, o, new float3(-5, 129, -63), new float3(10, 127, -57), 4.2f, 1.2f, Shadow);
            CarveOval(a, o, new int3(-16, 121, -68), new int3(4, 4, 4));
            CarveOval(a, o, new int3(4, 121, -68), new int3(4, 4, 4));
            Box(a, o, new int3(-17, 121, -71), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(3, 121, -71), new int3(3, 2, 2), Eye);
            FillRun(a, o, -11, -1, 119, -89, Scale);
            FillRun(a, o, -10, -2, 120, -88, Scale);
            Box(a, o, new int3(-11, 116, -93), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-4, 116, -93), new int3(3, 2, 3), Empty);

            // Dominant horns sweep backward more than upward. Three tiers create a crown without the
            // V4/V5 antler forest.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;

                float3 h0 = new float3(-6 + 8*s, 133, -49);
                float3 h1 = new float3(-6 + 16*s, 141, -38);
                float3 h2 = new float3(-6 + 24*s, 146, -22);
                float3 h3 = new float3(-6 + 29*s, 143, -6);
                VoxelLine(a, o, h0, h1, 3.8f, 2.8f, Warm);
                VoxelLine(a, o, h1, h2, 2.8f, 1.5f, Warm);
                VoxelLine(a, o, h2, h3, 1.5f, .18f, Warm);

                float3 m0 = new float3(-6 + 10*s, 128, -50);
                float3 m1 = new float3(-6 + 19*s, 135, -38);
                float3 m2 = new float3(-6 + 25*s, 136, -24);
                VoxelLine(a, o, m0, m1, 2.7f, 1.4f, Warm);
                VoxelLine(a, o, m1, m2, 1.4f, .16f, Warm);

                VoxelLine(a, o,
                    new float3(-6 + 13*s, 119, -57),
                    new float3(-6 + 26*s, 122, -45),
                    2.1f, .15f, Warm);
                VoxelLine(a, o,
                    new float3(-6 + 13*s, 113, -55),
                    new float3(-6 + 23*s, 111, -44),
                    1.8f, .15f, Warm);
            }

            // Sparse irregular teeth preserve negative space in the mouth.
            int[] toothZ = { -70, -77, -85 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -6 + side * (5.0f + i * 1.3f);
                    float len = i == 1 ? 6.0f : 4.5f;
                    VoxelLine(a, o,
                        new float3(x, 114, toothZ[i]),
                        new float3(x + .3f * side, 114 - len, toothZ[i] - 1),
                        1.15f, .14f, Warm);
                }
            }

            // Head/neck dorsal fins provide a clear taper into the back.
            float3[] crest =
            {
                new float3(-6, 126, -45),
                new float3(-5, 116, -37),
                new float3(-3, 106, -28),
                new float3(0, 96, -19),
                new float3(1, 86, -10),
            };
            for (int i = 0; i < crest.Length; i++)
            {
                float h = math.lerp(8.0f, 4.0f, i / (float)(crest.Length - 1));
                VoxelLine(a, o, crest[i], crest[i] + new float3(0, h, 3), 1.8f, .12f, Warm);
            }
        }

        private static void RebuildWings(IStructureAuthoringSession a, int3 o)
        {
            // Own the whole exposed wing envelope so no V3/V4/V5 strips survive.
            ClearBox(a, o, new int3(26, 34, 7), new int3(80, 120, 45));
            ClearBox(a, o, new int3(-106, 34, 7), new int3(80, 120, 45));

            AuthorWing(a, o, -1);
            AuthorWing(a, o, 1);
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 root = new float3(17*s, 78, 12);
            float3 elbow = new float3(45*s, 108, 20);
            float3 wrist = new float3(78*s, 136, 27);
            float3 hook = new float3(96*s, 145, 30);

            // Broad fan terminals. Their spacing is intentionally non-uniform so the trailing edge
            // arcs instead of becoming a rectangular curtain.
            float3[] edge =
            {
                new float3(98*s, 123, 34),
                new float3(99*s, 102, 34),
                new float3(93*s, 80, 32),
                new float3(81*s, 60, 28),
                new float3(64*s, 47, 23),
                new float3(43*s, 47, 17),
            };

            // Membrane first, then carve scallops, then restore the structural skeleton.
            FillTriangle(a, o, root, elbow, edge[5], 1.1f, Shadow);
            FillTriangle(a, o, elbow, wrist, edge[5], 1.0f, Shadow);
            FillTriangle(a, o, wrist, hook, edge[0], .95f, Shadow);
            for (int i = 0; i < edge.Length - 1; i++)
                FillTriangle(a, o, wrist, edge[i], edge[i + 1], .95f, Shadow);

            // Deep scalloped bays; smaller toward the body.
            for (int i = 0; i < edge.Length - 1; i++)
            {
                float3 mid = (edge[i] + edge[i + 1]) * .5f;
                int rx = math.max(5, 9 - i);
                int ry = math.max(4, 8 - i / 2);
                CarveOval(a, o,
                    (int3)math.round(mid + new float3(-3*s, -2, 0)),
                    new int3(rx, ry, 4));
            }

            // Heavy leading spar.
            VoxelLine(a, o, root, elbow, 6.8f, 5.0f, Body);
            VoxelLine(a, o, elbow, wrist, 5.0f, 3.1f, Body);
            VoxelLine(a, o, wrist, hook, 3.1f, .18f, Warm);

            // Curved two-segment fingers. Bends prevent the venetian-blind read produced by straight
            // wrist-to-edge rays.
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float3 end = edge[i + 1];
                float3 bend = math.lerp(wrist, end, .52f);
                bend += new float3((5.0f - i) * s, 3.0f - i * .7f, 1.5f);
                float r0 = math.lerp(2.6f, 1.9f, t);
                VoxelLine(a, o, wrist, bend, r0, r0 * .65f, Scale);
                VoxelLine(a, o, bend, end, r0 * .65f, .28f, Scale);
            }

            // Two small trailing hooks add silhouette punctuation without turning every bay into ribs.
            VoxelLine(a, o, edge[1], edge[1] + new float3(4*s, -6, -1), 1.0f, .12f, Warm);
            VoxelLine(a, o, edge[3], edge[3] + new float3(3*s, -5, -1), .9f, .12f, Warm);

            // Warm inner accents keep the membrane from reading as a featureless black sheet.
            VoxelLine(a, o, math.lerp(wrist, edge[1], .72f), edge[1], .8f, .2f, Warm);
            VoxelLine(a, o, math.lerp(wrist, edge[3], .76f), edge[3], .7f, .18f, Warm);
        }

        private static void RebuildTail(IStructureAuthoringSession a, int3 o)
        {
            // Clear every historical tail centerline after the body-integrated root. This is much
            // safer than the old rectangular foreground clear and prevents hidden leftovers.
            float3[] old =
            {
                new float3(20,38,39),new float3(49,32,53),new float3(76,24,53),new float3(98,15,36),
                new float3(102,9,12),new float3(94,7,-18),new float3(73,6,-45),new float3(43,5,-65),
                new float3(8,4,-77),new float3(-25,4,-79),new float3(-47,4,-71)
            };
            float[] oldR = { 18,16,14,12,10,8.5f,7.2f,6,5,3.5f,.35f };
            for (int i = 1; i < old.Length - 1; i++)
                ClearLine(a, o, old[i], old[i + 1], oldR[i] + 2.0f, oldR[i + 1] + 2.0f);

            float3[] v5 =
            {
                new float3(45,31,50), new float3(65,22,45), new float3(77,13,28),
                new float3(75,8,8), new float3(65,6,-9), new float3(54,5,-21), new float3(45,4,-29)
            };
            float[] v5R = { 14,11,8,6,4,2,.25f };
            for (int i = 0; i < v5.Length - 1; i++)
                ClearLine(a, o, v5[i], v5[i + 1], v5R[i] + 2.0f, v5R[i + 1] + 2.0f);
            ClearLine(a, o, new float3(77,8,-43), new float3(38,4,-65), 8.5f, 2.5f);

            // One large open C sweep on the visible/right quarter. It never crosses the front feet.
            float3[] p =
            {
                new float3(20,38,39),
                new float3(49,31,52),
                new float3(78,21,50),
                new float3(100,11,34),
                new float3(108,6,10),
                new float3(104,4,-17),
                new float3(92,3,-40),
                new float3(76,3,-59),
                new float3(62,3,-72),
                new float3(54,3,-79),
            };
            float[] r = { 17,15,13,10.5f,8.5f,6.8f,5.2f,3.8f,2.3f,.22f };
            for (int i = 0; i < p.Length - 1; i++)
                VoxelLine(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Dorsal tail blades track the curve and diminish continuously.
            for (int i = 1; i < p.Length - 2; i++)
            {
                float t = (i - 1) / (float)(p.Length - 4);
                float h = math.lerp(9.0f, 3.5f, t);
                float width = math.lerp(2.1f, .8f, t);
                VoxelLine(a, o,
                    p[i] + new float3(0, 1, 0),
                    p[i] + new float3(0, h, -1.5f),
                    width, .12f, Warm);
            }
        }

        private static void ClearFailedLimbs(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                int foreMinX = side < 0 ? -57 : 10;
                ClearBox(a, o, new int3(foreMinX, 0, -91), new int3(47, 70, 91));

                int rearMinX = side < 0 ? -63 : 20;
                ClearBox(a, o, new int3(rearMinX, 0, -61), new int3(43, 36, 67));
            }
        }

        private static void RebuildForelimbs(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 shoulder = new float3(22*s, 66, -3);
                float3 elbow = new float3(32*s, 45, -19);
                float3 wrist = new float3(29*s, 23, -39);
                float3 palm = new float3(29*s, 8, -56);

                FillEllipsoid(a, o, shoulder, new float3(12.5f, 13.5f, 12.5f), Body);
                VoxelLine(a, o, shoulder, elbow, 10.5f, 7.5f, Body);
                FillEllipsoid(a, o, elbow, new float3(8.5f, 9.0f, 9.5f), Body);
                VoxelLine(a, o, elbow, wrist, 7.5f, 5.0f, Body);
                FillEllipsoid(a, o, wrist, new float3(5.5f, 6.0f, 7.0f), Shadow);
                VoxelLine(a, o, wrist, palm, 5.0f, 4.1f, Body);
                FillEllipsoid(a, o, palm, new float3(9.5f, 5.0f, 10.5f), Body);

                float[] offsets = { -8.4f, -2.8f, 2.8f, 8.4f };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float x = 29*s + offsets[i];
                    float centerBias = (i == 1 || i == 2) ? 3.5f : 0f;
                    float3 knuckle = new float3(x, 7, -59 - centerBias * .3f);
                    float3 toe = new float3(x + 1.0f*s, 4.6f, -70 - centerBias);
                    float3 claw = new float3(x + 2.8f*s, 1.7f, -81 - centerBias);
                    VoxelLine(a, o, knuckle, toe, 2.6f, 1.55f, Body);
                    VoxelLine(a, o, toe, claw, 1.6f, .13f, Warm);
                }
            }
        }

        private static void RebuildRearFeet(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 knee = new float3(43*s, 25, 3);
                float3 hock = new float3(40*s, 13, -17);
                float3 foot = new float3(41*s, 7, -30);

                FillEllipsoid(a, o, knee, new float3(11.5f, 10.5f, 12.0f), Body);
                VoxelLine(a, o, knee, hock, 8.5f, 5.8f, Body);
                FillEllipsoid(a, o, hock, new float3(6.5f, 7.0f, 8.0f), Body);
                VoxelLine(a, o, hock, foot, 5.8f, 4.8f, Body);
                FillEllipsoid(a, o, foot, new float3(14.5f, 5.5f, 17.0f), Body);

                float[] offsets = { -9.0f, -3.0f, 3.0f, 9.0f };
                for (int i = 0; i < offsets.Length; i++)
                {
                    float x = 41*s + offsets[i];
                    float extra = (i == 1 || i == 2) ? 3.0f : 0f;
                    float3 knuckle = new float3(x, 6.5f, -34);
                    float3 toe = new float3(x + .8f*s, 4.2f, -45 - extra);
                    float3 claw = new float3(x + 2.2f*s, 1.6f, -57 - extra);
                    VoxelLine(a, o, knuckle, toe, 2.8f, 1.7f, Body);
                    VoxelLine(a, o, toe, claw, 1.7f, .13f, Warm);
                }
            }
        }

        private static void RebuildVentralArmor(IStructureAuthoringSession a, int3 o)
        {
            // Overwrite the old horizontal warm bands with a continuous body surface first.
            VoxelLine(a, o, new float3(0, 108, -35), new float3(0, 58, -15), 11.0f, 19.0f, Body);

            // Nested shields overlap downward. Each plate narrows at both top and bottom so no row
            // can read as a rectangular rib.
            for (int i = 0; i < 10; i++)
            {
                int cy = 108 - i * 6;
                int z = -39 + i * 2;
                int half = 7 + i / 2;
                AuthorShield(a, o, cy, z, half);
            }
        }

        private static void AuthorShield(IStructureAuthoringSession a, int3 o, int cy, int z, int half)
        {
            int[] taper = { 4, 1, 0, 1, 4, 7 };
            for (int row = 0; row < taper.Length; row++)
            {
                int h = math.max(2, half - taper[row]);
                int y = cy + 2 - row;
                int depth = row >= 3 ? 3 : 2;
                for (int dz = 0; dz < depth; dz++)
                    FillRun(a, o, -h, h, y, z - dz, Warm);
            }
            // Small downward point in the center makes the overlap direction unambiguous.
            FillRun(a, o, -math.max(1, half / 3), math.max(1, half / 3), cy - 4, z - 2, Warm);
        }

        private static void AddPrimarySurfaceBreakup(IStructureAuthoringSession a, int3 o)
        {
            // Large, sparse shoulder/haunch scale islands. These are secondary form accents rather
            // than a tiled texture; tertiary polish will be added only after silhouette approval.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                FillEllipsoid(a, o, new float3(22*s, 72, 0), new float3(5.0f, 3.5f, 2.0f), Scale);
                FillEllipsoid(a, o, new float3(27*s, 61, 8), new float3(4.5f, 3.2f, 2.0f), Scale);
                FillEllipsoid(a, o, new float3(31*s, 42, 24), new float3(5.2f, 3.6f, 2.0f), Scale);
                FillEllipsoid(a, o, new float3(40*s, 31, 23), new float3(4.5f, 3.0f, 1.8f), Scale);
            }

            // Moss is restricted to creases so the silhouette stays clean.
            Box(a, o, new int3(-10, 97, -26), new int3(3, 2, 3), Moss);
            Box(a, o, new int3(13, 72, 5), new int3(3, 2, 3), Moss);
            Box(a, o, new int3(30, 35, 38), new int3(3, 2, 3), Moss);
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

        private static void ClearLine(
            IStructureAuthoringSession a, int3 o, float3 p0, float3 p1, float r0, float r1)
        {
            VoxelLine(a, o, p0, p1, r0, r1, Empty);
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

        private static void ClearBox(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            Box(a, o, min, size, Empty);
        }
    }
}
