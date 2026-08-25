using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V9 is a clean reference-proportion rebuild. It does not inherit V7/V8 geometry. The entire
    /// statue is authored here from deterministic implicit volumes sampled into the canonical 10 cm
    /// voxel grid. Primary goals: crouched mass, articulated compact limbs, integrated dragon crown,
    /// depth-swept wings with shallow curved scallops, broad ventral shields and a slimmer armored tail.
    /// </summary>
    public static class DragonStatueConceptV9ReferenceAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Armor = GameMaterialIds.Dirt;
        private const byte Horn = GameMaterialIds.Stone;
        private const byte Membrane = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            AuthorBody(a, o);
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
            AuthorCrest(a, o);
            AuthorScales(a, o);
            AuthorPatina(a, o);
        }

        private static void AuthorBody(IStructureAuthoringSession a, int3 o)
        {
            // Crouched, diagonal mass rather than an upright cylinder.
            FillEllipsoid(a, o, new float3(0, 32, 22), new float3(30, 19, 31), Body);
            FillEllipsoid(a, o, new float3(0, 47, 5), new float3(27, 21, 27), Body);
            FillEllipsoid(a, o, new float3(0, 61, -11), new float3(23, 20, 22), Body);

            // Large seated haunches, tucked into the pelvis.
            FillEllipsoid(a, o, new float3(-27, 28, 22), new float3(19, 17, 21), Body);
            FillEllipsoid(a, o, new float3(27, 28, 22), new float3(19, 17, 21), Body);

            // Shoulder caps establish a broad upper silhouette without extending the arms vertically.
            FillEllipsoid(a, o, new float3(-19, 61, -17), new float3(12, 13, 12), Body);
            FillEllipsoid(a, o, new float3(19, 61, -17), new float3(12, 13, 12), Body);
        }

        private static void AuthorNeck(IStructureAuthoringSession a, int3 o)
        {
            // Graceful S-neck with a substantial shoulder transition and a smaller head socket.
            VoxelLine(a, o, new float3(0, 62, -15), new float3(-1, 76, -25), 15.5f, 13.5f, Body);
            VoxelLine(a, o, new float3(-1, 75, -25), new float3(1, 89, -34), 13.5f, 11.8f, Body);
            VoxelLine(a, o, new float3(1, 88, -34), new float3(-2, 102, -45), 11.8f, 10.0f, Body);
            VoxelLine(a, o, new float3(-2, 101, -45), new float3(-5, 114, -56), 10.0f, 8.4f, Body);

            // Side muscle planes stop the neck reading as a smooth pipe.
            float3[] c =
            {
                new float3(0, 75, -25), new float3(0, 88, -34),
                new float3(-2, 100, -44), new float3(-5, 111, -53)
            };
            float[] lateral = { 11.0f, 9.5f, 8.0f, 6.8f };
            for (int i = 0; i < c.Length; i++)
            {
                FillEllipsoid(a, o, c[i] + new float3(-lateral[i], 0, 1), new float3(4.0f, 5.5f, 4.2f), Body);
                FillEllipsoid(a, o, c[i] + new float3(lateral[i], 0, 1), new float3(4.0f, 5.5f, 4.2f), Body);
            }
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Low angular skull with strong cheek roots.
            FillEllipsoid(a, o, new float3(-5, 120, -65), new float3(17.5f, 12.0f, 14.0f), Body);
            FillEllipsoid(a, o, new float3(-17, 114, -66), new float3(8.0f, 8.5f, 8.5f), Body);
            FillEllipsoid(a, o, new float3(7, 114, -66), new float3(8.0f, 8.5f, 8.5f), Body);

            // Wedge muzzle widens gradually into the cranium.
            for (int z = -93; z <= -69; z++)
            {
                float t = (z + 93) / 24f;
                int rx = (int)math.round(math.lerp(5.2f, 11.5f, t));
                int ry = (int)math.round(math.lerp(2.8f, 5.5f, t));
                int cy = (int)math.round(math.lerp(117f, 120f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, math.max(2, ry), Body);
            }
            FillEllipsoid(a, o, new float3(-5, 118, -91), new float3(6.0f, 3.5f, 3.5f), Body);
            Box(a, o, new int3(-10, 118, -95), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 118, -95), new int3(3, 2, 3), Empty);

            // Open mouth with a slim attached lower jaw.
            for (int z = -89; z <= -68; z++)
            {
                float t = (z + 89) / 21f;
                int half = (int)math.round(math.lerp(4.0f, 8.6f, t));
                for (int y = 108; y <= 114; y++)
                    FillRun(a, o, -5 - half, -5 + half, y, z, Empty);
            }
            for (int z = -89; z <= -65; z++)
            {
                float t = (z + 89) / 24f;
                int rx = (int)math.round(math.lerp(4.5f, 10.0f, t));
                int ry = (int)math.round(math.lerp(2.0f, 3.6f, t));
                int cy = (int)math.round(math.lerp(103f, 108f, t));
                FillOvalSliceXY(a, o, -5, cy, z, rx, math.max(2, ry), Shadow);
            }
            FillEllipsoid(a, o, new float3(-15, 109, -64), new float3(6.5f, 6.0f, 7.0f), Body);
            FillEllipsoid(a, o, new float3(5, 109, -64), new float3(6.5f, 6.0f, 7.0f), Body);

            // Brow plates and recessed eyes.
            VoxelLine(a, o, new float3(-5, 129, -72), new float3(-19, 127, -64), 4.0f, 1.5f, Shadow);
            VoxelLine(a, o, new float3(-5, 129, -72), new float3(9, 127, -64), 4.0f, 1.5f, Shadow);
            CarveOval(a, o, new int3(-14, 122, -73), new int3(3, 3, 3));
            CarveOval(a, o, new int3(4, 122, -73), new int3(3, 3, 3));
            FillEllipsoid(a, o, new float3(-14, 122, -75), new float3(1.8f, 1.4f, 1.4f), Eye);
            FillEllipsoid(a, o, new float3(4, 122, -75), new float3(1.8f, 1.4f, 1.4f), Eye);

            // Main horns are shorter, thicker and integrated by crown plates. They rise, sweep back,
            // and hook slightly downward at the tips instead of reading as isolated goat horns.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                FillEllipsoid(a, o, new float3(-5 + 8*s, 131, -57), new float3(6.0f, 4.0f, 5.0f), Armor);
                float3 h0 = new float3(-5 + 9*s, 132, -56);
                float3 h1 = new float3(-5 + 13*s, 141, -49);
                float3 h2 = new float3(-5 + 16*s, 147, -39);
                float3 h3 = new float3(-5 + 19*s, 149, -29);
                float3 h4 = new float3(-5 + 21*s, 145, -20);
                VoxelLine(a, o, h0, h1, 4.2f, 3.3f, Horn);
                VoxelLine(a, o, h1, h2, 3.3f, 2.3f, Horn);
                VoxelLine(a, o, h2, h3, 2.3f, 1.25f, Horn);
                VoxelLine(a, o, h3, h4, 1.25f, .13f, Horn);

                // Layered temple and cheek fins.
                VoxelLine(a, o, new float3(-5 + 12*s, 126, -61), new float3(-5 + 23*s, 131, -50), 2.3f, .13f, Horn);
                VoxelLine(a, o, new float3(-5 + 14*s, 118, -66), new float3(-5 + 26*s, 120, -56), 1.9f, .13f, Horn);
                VoxelLine(a, o, new float3(-5 + 13*s, 111, -63), new float3(-5 + 21*s, 108, -53), 1.5f, .12f, Horn);
            }

            // Layered crown plates and nasal ridge make the skull read armored at thumbnail scale.
            FillEllipsoid(a, o, new float3(-5, 132, -64), new float3(7.5f, 3.0f, 5.5f), Armor);
            FillEllipsoid(a, o, new float3(-5, 130, -54), new float3(6.0f, 2.6f, 4.0f), Armor);
            FillEllipsoid(a, o, new float3(-5, 124, -80), new float3(5.0f, 2.2f, 5.0f), Armor);

            // Sparse teeth, leaving a large readable mouth void.
            int[] toothZ = { -72, -79, -86 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -5 + side * (4.6f + i * 1.0f);
                    float len = i == 1 ? 5.2f : 4.0f;
                    VoxelLine(a, o, new float3(x, 115, toothZ[i]),
                        new float3(x + .25f*side, 115-len, toothZ[i]-1), 1.0f, .12f, Horn);
                }
            }
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(20*s, 61, -18);
            float3 elbow = new float3(29*s, 44, -18);
            float3 wrist = new float3(19*s, 26, -37);
            float3 palm = new float3(20*s, 8, -48);

            // Compact upper arm, strong elbow, shorter forearm.
            VoxelLine(a, o, shoulder, elbow, 8.8f, 6.5f, Body);
            FillEllipsoid(a, o, elbow, new float3(7.0f, 7.2f, 7.8f), Body);
            VoxelLine(a, o, elbow, wrist, 6.3f, 4.4f, Body);
            FillEllipsoid(a, o, wrist, new float3(4.8f, 5.0f, 5.8f), Shadow);
            VoxelLine(a, o, wrist, palm, 4.3f, 3.6f, Body);
            FillEllipsoid(a, o, palm, new float3(9.0f, 4.5f, 9.0f), Body);

            float[] offsets = { -6.8f, -2.3f, 2.3f, 6.8f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 2.5f : 0f;
                float x = 20*s + offsets[i]*s;
                float3 knuckle = new float3(x, 7.2f, -52);
                float3 toe = new float3(x + 1.0f*s, 4.2f, -61 - extra);
                float3 claw = new float3(x + 3.0f*s, 1.3f, -70 - extra);
                VoxelLine(a, o, knuckle, toe, 2.3f, 1.35f, Body);
                VoxelLine(a, o, toe, claw, 1.35f, .12f, Horn);
            }

            VoxelLine(a, o, elbow + new float3(1.0f*s,0,1), elbow + new float3(7*s,2,5), 1.6f, .12f, Horn);
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(26*s, 29, 21);
            float3 knee = new float3(38*s, 23, 5);
            float3 hock = new float3(30*s, 13, -13);
            float3 foot = new float3(30*s, 7, -27);

            FillEllipsoid(a, o, hip, new float3(17.5f, 16.0f, 19.5f), Body);
            VoxelLine(a, o, hip, knee, 11.0f, 7.5f, Body);
            FillEllipsoid(a, o, knee, new float3(8.5f, 8.0f, 9.5f), Body);
            VoxelLine(a, o, knee, hock, 7.2f, 5.0f, Body);
            FillEllipsoid(a, o, hock, new float3(5.7f, 6.0f, 6.8f), Body);
            VoxelLine(a, o, hock, foot, 5.0f, 4.0f, Body);
            FillEllipsoid(a, o, foot, new float3(11.0f, 4.6f, 12.5f), Body);

            float[] offsets = { -7.0f, -2.3f, 2.3f, 7.0f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 2.0f : 0f;
                float x = 30*s + offsets[i]*s;
                float3 knuckle = new float3(x, 6.3f, -31);
                float3 toe = new float3(x + .8f*s, 3.9f, -40 - extra);
                float3 claw = new float3(x + 2.5f*s, 1.3f, -49 - extra);
                VoxelLine(a, o, knuckle, toe, 2.3f, 1.3f, Body);
                VoxelLine(a, o, toe, claw, 1.3f, .12f, Horn);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            // Strong depth sweep: the wing moves backward (+Z) as it moves outward. This prevents
            // the vertical cardboard-panel read visible in V7/V8.
            float3 root = new float3(16*s, 68, 7);
            float3 elbow = new float3(48*s, 100, 20);
            float3 wrist = new float3(84*s, 132, 31);
            float3 arch1 = new float3(108*s, 145, 36);
            float3 arch2 = new float3(122*s, 143, 33);
            float3 hook = new float3(125*s, 135, 28);

            float3[] tips =
            {
                new float3(118*s, 117, 30),
                new float3(114*s, 91, 28),
                new float3(104*s, 68, 24),
                new float3(88*s, 50, 18),
                new float3(66*s, 43, 13),
            };

            // Inner/back membrane.
            FillTriangle(a, o, root, elbow, tips[4], 1.15f, Membrane);
            FillTriangle(a, o, elbow, wrist, tips[4], 1.05f, Membrane);

            // Four shallow curved scallop bays. Each bay is a fan over a 5-point concave arc; this
            // keeps broad membrane around the finger tips and avoids dangling-wire exposure.
            for (int i = 0; i < tips.Length - 1; i++)
                FillScallopedBay(a, o, wrist, tips[i], tips[i + 1], i);

            // Top membrane reaches into the hooked leading edge.
            FillTriangle(a, o, wrist, arch2, tips[0], 1.0f, Membrane);

            // Arched leading arm.
            VoxelLine(a, o, root, elbow, 6.5f, 4.7f, Body);
            VoxelLine(a, o, elbow, wrist, 4.7f, 3.0f, Body);
            VoxelLine(a, o, wrist, arch1, 3.0f, 2.0f, Armor);
            VoxelLine(a, o, arch1, arch2, 2.0f, 1.0f, Armor);
            VoxelLine(a, o, arch2, hook, 1.0f, .12f, Horn);

            // Curved structural fingers terminate at the actual membrane low points.
            for (int i = 0; i < 4; i++)
            {
                float t = i / 3f;
                float3 end = tips[i];
                float3 bend = math.lerp(wrist, end, .52f) + new float3((5.0f-i*.6f)*s, 3.0f-i*.5f, 3.5f);
                float r0 = math.lerp(2.4f, 1.6f, t);
                VoxelLine(a, o, wrist, bend, r0, r0*.62f, Body);
                VoxelLine(a, o, bend, end, r0*.62f, .16f, Armor);
            }

            // Broad scale caps along the leading edge reinforce the sculpted-stone reference language.
            FillEllipsoid(a, o, math.lerp(elbow, wrist, .30f), new float3(4.0f, 2.3f, 2.8f), Armor);
            FillEllipsoid(a, o, math.lerp(elbow, wrist, .52f), new float3(3.6f, 2.1f, 2.6f), Armor);
            FillEllipsoid(a, o, math.lerp(elbow, wrist, .72f), new float3(3.1f, 1.9f, 2.3f), Armor);
        }

        private static void FillScallopedBay(IStructureAuthoringSession a, int3 o, float3 wrist, float3 tipA, float3 tipB, int bay)
        {
            float3 mid = (tipA + tipB) * .5f;
            float depth = math.lerp(.86f, .90f, bay / 3f);
            float3 notch = math.lerp(wrist, mid, depth);
            // Shift notch slightly upward/inward for a smooth shallow U rather than a sharp V.
            notch += new float3(0, 2.0f, -1.0f);
            float3 q1 = math.lerp(tipA, notch, .52f);
            float3 q2 = notch;
            float3 q3 = math.lerp(notch, tipB, .52f);
            float3[] edge = { tipA, q1, q2, q3, tipB };
            for (int i = 0; i < edge.Length - 1; i++)
                FillTriangle(a, o, wrist, edge[i], edge[i + 1], 1.0f, Membrane);
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Slim open foreground sweep, substantially smaller than V7/V8 and never a closed ring.
            float3[] p =
            {
                new float3(14,34,39), new float3(39,27,47), new float3(63,19,44),
                new float3(84,12,32), new float3(98,7,15), new float3(98,5,-4),
                new float3(90,4,-22), new float3(78,3,-37), new float3(64,2.5f,-50),
                new float3(50,2.2f,-60), new float3(38,2.0f,-67)
            };
            float[] r = { 14.0f,12.2f,10.2f,8.2f,6.5f,5.1f,4.0f,3.0f,2.1f,1.3f,.18f };
            for (int i = 0; i < p.Length - 1; i++)
                VoxelLine(a, o, p[i], p[i + 1], r[i], r[i + 1], Body);

            // Overlapping dorsal armor follows the curve.
            for (int i = 2; i < p.Length - 2; i++)
            {
                float t = (i - 2) / (float)(p.Length - 5);
                FillEllipsoid(a, o, p[i] + new float3(0, math.lerp(4.8f,1.5f,t), -1),
                    new float3(math.lerp(3.8f,1.7f,t), 1.6f, math.lerp(2.7f,1.3f,t)), Armor);
            }

            // Single connected blade tip.
            float3 tip = p[p.Length - 1];
            VoxelLine(a, o, tip, tip + new float3(-12, 3, -4), 1.5f, .12f, Horn);
        }

        private static void AuthorVentralArmor(IStructureAuthoringSession a, int3 o)
        {
            Shield(a, o, new float3(-4, 101, -53), new float3(7.5f,4.8f,3.0f));
            Shield(a, o, new float3(-3, 89, -45), new float3(9.5f,5.5f,3.2f));
            Shield(a, o, new float3(-1, 77, -37), new float3(11.5f,6.0f,3.4f));
            Shield(a, o, new float3(0, 64, -31), new float3(13.5f,6.5f,3.6f));
            Shield(a, o, new float3(0, 51, -27), new float3(14.5f,6.7f,3.8f));
            Shield(a, o, new float3(0, 39, -24), new float3(13.0f,5.8f,3.5f));
        }

        private static void Shield(IStructureAuthoringSession a, int3 o, float3 c, float3 r)
        {
            FillEllipsoid(a, o, c, r, Armor);
            VoxelLine(a, o, c + new float3(0,-1.4f,-2.2f),
                c + new float3(0,-r.y-2.5f,-3.0f), math.max(1.7f,r.x*.22f), .12f, Armor);
        }

        private static void AuthorCrest(IStructureAuthoringSession a, int3 o)
        {
            float3[] crest =
            {
                new float3(-5,119,-54), new float3(-3,108,-45), new float3(-1,97,-36),
                new float3(0,86,-27), new float3(0,75,-18), new float3(0,64,-8),
                new float3(0,53,3), new float3(0,42,14)
            };
            for (int i = 0; i < crest.Length; i++)
            {
                float t = i/(float)(crest.Length-1);
                float h = math.lerp(7.5f,4.5f,t);
                VoxelLine(a, o, crest[i], crest[i]+new float3(0,h,4.0f), math.lerp(1.8f,1.3f,t), .12f, Horn);
            }
        }

        private static void AuthorScales(IStructureAuthoringSession a, int3 o)
        {
            // Large overlapping scale islands on neck, shoulder and haunch. Same body material means
            // lighting carries the detail rather than checkerboard color noise.
            for (int side=-1; side<=1; side+=2)
            {
                float s=side;
                float3[] neck =
                {
                    new float3(11*s,82,-31), new float3(10*s,89,-36), new float3(9*s,96,-42),
                    new float3(8*s,103,-48), new float3(7*s,110,-54)
                };
                for (int i=0;i<neck.Length;i++)
                    FillEllipsoid(a,o,neck[i],new float3(3.5f,2.6f,1.6f),Body);

                float3[] shoulder =
                {
                    new float3(22*s,67,-18),new float3(27*s,62,-14),new float3(29*s,56,-9),
                    new float3(31*s,49,-2)
                };
                for(int i=0;i<shoulder.Length;i++)
                    FillEllipsoid(a,o,shoulder[i],new float3(3.7f,2.7f,1.7f),Body);

                float3[] haunch =
                {
                    new float3(33*s,38,17),new float3(38*s,32,20),new float3(39*s,25,19),
                    new float3(37*s,20,14)
                };
                for(int i=0;i<haunch.Length;i++)
                    FillEllipsoid(a,o,haunch[i],new float3(4.0f,2.9f,1.8f),Body);
            }

            // Dark creases under major joints.
            FillEllipsoid(a,o,new float3(-27,43,-20),new float3(3.5f,2.3f,2.0f),Shadow);
            FillEllipsoid(a,o,new float3(27,43,-20),new float3(3.5f,2.3f,2.0f),Shadow);
            FillEllipsoid(a,o,new float3(-31,17,-12),new float3(3.2f,2.0f,1.8f),Shadow);
            FillEllipsoid(a,o,new float3(31,17,-12),new float3(3.2f,2.0f,1.8f),Shadow);
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            FillEllipsoid(a,o,new float3(9,94,-39),new float3(2.8f,1.5f,2.0f),Moss);
            FillEllipsoid(a,o,new float3(18,65,-7),new float3(3.0f,1.5f,2.2f),Moss);
            FillEllipsoid(a,o,new float3(31,34,30),new float3(3.4f,1.6f,2.5f),Moss);
            FillEllipsoid(a,o,new float3(80,13,30),new float3(2.5f,1.3f,2.0f),Moss);
            FillEllipsoid(a,o,new float3(74,115,27),new float3(2.4f,1.2f,1.8f),Moss);
        }

        private static void FillRun(IStructureAuthoringSession a,int3 o,int x0,int x1,int y,int z,byte material)
        {
            if(x0>x1)(x0,x1)=(x1,x0);
            for(int x=x0;x<=x1;x++)a.Set(o.x+x,o.y+y,o.z+z,material);
        }

        private static void FillOvalSliceXY(IStructureAuthoringSession a,int3 o,int cx,int cy,int z,int rx,int ry,byte material)
        {
            float sx=math.max(1,rx),sy=math.max(1,ry);
            for(int y=cy-ry;y<=cy+ry;y++)
            for(int x=cx-rx;x<=cx+rx;x++)
            {
                float dx=(x+.5f-cx)/sx,dy=(y+.5f-cy)/sy;
                if(dx*dx+dy*dy<=1f)a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void FillEllipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte material)
        {
            int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1);
            float3 safe=math.max(r,new float3(.5f));
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;
                if(math.dot(q,q)<=1f)a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void VoxelLine(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float len=math.length(p1-p0);
            int steps=math.max(1,(int)math.ceil(len*1.5f));
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps;
                float3 p=math.lerp(p0,p1,t);
                float r=math.lerp(r0,r1,t);
                for(int y=(int)math.floor(p.y-r);y<=(int)math.ceil(p.y+r);y++)
                for(int z=(int)math.floor(p.z-r);z<=(int)math.ceil(p.z+r);z++)
                for(int x=(int)math.floor(p.x-r);x<=(int)math.ceil(p.x+r);x++)
                {
                    float3 d=new float3(x+.5f,y+.5f,z+.5f)-p;
                    if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,material);
                }
            }
        }

        private static void FillTriangle(IStructureAuthoringSession a,int3 o,float3 va,float3 vb,float3 vc,float thick,byte material)
        {
            float3 n=math.normalizesafe(math.cross(vb-va,vc-va),new float3(0,0,1));
            int3 min=(int3)math.floor(math.min(va,math.min(vb,vc))-thick-1);
            int3 max=(int3)math.ceil(math.max(va,math.max(vb,vc))+thick+1);
            float3 v0=vb-va,v1=vc-va;
            float d00=math.dot(v0,v0),d01=math.dot(v0,v1),d11=math.dot(v1,v1);
            float den=d00*d11-d01*d01;
            if(math.abs(den)<.0001f)return;
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 p=new float3(x+.5f,y+.5f,z+.5f);
                float dist=math.dot(p-va,n);
                if(math.abs(dist)>thick)continue;
                float3 v2=(p-n*dist)-va;
                float d20=math.dot(v2,v0),d21=math.dot(v2,v1);
                float v=(d11*d20-d01*d21)/den,w=(d00*d21-d01*d20)/den,u=1-v-w;
                if(u>=0&&v>=0&&w>=0)a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void CarveOval(IStructureAuthoringSession a,int3 o,int3 c,int3 r)
        {
            for(int y=c.y-r.y;y<=c.y+r.y;y++)
            for(int z=c.z-r.z;z<=c.z+r.z;z++)
            for(int x=c.x-r.x;x<=c.x+r.x;x++)
            {
                float dx=(x+.5f-c.x)/math.max(1f,r.x),dy=(y+.5f-c.y)/math.max(1f,r.y),dz=(z+.5f-c.z)/math.max(1f,r.z);
                if(dx*dx+dy*dy+dz*dz<=1f)a.Set(o.x+x,o.y+y,o.z+z,Empty);
            }
        }

        private static void Box(IStructureAuthoringSession a,int3 o,int3 min,int3 size,byte material)
        {
            int3 max=min+size;
            for(int y=min.y;y<max.y;y++)
            for(int z=min.z;z<max.z;z++)
            for(int x=min.x;x<max.x;x++)a.Set(o.x+x,o.y+y,o.z+z,material);
        }
    }
}
