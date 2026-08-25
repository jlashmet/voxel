using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V8 is a production-capture-driven form correction over the clean V7 base. It owns only the
    /// regions that were still structurally wrong in the V7 hero render: crown/head, front limbs,
    /// distal rear limbs, wings, ventral armor and foreground tail. Each owned region is cleared
    /// before being rebuilt so rejected V7 silhouettes cannot survive underneath the correction.
    /// </summary>
    public static class DragonStatueConceptV8AAAFormPass
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Armor = GameMaterialIds.Stone;
        private const byte Horn = GameMaterialIds.Dirt;
        private const byte Membrane = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Apply(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            ClearOwnedRegions(a, o);
            RebuildHeadAndCrown(a, o);
            RebuildFrontLimbs(a, o);
            RebuildRearDistalLimbs(a, o);
            RebuildWings(a, o);
            RebuildVentralArmor(a, o);
            RebuildTail(a, o);
            AddSilhouetteAccents(a, o);
        }

        private static void ClearOwnedRegions(IStructureAuthoringSession a, int3 o)
        {
            // Entire V7 head/crown envelope, including cheek spikes and goat-like horizontal horns.
            ClearBox(a, o, new int3(-42, 100, -96), new int3(76, 66, 96));

            // Distal front limbs. Keep the shoulder sockets attached to the torso.
            ClearBox(a, o, new int3(-55, 0, -86), new int3(38, 64, 82));
            ClearBox(a, o, new int3(17, 0, -86), new int3(38, 64, 82));

            // Distal rear limbs/feet. Preserve the large V7 haunches.
            ClearBox(a, o, new int3(-57, 0, -62), new int3(36, 34, 69));
            ClearBox(a, o, new int3(21, 0, -62), new int3(36, 34, 69));

            // Exposed wings only. Roots inside +/-28 remain connected to the shoulder/back mass.
            ClearBox(a, o, new int3(28, 38, -26), new int3(99, 122, 70));
            ClearBox(a, o, new int3(-127, 38, -26), new int3(99, 122, 70));

            // V7 chest plates and their protruding points. Rebuild the front body surface below.
            ClearBox(a, o, new int3(-21, 34, -58), new int3(42, 80, 43));

            // V7 foreground tail after its body-integrated root. Clearing line-by-line avoids erasing
            // the haunch while removing the oversized snake-like loop and detached fork fragment.
            float3[] oldTail =
            {
                new float3(15,37,39), new float3(46,30,51), new float3(76,21,49),
                new float3(101,12,34), new float3(113,7,11), new float3(110,5,-15),
                new float3(99,4,-37), new float3(84,3,-55), new float3(67,2.5f,-68),
                new float3(53,2.5f,-77)
            };
            float[] r = { 18, 16, 14, 12, 10, 8.5f, 7, 5.5f, 4, 3 };
            for (int i = 1; i < oldTail.Length - 1; i++)
                VoxelLine(a, o, oldTail[i], oldTail[i + 1], r[i], r[i + 1], Empty);
            VoxelLine(a, o, oldTail[oldTail.Length - 1], new float3(42, 5, -84), 4.5f, .5f, Empty);
        }

        private static void RebuildHeadAndCrown(IStructureAuthoringSession a, int3 o)
        {
            // Reconnect neck into a larger angular skull. The cranium is higher at the rear and low
            // over the muzzle, matching the reference's predatory wedge rather than a horse head.
            VoxelLine(a, o, new float3(-3, 108, -43), new float3(-5, 119, -54), 11.0f, 10.0f, Body);
            FillEllipsoid(a, o, new float3(-5, 123, -59), new float3(18.5f, 13.5f, 15.5f), Body);
            FillEllipsoid(a, o, new float3(-18, 116, -62), new float3(8.5f, 9.5f, 9.5f), Body);
            FillEllipsoid(a, o, new float3(8, 116, -62), new float3(8.5f, 9.5f, 9.5f), Body);

            // Long low muzzle. Width grows toward the skull, while height remains compressed.
            for (int z = -91; z <= -66; z++)
            {
                float t = (z + 91) / 25f;
                int rx = (int)math.round(math.lerp(5.5f, 12.5f, t));
                int ry = (int)math.round(math.lerp(3.0f, 6.0f, t));
                int cy = (int)math.round(math.lerp(118f, 121f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, math.max(2, ry), Body);
            }

            // Nose cap and nostrils.
            FillEllipsoid(a, o, new float3(-5, 119, -89), new float3(6.5f, 3.7f, 3.8f), Body);
            Box(a, o, new int3(-10, 119, -93), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 119, -93), new int3(3, 2, 3), Empty);

            // Open mouth with a narrower lower jaw than V7. The cavity is deepest at mid-muzzle.
            for (int z = -87; z <= -66; z++)
            {
                float t = (z + 87) / 21f;
                int half = (int)math.round(math.lerp(4.2f, 9.0f, t));
                for (int y = 109; y <= 115; y++)
                    FillRun(a, o, -5 - half, -5 + half, y, z, Empty);
            }
            for (int z = -87; z <= -63; z++)
            {
                float t = (z + 87) / 24f;
                int rx = (int)math.round(math.lerp(4.8f, 10.5f, t));
                int ry = (int)math.round(math.lerp(2.1f, 3.8f, t));
                int cy = (int)math.round(math.lerp(104f, 109f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, math.max(2, ry), Shadow);
            }
            FillEllipsoid(a, o, new float3(-16, 110, -62), new float3(7, 6.5f, 7.5f), Body);
            FillEllipsoid(a, o, new float3(6, 110, -62), new float3(7, 6.5f, 7.5f), Body);

            // Layered brow/temple planes and bright recessed eyes.
            VoxelLine(a, o, new float3(-5, 132, -69), new float3(-20, 129, -62), 4.5f, 1.6f, Shadow);
            VoxelLine(a, o, new float3(-5, 132, -69), new float3(10, 129, -62), 4.5f, 1.6f, Shadow);
            CarveOval(a, o, new int3(-15, 124, -71), new int3(4, 4, 4));
            CarveOval(a, o, new int3(5, 124, -71), new int3(4, 4, 4));
            FillEllipsoid(a, o, new float3(-15, 124, -73), new float3(2.0f, 1.5f, 1.5f), Eye);
            FillEllipsoid(a, o, new float3(5, 124, -73), new float3(2.0f, 1.5f, 1.5f), Eye);

            // Crown horns now arc strongly upward first, then sweep backward. Their lateral spread is
            // intentionally restrained so the silhouette reads dragon crown, not goat/antlers.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 h0 = new float3(-5 + 9*s, 134, -54);
                float3 h1 = new float3(-5 + 13*s, 146, -46);
                float3 h2 = new float3(-5 + 16*s, 154, -35);
                float3 h3 = new float3(-5 + 18*s, 158, -23);
                float3 h4 = new float3(-5 + 18*s, 155, -11);
                VoxelLine(a, o, h0, h1, 4.1f, 3.2f, Horn);
                VoxelLine(a, o, h1, h2, 3.2f, 2.2f, Horn);
                VoxelLine(a, o, h2, h3, 2.2f, 1.3f, Horn);
                VoxelLine(a, o, h3, h4, 1.3f, .14f, Horn);

                // Swept temple/cheek fins echo the reference crown without competing with the horns.
                VoxelLine(a, o,
                    new float3(-5 + 13*s, 130, -57),
                    new float3(-5 + 25*s, 135, -45),
                    2.4f, .14f, Horn);
                VoxelLine(a, o,
                    new float3(-5 + 15*s, 121, -62),
                    new float3(-5 + 28*s, 124, -52),
                    2.0f, .14f, Horn);
                VoxelLine(a, o,
                    new float3(-5 + 14*s, 113, -60),
                    new float3(-5 + 23*s, 111, -50),
                    1.6f, .12f, Horn);
            }

            // Dorsal head plates break the skull into layered armor.
            FillEllipsoid(a, o, new float3(-5, 136, -58), new float3(7.0f, 3.2f, 5.0f), Armor);
            FillEllipsoid(a, o, new float3(-5, 132, -48), new float3(6.0f, 2.8f, 4.0f), Armor);

            // Sparse teeth preserve mouth negative space.
            int[] toothZ = { -70, -77, -84 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -5 + side * (4.8f + i * 1.15f);
                    float len = i == 1 ? 5.8f : 4.4f;
                    VoxelLine(a, o,
                        new float3(x, 116, toothZ[i]),
                        new float3(x + .3f * side, 116 - len, toothZ[i] - 1),
                        1.0f, .12f, Armor);
                }
            }
        }

        private static void RebuildFrontLimbs(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                // Pronounced elbow-out / wrist-in bend removes the hanging-column/gorilla read.
                float3 shoulder = new float3(20*s, 64, -13);
                float3 elbow = new float3(30*s, 47, -17);
                float3 wrist = new float3(21*s, 27, -36);
                float3 palm = new float3(22*s, 9, -49);

                VoxelLine(a, o, shoulder, elbow, 8.8f, 6.7f, Body);
                FillEllipsoid(a, o, elbow, new float3(7.2f, 7.5f, 8.0f), Body);
                VoxelLine(a, o, elbow, wrist, 6.5f, 4.5f, Body);
                FillEllipsoid(a, o, wrist, new float3(5.0f, 5.2f, 6.2f), Shadow);
                VoxelLine(a, o, wrist, palm, 4.4f, 3.7f, Body);
                FillEllipsoid(a, o, palm, new float3(9.5f, 4.6f, 9.2f), Body);

                // Four compact splayed fingers, with the center pair slightly longer.
                float[] lateral = { -7.2f, -2.4f, 2.4f, 7.2f };
                for (int i = 0; i < lateral.Length; i++)
                {
                    float spread = lateral[i] * s;
                    float extra = (i == 1 || i == 2) ? 3.0f : 0f;
                    float3 knuckle = new float3(22*s + spread, 7.5f, -53);
                    float3 toe = new float3(22*s + spread + 1.2f*s, 4.4f, -63 - extra);
                    float3 claw = new float3(22*s + spread + 3.3f*s, 1.4f, -73 - extra);
                    VoxelLine(a, o, knuckle, toe, 2.45f, 1.45f, Body);
                    VoxelLine(a, o, toe, claw, 1.45f, .12f, Armor);
                }

                // Elbow spur echoes the reference limb armor and sharpens the bend silhouette.
                VoxelLine(a, o, elbow + new float3(1.5f*s, 0, 1),
                    elbow + new float3(8.0f*s, 2.5f, 5), 1.8f, .12f, Horn);
            }
        }

        private static void RebuildRearDistalLimbs(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                // Tuck the hock and foot inward under the haunch instead of sprawling laterally.
                float3 knee = new float3(35*s, 27, 6);
                float3 hock = new float3(30*s, 15, -13);
                float3 foot = new float3(31*s, 7, -28);
                VoxelLine(a, o, knee, hock, 7.7f, 5.3f, Body);
                FillEllipsoid(a, o, hock, new float3(5.8f, 6.2f, 7.0f), Body);
                VoxelLine(a, o, hock, foot, 5.2f, 4.2f, Body);
                FillEllipsoid(a, o, foot, new float3(11.5f, 4.8f, 13.0f), Body);

                float[] lateral = { -7.3f, -2.4f, 2.4f, 7.3f };
                for (int i = 0; i < lateral.Length; i++)
                {
                    float spread = lateral[i] * s;
                    float extra = (i == 1 || i == 2) ? 2.5f : 0f;
                    float3 knuckle = new float3(31*s + spread, 6.4f, -32);
                    float3 toe = new float3(31*s + spread + .8f*s, 4.0f, -42 - extra);
                    float3 claw = new float3(31*s + spread + 2.5f*s, 1.4f, -52 - extra);
                    VoxelLine(a, o, knuckle, toe, 2.4f, 1.4f, Body);
                    VoxelLine(a, o, toe, claw, 1.4f, .12f, Armor);
                }
            }
        }

        private static void RebuildWings(IStructureAuthoringSession a, int3 o)
        {
            AuthorWing(a, o, 1, 1.0f, 0f);
            // Far wing stays clearly visible rather than being intentionally hidden behind the body.
            AuthorWing(a, o, -1, .88f, -4f);
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side, float scale, float zShift)
        {
            float s = side;
            float3 root = new float3(18*s, 74, 8 + zShift);
            float3 elbow = new float3(49*scale*s, 105, 15 + zShift);
            float3 wrist = new float3(82*scale*s, 132, 11 + zShift);

            // Four outer finger tips: fewer, broader bays than V7. These are the low points of the
            // trailing edge; rounded concave scallops are carved between them below.
            float3[] tips =
            {
                new float3(111*scale*s, 120, 2 + zShift),
                new float3(108*scale*s, 94, 0 + zShift),
                new float3(98*scale*s, 70, 0 + zShift),
                new float3(82*scale*s, 52, 4 + zShift),
                new float3(61*scale*s, 46, 9 + zShift),
            };

            // Broad membrane fan.
            FillTriangle(a, o, root, elbow, tips[4], 1.15f, Membrane);
            FillTriangle(a, o, elbow, wrist, tips[4], 1.05f, Membrane);
            for (int i = 0; i < tips.Length - 1; i++)
                FillTriangle(a, o, wrist, tips[i], tips[i + 1], 1.0f, Membrane);

            // Rounded scallops. Centers sit just outside/below each bay so the carve cuts a smooth U
            // into the trailing edge instead of leaving V7's hanging triangular teeth.
            for (int i = 0; i < tips.Length - 1; i++)
            {
                float3 mid = (tips[i] + tips[i + 1]) * .5f;
                float rx = math.lerp(9.5f, 7.0f, i / 3f) * scale;
                float ry = math.lerp(8.0f, 6.0f, i / 3f);
                CarveOval(a, o,
                    (int3)math.round(mid + new float3(-3.0f*s, -5.5f, 0)),
                    new int3(math.max(5, (int)math.round(rx)), math.max(4, (int)math.round(ry)), 5));
            }

            // Heavy arched leading edge followed by a hooked outer tip.
            VoxelLine(a, o, root, elbow, 6.7f, 4.8f, Body);
            VoxelLine(a, o, elbow, wrist, 4.8f, 3.0f, Body);
            float3 arch1 = new float3(104*scale*s, 145, 5 + zShift);
            float3 arch2 = new float3(116*scale*s, 143, -2 + zShift);
            float3 arch3 = new float3(120*scale*s, 133, -5 + zShift);
            VoxelLine(a, o, wrist, arch1, 3.0f, 2.0f, Armor);
            VoxelLine(a, o, arch1, arch2, 2.0f, 1.0f, Armor);
            VoxelLine(a, o, arch2, arch3, 1.0f, .12f, Horn);

            // Four curved fingers. Restore them after scallop carving so structural rays remain intact.
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float3 end = tips[i];
                float3 bend = math.lerp(wrist, end, .52f)
                    + new float3((6.0f - i*.8f)*scale*s, 4.0f - i*.7f, 1.6f);
                float r0 = math.lerp(2.5f, 1.7f, t);
                VoxelLine(a, o, wrist, bend, r0, r0*.62f, Body);
                VoxelLine(a, o, bend, end, r0*.62f, .18f, Armor);
            }

            // Subtle warm crease accents, deliberately sparse.
            VoxelLine(a, o, math.lerp(wrist, tips[1], .72f), tips[1], .55f, .14f, Horn);
            VoxelLine(a, o, math.lerp(wrist, tips[3], .76f), tips[3], .50f, .14f, Horn);
        }

        private static void RebuildVentralArmor(IStructureAuthoringSession a, int3 o)
        {
            // Restore a continuous front torso/neck surface after clearing V7 plates.
            VoxelLine(a, o, new float3(0, 111, -44), new float3(0, 79, -27), 10.0f, 15.0f, Body);
            VoxelLine(a, o, new float3(0, 79, -27), new float3(0, 45, -20), 15.0f, 18.0f, Body);

            // Six large light shields, with obvious vertical overlap and generous separation from one
            // another. This removes the striped/ribbed look in the V7 capture.
            Shield(a, o, new float3(-4, 104, -50), new float3(8.0f, 5.0f, 3.0f));
            Shield(a, o, new float3(-3, 92, -42), new float3(10.0f, 5.8f, 3.2f));
            Shield(a, o, new float3(-1, 80, -34), new float3(12.0f, 6.3f, 3.4f));
            Shield(a, o, new float3(0, 67, -28), new float3(14.0f, 6.8f, 3.6f));
            Shield(a, o, new float3(0, 54, -24), new float3(15.5f, 7.0f, 3.8f));
            Shield(a, o, new float3(0, 42, -21), new float3(14.0f, 6.3f, 3.6f));
        }

        private static void Shield(IStructureAuthoringSession a, int3 o, float3 c, float3 r)
        {
            FillEllipsoid(a, o, c, r, Armor);
            VoxelLine(a, o,
                c + new float3(0, -1.5f, -2.3f),
                c + new float3(0, -r.y - 2.8f, -3.0f),
                math.max(1.8f, r.x*.24f), .14f, Armor);
        }

        private static void RebuildTail(IStructureAuthoringSession a, int3 o)
        {
            // Slimmer open sweep. It supports the composition without becoming a giant smooth ring.
            float3[] p =
            {
                new float3(15,37,39),
                new float3(42,29,47),
                new float3(67,20,44),
                new float3(87,13,31),
                new float3(98,8,12),
                new float3(95,6,-8),
                new float3(85,5,-27),
                new float3(71,4,-43),
                new float3(56,3,-57),
                new float3(43,3,-66),
            };
            float[] r = { 15.5f, 13.5f, 11.2f, 9.0f, 7.0f, 5.5f, 4.1f, 3.0f, 1.7f, .22f };
            for (int i = 0; i < p.Length - 1; i++)
                VoxelLine(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Overlapping dorsal tail plates rather than isolated spikes. They make the tail feel
            // armored like the reference and break the snake-smooth V7 surface.
            for (int i = 2; i < p.Length - 2; i++)
            {
                float t = (i - 2) / (float)(p.Length - 5);
                float3 baseP = p[i] + new float3(0, math.lerp(5.5f, 2.0f, t), -1.0f);
                FillEllipsoid(a, o, baseP,
                    new float3(math.lerp(4.2f, 2.0f, t), 1.8f, math.lerp(3.0f, 1.5f, t)), Armor);
            }

            // One connected blade tip. No forked free fragment.
            float3 tip = p[p.Length - 1];
            VoxelLine(a, o, tip, tip + new float3(-13, 3, -4), 1.8f, .12f, Armor);
            VoxelLine(a, o, tip + new float3(-5, 1, -1), tip + new float3(-10, 6, 2), 1.3f, .12f, Horn);
        }

        private static void AddSilhouetteAccents(IStructureAuthoringSession a, int3 o)
        {
            // Neck crest transitions from head into shoulder with broad, swept fins.
            float3[] crest =
            {
                new float3(-4, 115, -45), new float3(-2, 104, -36),
                new float3(0, 93, -27), new float3(1, 82, -17), new float3(0, 72, -7),
            };
            for (int i = 0; i < crest.Length; i++)
            {
                float h = math.lerp(8.0f, 5.0f, i/(float)(crest.Length - 1));
                VoxelLine(a, o, crest[i], crest[i] + new float3(0, h, 4), 1.7f, .12f, Horn);
            }

            // Moss stays sparse and sheltered after the large-form rebuild.
            FillEllipsoid(a, o, new float3(9, 93, -30), new float3(2.7f, 1.5f, 2.0f), Moss);
            FillEllipsoid(a, o, new float3(18, 67, -6), new float3(3.0f, 1.5f, 2.2f), Moss);
            FillEllipsoid(a, o, new float3(68, 21, 43), new float3(2.8f, 1.4f, 2.2f), Moss);
        }

        private static void FillRun(IStructureAuthoringSession a, int3 o, int x0, int x1, int y, int z, byte material)
        {
            if (x0 > x1) (x0, x1) = (x1, x0);
            for (int x = x0; x <= x1; x++) a.Set(o.x + x, o.y + y, o.z + z, material);
        }

        private static void FillOvalSliceXY(IStructureAuthoringSession a, int3 o, int cx, int cy, int z, int rx, int ry, byte material)
        {
            float sx = math.max(1, rx);
            float sy = math.max(1, ry);
            for (int y = cy - ry; y <= cy + ry; y++)
            for (int x = cx - rx; x <= cx + rx; x++)
            {
                float dx = (x + .5f - cx) / sx;
                float dy = (y + .5f - cy) / sy;
                if (dx*dx + dy*dy <= 1f) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void FillEllipsoid(IStructureAuthoringSession a, int3 o, float3 c, float3 r, byte material)
        {
            int3 min = (int3)math.floor(c - r - 1);
            int3 max = (int3)math.ceil(c + r + 1);
            float3 safe = math.max(r, new float3(.5f));
            for (int y = min.y; y <= max.y; y++)
            for (int z = min.z; z <= max.z; z++)
            for (int x = min.x; x <= max.x; x++)
            {
                float3 q = (new float3(x + .5f, y + .5f, z + .5f) - c) / safe;
                if (math.dot(q, q) <= 1f) a.Set(o.x + x, o.y + y, o.z + z, material);
            }
        }

        private static void VoxelLine(IStructureAuthoringSession a, int3 o, float3 p0, float3 p1, float r0, float r1, byte material)
        {
            float len = math.length(p1 - p0);
            int steps = math.max(1, (int)math.ceil(len*1.5f));
            for (int i = 0; i <= steps; i++)
            {
                float t = i/(float)steps;
                float3 p = math.lerp(p0, p1, t);
                float r = math.lerp(r0, r1, t);
                for (int y = (int)math.floor(p.y-r); y <= (int)math.ceil(p.y+r); y++)
                for (int z = (int)math.floor(p.z-r); z <= (int)math.ceil(p.z+r); z++)
                for (int x = (int)math.floor(p.x-r); x <= (int)math.ceil(p.x+r); x++)
                {
                    float3 d = new float3(x+.5f, y+.5f, z+.5f) - p;
                    if (math.dot(d,d) <= r*r) a.Set(o.x+x, o.y+y, o.z+z, material);
                }
            }
        }

        private static void FillTriangle(IStructureAuthoringSession a, int3 o, float3 va, float3 vb, float3 vc, float thick, byte material)
        {
            float3 n = math.normalizesafe(math.cross(vb-va, vc-va), new float3(0,0,1));
            int3 min = (int3)math.floor(math.min(va, math.min(vb,vc)) - thick - 1);
            int3 max = (int3)math.ceil(math.max(va, math.max(vb,vc)) + thick + 1);
            float3 v0 = vb-va;
            float3 v1 = vc-va;
            float d00 = math.dot(v0,v0);
            float d01 = math.dot(v0,v1);
            float d11 = math.dot(v1,v1);
            float den = d00*d11 - d01*d01;
            if (math.abs(den) < .0001f) return;
            for (int y=min.y; y<=max.y; y++)
            for (int z=min.z; z<=max.z; z++)
            for (int x=min.x; x<=max.x; x++)
            {
                float3 p = new float3(x+.5f,y+.5f,z+.5f);
                float dist = math.dot(p-va,n);
                if (math.abs(dist)>thick) continue;
                float3 v2 = (p-n*dist)-va;
                float d20=math.dot(v2,v0);
                float d21=math.dot(v2,v1);
                float v=(d11*d20-d01*d21)/den;
                float w=(d00*d21-d01*d20)/den;
                float u=1-v-w;
                if (u>=0 && v>=0 && w>=0) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void CarveOval(IStructureAuthoringSession a, int3 o, int3 c, int3 r)
        {
            for (int y=c.y-r.y; y<=c.y+r.y; y++)
            for (int z=c.z-r.z; z<=c.z+r.z; z++)
            for (int x=c.x-r.x; x<=c.x+r.x; x++)
            {
                float dx=(x+.5f-c.x)/math.max(1f,r.x);
                float dy=(y+.5f-c.y)/math.max(1f,r.y);
                float dz=(z+.5f-c.z)/math.max(1f,r.z);
                if (dx*dx+dy*dy+dz*dz<=1f) a.Set(o.x+x,o.y+y,o.z+z,Empty);
            }
        }

        private static void Box(IStructureAuthoringSession a, int3 o, int3 min, int3 size, byte material)
        {
            int3 max=min+size;
            for (int y=min.y; y<max.y; y++)
            for (int z=min.z; z<max.z; z++)
            for (int x=min.x; x<max.x; x++) a.Set(o.x+x,o.y+y,o.z+z,material);
        }

        private static void ClearBox(IStructureAuthoringSession a, int3 o, int3 min, int3 size)
        {
            Box(a,o,min,size,Empty);
        }
    }
}
