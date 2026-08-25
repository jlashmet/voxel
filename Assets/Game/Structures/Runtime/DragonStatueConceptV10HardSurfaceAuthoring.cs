using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Clean V10 hero rebuild. Rounded volumes establish only the underlying anatomy. The visible
    /// form language is dominated by chamfered skull wedges, explicit overlapping armor, tapered
    /// claws/horns, and concave curved wing polygons. This intentionally avoids the soft-tube +
    /// planar-triangle vocabulary that kept V7-V9 looking procedural in production captures.
    /// </summary>
    public static class DragonStatueConceptV10HardSurfaceAuthoring
    {
        private const byte Empty = GameMaterialIds.Empty;
        private const byte Body = GameMaterialIds.Slate;
        private const byte Shadow = GameMaterialIds.DarkStone;
        private const byte Scale = GameMaterialIds.Stone;
        private const byte Armor = GameMaterialIds.Dirt;
        private const byte Membrane = GameMaterialIds.Wood;
        private const byte Moss = GameMaterialIds.Moss;
        private const byte Eye = GameMaterialIds.Gold;

        public static void Author(IStructureAuthoringSession a, int3 o)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));

            AuthorCoreAnatomy(a, o);
            AuthorNeck(a, o);
            AuthorHead(a, o);
            AuthorRearLeg(a, o, -1);
            AuthorRearLeg(a, o, 1);
            AuthorForeleg(a, o, -1);
            AuthorForeleg(a, o, 1);
            AuthorWing(a, o, 1, 1.0f, 7f);
            AuthorWing(a, o, -1, .82f, -13f);
            AuthorTail(a, o);
            AuthorVentralShields(a, o);
            AuthorNeckScaleRows(a, o);
            AuthorBodyScaleClusters(a, o);
            AuthorDorsalCrest(a, o);
            AuthorPatina(a, o);
        }

        private static void AuthorCoreAnatomy(IStructureAuthoringSession a, int3 o)
        {
            // Low diagonal body: pelvis is rear/high-Z; sternum rises forward toward -Z.
            Ellipsoid(a, o, new float3(0, 29, 22), new float3(29, 17, 29), Body);
            Ellipsoid(a, o, new float3(0, 43, 5), new float3(26, 19, 25), Body);
            Ellipsoid(a, o, new float3(0, 56, -12), new float3(22, 18, 20), Body);

            // Distinct seated haunches, partially buried into the pelvis instead of free spheres.
            Ellipsoid(a, o, new float3(-25, 27, 20), new float3(18, 16, 19), Body);
            Ellipsoid(a, o, new float3(25, 27, 20), new float3(18, 16, 19), Body);

            // Shoulder armor sockets.
            Ellipsoid(a, o, new float3(-18, 57, -18), new float3(11, 12, 11), Body);
            Ellipsoid(a, o, new float3(18, 57, -18), new float3(11, 12, 11), Body);

            // Underside shadow separates ribcage from abdomen in the hero view.
            Ellipsoid(a, o, new float3(0, 35, -11), new float3(17, 12, 10), Shadow);
        }

        private static void AuthorNeck(IStructureAuthoringSession a, int3 o)
        {
            // S-curve with a visible forward/backward change of direction.
            Capsule(a, o, new float3(0, 57, -15), new float3(-2, 70, -25), 14.5f, 12.5f, Body);
            Capsule(a, o, new float3(-2, 69, -25), new float3(1, 83, -35), 12.5f, 10.8f, Body);
            Capsule(a, o, new float3(1, 82, -35), new float3(-3, 97, -47), 10.8f, 9.2f, Body);
            Capsule(a, o, new float3(-3, 96, -47), new float3(-5, 110, -58), 9.2f, 7.8f, Body);

            // Hard side planes break the silhouette and give scale rows a substrate.
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                float y = math.lerp(68f, 108f, t);
                float z = math.lerp(-27f, -57f, t);
                float x = math.lerp(10.5f, 6.5f, t);
                ChamferedBox(a, o, new float3(-x, y, z), new float3(4.2f, 5.0f, 4.0f), Body);
                ChamferedBox(a, o, new float3(x, y, z), new float3(4.2f, 5.0f, 4.0f), Body);
            }
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Angular rear cranium. Width grows toward the rear and the top plane rises slightly.
            ChamferedPrismZ(a, o, -5, 118, -72, -55,
                13f, 17f, 7.0f, 10.5f, Body);
            ChamferedBox(a, o, new float3(-17, 113, -63), new float3(7.5f, 8.0f, 8.0f), Body);
            ChamferedBox(a, o, new float3(7, 113, -63), new float3(7.5f, 8.0f, 8.0f), Body);

            // Low tapered muzzle built from chamfered slices, not ellipsoids.
            ChamferedPrismZ(a, o, -5, 116, -96, -71,
                4.8f, 12.0f, 2.8f, 5.7f, Body);
            ChamferedBox(a, o, new float3(-5, 116, -93), new float3(5.5f, 3.2f, 3.4f), Body);

            // Flat nasal armor and two true nostril cavities.
            ShieldPlate(a, o, new float3(-5, 122, -82), 6.0f, 5.0f, 2.3f, Scale);
            Box(a, o, new int3(-10, 116, -98), new int3(3, 2, 3), Empty);
            Box(a, o, new int3(-3, 116, -98), new int3(3, 2, 3), Empty);

            // Deep open mouth. Rear hinge remains solid through cheek masses.
            ChamferedPrismZ(a, o, -5, 108, -91, -68,
                3.7f, 8.8f, 2.8f, 4.5f, Empty);
            Box(a, o, new int3(-10, 108, -87), new int3(11, 6, 17), Empty);

            // Slim angular lower jaw, visibly separate from the upper muzzle.
            ChamferedPrismZ(a, o, -5, 103, -91, -65,
                4.2f, 10.0f, 2.0f, 3.5f, Shadow);
            ChamferedBox(a, o, new float3(-15, 108, -64), new float3(6.5f, 6.0f, 6.5f), Body);
            ChamferedBox(a, o, new float3(5, 108, -64), new float3(6.5f, 6.0f, 6.5f), Body);

            // Strong brow wedges and small recessed eyes.
            HardSegment(a, o, new float3(-5, 128, -72), new float3(-19, 126, -63), 4.0f, 1.4f, Shadow);
            HardSegment(a, o, new float3(-5, 128, -72), new float3(9, 126, -63), 4.0f, 1.4f, Shadow);
            ChamferedBox(a, o, new float3(-14, 121, -73), new float3(3.2f, 2.6f, 2.5f), Empty);
            ChamferedBox(a, o, new float3(4, 121, -73), new float3(3.2f, 2.6f, 2.5f), Empty);
            Box(a, o, new int3(-15, 121, -76), new int3(3, 2, 2), Eye);
            Box(a, o, new int3(3, 121, -76), new int3(3, 2, 2), Eye);

            // Crown base is a stack of plates so the horns emerge from armor rather than the skull as
            // two isolated goat tubes.
            ShieldPlate(a, o, new float3(-5, 132, -62), 9.0f, 5.0f, 3.0f, Scale);
            ShieldPlate(a, o, new float3(-5, 133, -53), 7.5f, 4.5f, 2.8f, Scale);
            ShieldPlate(a, o, new float3(-5, 131, -45), 6.0f, 4.0f, 2.6f, Scale);

            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                // Body-colored roots visually integrate into the crown; only the terminal half is pale.
                float3 p0 = new float3(-5 + 8*s, 132, -56);
                float3 p1 = new float3(-5 + 11*s, 140, -49);
                float3 p2 = new float3(-5 + 14*s, 145, -40);
                float3 p3 = new float3(-5 + 17*s, 146, -31);
                float3 p4 = new float3(-5 + 19*s, 142, -23);
                HardSegment(a, o, p0, p1, 4.0f, 3.2f, Body);
                HardSegment(a, o, p1, p2, 3.2f, 2.3f, Body);
                HardSegment(a, o, p2, p3, 2.3f, 1.2f, Scale);
                HardSegment(a, o, p3, p4, 1.2f, .32f, Scale);

                // Layered temple fins and cheek spikes create the dragon crown silhouette.
                HardSegment(a, o, new float3(-5 + 12*s, 126, -62), new float3(-5 + 24*s, 130, -50), 2.5f, .30f, Scale);
                HardSegment(a, o, new float3(-5 + 14*s, 119, -66), new float3(-5 + 27*s, 121, -55), 2.1f, .28f, Scale);
                HardSegment(a, o, new float3(-5 + 13*s, 112, -63), new float3(-5 + 22*s, 108, -52), 1.7f, .26f, Scale);
            }

            // Individually rooted teeth. Larger root overlap prevents detached production fragments.
            int[] toothZ = { -73, -80, -87 };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < toothZ.Length; i++)
                {
                    float x = -5 + side * (4.5f + i * 1.0f);
                    float len = i == 1 ? 5.0f : 4.0f;
                    HardSegment(a, o, new float3(x, 114, toothZ[i]),
                        new float3(x + .25f * side, 114 - len, toothZ[i] - 1),
                        1.45f, .35f, Scale);
                }
            }

            // Additional top/cheek armor plates establish faceted rather than soft facial planes.
            ShieldPlate(a, o, new float3(-14, 126, -61), 5.0f, 4.0f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(4, 126, -61), 5.0f, 4.0f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(-18, 116, -65), 4.5f, 3.5f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(8, 116, -65), 4.5f, 3.5f, 2.0f, Scale);
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(18*s, 57, -19);
            float3 elbow = new float3(30*s, 40, -18);
            float3 wrist = new float3(16*s, 23, -36);
            float3 palm = new float3(17*s, 7, -47);

            Capsule(a, o, shoulder, elbow, 8.2f, 6.2f, Body);
            ChamferedBox(a, o, elbow, new float3(6.8f, 6.5f, 7.5f), Body);
            Capsule(a, o, elbow, wrist, 6.0f, 4.2f, Body);
            ChamferedBox(a, o, wrist, new float3(4.8f, 4.8f, 5.6f), Shadow);
            Capsule(a, o, wrist, palm, 4.1f, 3.5f, Body);
            ChamferedBox(a, o, new float3(17*s, 7, -50), new float3(8.5f, 4.2f, 8.5f), Body);

            // Shoulder/elbow plate hierarchy sharpens the joint read.
            ShieldPlate(a, o, new float3(22*s, 58, -27), 5.0f, 4.0f, 2.2f, Scale);
            ShieldPlate(a, o, new float3(30*s, 41, -25), 4.2f, 3.6f, 2.0f, Scale);
            HardSegment(a, o, elbow + new float3(2*s, 0, 1), elbow + new float3(8*s, 2, 5), 1.8f, .30f, Scale);

            float[] offsets = { -6.2f, -2.1f, 2.1f, 6.2f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 2.4f : 0f;
                float x = 17*s + offsets[i]*s;
                float3 knuckle = new float3(x, 6.8f, -52);
                float3 toe = new float3(x + 1.0f*s, 4.0f, -60 - extra);
                float3 claw = new float3(x + 3.0f*s, 1.2f, -69 - extra);
                HardSegment(a, o, knuckle, toe, 2.4f, 1.7f, Body);
                HardSegment(a, o, toe, claw, 1.8f, .32f, Scale);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(24*s, 28, 20);
            float3 knee = new float3(36*s, 20, 5);
            float3 hock = new float3(27*s, 12, -12);
            float3 foot = new float3(27*s, 6, -25);

            Ellipsoid(a, o, hip, new float3(16.5f, 15.0f, 18.0f), Body);
            Capsule(a, o, hip, knee, 10.0f, 7.0f, Body);
            ChamferedBox(a, o, knee, new float3(7.5f, 7.0f, 8.5f), Body);
            Capsule(a, o, knee, hock, 6.8f, 4.8f, Body);
            ChamferedBox(a, o, hock, new float3(5.2f, 5.5f, 6.5f), Body);
            Capsule(a, o, hock, foot, 4.8f, 3.8f, Body);
            ChamferedBox(a, o, foot, new float3(10.0f, 4.2f, 11.5f), Body);

            ShieldPlate(a, o, new float3(32*s, 28, -1), 5.0f, 4.0f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(35*s, 19, -5), 4.3f, 3.5f, 2.0f, Scale);

            float[] offsets = { -6.5f, -2.2f, 2.2f, 6.5f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 2.0f : 0f;
                float x = 27*s + offsets[i]*s;
                float3 knuckle = new float3(x, 5.8f, -29);
                float3 toe = new float3(x + .8f*s, 3.5f, -37 - extra);
                float3 claw = new float3(x + 2.6f*s, 1.2f, -46 - extra);
                HardSegment(a, o, knuckle, toe, 2.3f, 1.65f, Body);
                HardSegment(a, o, toe, claw, 1.7f, .32f, Scale);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side, float scale, float zOffset)
        {
            // The membrane is one curved concave polygon in wing-local (outward, Y) space. This gives
            // exact control over the bat silhouette and avoids a fan of coplanar triangular panels.
            float2[] outline =
            {
                new float2(16, 66),
                new float2(43, 98),
                new float2(80, 128),
                new float2(108, 143),
                new float2(121, 139),
                new float2(117, 116),
                new float2(102, 107),
                new float2(107, 91),
                new float2(86, 82),
                new float2(91, 68),
                new float2(68, 61),
                new float2(73, 51),
                new float2(51, 54),
                new float2(32, 57),
            };
            for (int i = 0; i < outline.Length; i++) outline[i] *= scale;

            FillCurvedWingPolygon(a, o, side, outline, zOffset, Membrane);

            float3 root = WingPoint(side, 16*scale, 66*scale, zOffset);
            float3 elbow = WingPoint(side, 43*scale, 98*scale, zOffset);
            float3 wrist = WingPoint(side, 80*scale, 128*scale, zOffset);
            float3 arch = WingPoint(side, 108*scale, 143*scale, zOffset);
            float3 hook = WingPoint(side, 121*scale, 139*scale, zOffset);

            // Heavy leading edge and hooked tip.
            HardSegment(a, o, root, elbow, 6.5f*scale, 4.8f*scale, Body);
            HardSegment(a, o, elbow, wrist, 4.8f*scale, 3.0f*scale, Body);
            HardSegment(a, o, wrist, arch, 3.0f*scale, 1.8f*scale, Scale);
            HardSegment(a, o, arch, hook, 1.8f*scale, .32f, Scale);

            // Four finger tips correspond to the outer lows between concave notches.
            float2[] finger2 =
            {
                new float2(117,116), new float2(107,91), new float2(91,68), new float2(73,51)
            };
            for (int i = 0; i < finger2.Length; i++)
            {
                float u = finger2[i].x * scale;
                float y = finger2[i].y * scale;
                float3 end = WingPoint(side, u, y, zOffset);
                float3 bend = math.lerp(wrist, end, .52f);
                bend += new float3((4.5f-i*.45f)*scale*side, 2.5f-i*.35f, 3.0f*scale);
                float r0 = math.lerp(2.5f, 1.7f, i/3f) * scale;
                HardSegment(a, o, wrist, bend, r0, r0*.65f, Body);
                HardSegment(a, o, bend, end, r0*.65f, .28f, Scale);
            }

            // Leading-edge armor cadence hides the procedural spar and gives a sculpted statue read.
            for (int i = 1; i <= 7; i++)
            {
                float t = i/8f;
                float3 p = t < .45f
                    ? math.lerp(root, elbow, t/.45f)
                    : math.lerp(elbow, wrist, (t-.45f)/.55f);
                ChamferedBox(a, o, p + new float3(-1.2f*side, 1.8f, -1),
                    new float3(3.8f*scale, 2.0f*scale, 2.8f*scale), Scale);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Controlled foreground C-sweep. It is intentionally shorter and slimmer than every
            // previous clean pass, with no standalone blade branch that can detach in the renderer.
            float3[] p =
            {
                new float3(13,31,37), new float3(34,25,43), new float3(54,18,40),
                new float3(72,12,30), new float3(84,7,15), new float3(84,5,-2),
                new float3(77,4,-18), new float3(66,3,-31), new float3(53,2.5f,-41),
                new float3(42,2.2f,-49), new float3(34,2.0f,-54)
            };
            float[] r = { 12.5f,11.0f,9.2f,7.4f,5.8f,4.6f,3.6f,2.7f,1.9f,1.1f,.38f };
            for (int i = 0; i < p.Length - 1; i++)
                Capsule(a, o, p[i], p[i+1], r[i], r[i+1], Body);

            // Overlapping armor plates, rooted deeply into the tail surface.
            for (int i = 1; i < p.Length - 2; i++)
            {
                float t = (i-1)/(float)(p.Length-4);
                float3 c = p[i] + new float3(0, r[i]*.72f, -1);
                ShieldPlate(a, o, c,
                    math.lerp(4.0f,1.8f,t), math.lerp(3.8f,2.0f,t), 1.7f, Scale);
            }
        }

        private static void AuthorVentralShields(IStructureAuthoringSession a, int3 o)
        {
            // Six broad hard-surface shields. Centers follow the S-neck/chest rather than a straight stack.
            ShieldPlate(a, o, new float3(-4, 100, -58), 7.0f, 6.0f, 2.5f, Armor);
            ShieldPlate(a, o, new float3(-3, 88, -48), 9.0f, 7.0f, 2.7f, Armor);
            ShieldPlate(a, o, new float3(-1, 75, -39), 11.0f, 8.0f, 2.9f, Armor);
            ShieldPlate(a, o, new float3(0, 62, -32), 13.0f, 8.5f, 3.1f, Armor);
            ShieldPlate(a, o, new float3(0, 49, -27), 14.5f, 9.0f, 3.3f, Armor);
            ShieldPlate(a, o, new float3(0, 37, -24), 13.0f, 8.0f, 3.1f, Armor);
        }

        private static void AuthorNeckScaleRows(IStructureAuthoringSession a, int3 o)
        {
            // Overlapping side scales create the reference's plated neck. Leave the central front free
            // for the large warm ventral shields.
            for (int row = 0; row < 6; row++)
            {
                float t = row/5f;
                float y = math.lerp(69f, 110f, t);
                float z = math.lerp(-30f, -59f, t);
                float lateral = math.lerp(10.5f, 7.0f, t);
                float size = math.lerp(4.2f, 3.2f, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    ShieldPlate(a, o, new float3(lateral*side, y, z-3), size, size*1.15f, 1.8f, Scale);
                    ShieldPlate(a, o, new float3((lateral+5.0f)*side, y-2.5f, z+1), size*.85f, size, 1.6f, Scale);
                }
            }
        }

        private static void AuthorBodyScaleClusters(IStructureAuthoringSession a, int3 o)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                // Shoulder plates.
                float3[] shoulder =
                {
                    new float3(19*s,62,-27), new float3(25*s,58,-24), new float3(28*s,52,-19),
                    new float3(29*s,46,-12), new float3(25*s,42,-8)
                };
                for (int i=0;i<shoulder.Length;i++)
                    ShieldPlate(a,o,shoulder[i],4.0f,4.6f,1.8f,Scale);

                // Haunch plates wrap the visible/front face.
                float3[] haunch =
                {
                    new float3(25*s,38,2),new float3(31*s,35,4),new float3(35*s,30,6),
                    new float3(35*s,24,5),new float3(31*s,20,1),new float3(25*s,19,-1)
                };
                for(int i=0;i<haunch.Length;i++)
                    ShieldPlate(a,o,haunch[i],4.3f,4.8f,1.8f,Scale);
            }
        }

        private static void AuthorDorsalCrest(IStructureAuthoringSession a, int3 o)
        {
            float3[] roots =
            {
                new float3(-5,116,-55),new float3(-3,105,-46),new float3(-1,94,-37),
                new float3(0,83,-28),new float3(0,72,-18),new float3(0,61,-8),
                new float3(0,50,3),new float3(0,39,14)
            };
            for(int i=0;i<roots.Length;i++)
            {
                float t=i/(float)(roots.Length-1);
                HardSegment(a,o,roots[i],roots[i]+new float3(0,math.lerp(7.0f,4.0f,t),4),
                    math.lerp(1.8f,1.2f,t),.30f,Scale);
            }
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            ChamferedBox(a,o,new float3(9,91,-44),new float3(2.4f,1.2f,1.5f),Moss);
            ChamferedBox(a,o,new float3(18,60,-7),new float3(2.8f,1.3f,1.8f),Moss);
            ChamferedBox(a,o,new float3(29,33,28),new float3(3.0f,1.3f,2.0f),Moss);
            ChamferedBox(a,o,new float3(57,18,39),new float3(2.4f,1.1f,1.6f),Moss);
            ChamferedBox(a,o,new float3(61,113,26),new float3(2.2f,1.0f,1.5f),Moss);
        }

        // ---- Hard-surface / voxel geometry helpers -------------------------------------------------

        private static void ChamferedPrismZ(IStructureAuthoringSession a, int3 o,
            float cx, float cy, int z0, int z1,
            float halfX0, float halfX1, float halfY0, float halfY1, byte material)
        {
            int minZ = math.min(z0,z1), maxZ = math.max(z0,z1);
            int dz = math.max(1,maxZ-minZ);
            for(int z=minZ;z<=maxZ;z++)
            {
                float t=(z-minZ)/(float)dz;
                float hx=math.lerp(halfX0,halfX1,t), hy=math.lerp(halfY0,halfY1,t);
                FillChamferedSliceXY(a,o,cx,cy,z,hx,hy,material);
            }
        }

        private static void FillChamferedSliceXY(IStructureAuthoringSession a,int3 o,float cx,float cy,int z,float hx,float hy,byte material)
        {
            int minX=(int)math.floor(cx-hx),maxX=(int)math.ceil(cx+hx);
            int minY=(int)math.floor(cy-hy),maxY=(int)math.ceil(cy+hy);
            for(int y=minY;y<=maxY;y++)
            for(int x=minX;x<=maxX;x++)
            {
                float ax=math.abs(x+.5f-cx)/math.max(.5f,hx);
                float ay=math.abs(y+.5f-cy)/math.max(.5f,hy);
                if(ax<=1f && ay<=1f && ax+ay<=1.48f)
                    a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void ChamferedBox(IStructureAuthoringSession a,int3 o,float3 c,float3 half,byte material)
        {
            int3 min=(int3)math.floor(c-half),max=(int3)math.ceil(c+half);
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float ax=math.abs(x+.5f-c.x)/math.max(.5f,half.x);
                float ay=math.abs(y+.5f-c.y)/math.max(.5f,half.y);
                float az=math.abs(z+.5f-c.z)/math.max(.5f,half.z);
                if(ax<=1f && ay<=1f && az<=1f && ax+ay+az<=2.15f)
                    a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void ShieldPlate(IStructureAuthoringSession a,int3 o,float3 c,float halfWidth,float height,float depth,byte material)
        {
            int top=(int)math.ceil(c.y+height*.45f);
            int bottom=(int)math.floor(c.y-height*.55f);
            int front=(int)math.floor(c.z-depth),back=(int)math.ceil(c.z+depth*.25f);
            int rows=math.max(1,top-bottom);
            for(int y=bottom;y<=top;y++)
            {
                float fromTop=(top-y)/(float)rows;
                // broad shoulder, then tapered lower half to a point
                float widthFactor=fromTop<.45f?math.lerp(.78f,1f,fromTop/.45f):math.lerp(1f,.18f,(fromTop-.45f)/.55f);
                int hw=math.max(1,(int)math.round(halfWidth*widthFactor));
                for(int z=front;z<=back;z++)
                for(int x=(int)math.round(c.x)-hw;x<=(int)math.round(c.x)+hw;x++)
                    a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void HardSegment(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float len=math.length(p1-p0);
            int steps=math.max(1,(int)math.ceil(len*1.7f));
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps;
                float3 p=math.lerp(p0,p1,t);
                float r=math.lerp(r0,r1,t);
                ChamferedBox(a,o,p,new float3(r,r*.78f,r*.78f),material);
            }
        }

        private static float3 WingPoint(int side,float u,float y,float zOffset)
        {
            float nu=math.saturate((u-15f)/108f);
            float z=zOffset + .16f*u + .035f*(y-65f) + 4.2f*math.sin(math.PI*nu);
            return new float3(side*u,y,z);
        }

        private static void FillCurvedWingPolygon(IStructureAuthoringSession a,int3 o,int side,float2[] polygon,float zOffset,byte material)
        {
            float minU=float.MaxValue,maxU=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            for(int i=0;i<polygon.Length;i++)
            {
                minU=math.min(minU,polygon[i].x); maxU=math.max(maxU,polygon[i].x);
                minY=math.min(minY,polygon[i].y); maxY=math.max(maxY,polygon[i].y);
            }
            for(int y=(int)math.floor(minY);y<=(int)math.ceil(maxY);y++)
            for(int u=(int)math.floor(minU);u<=(int)math.ceil(maxU);u++)
            {
                float2 p=new float2(u+.5f,y+.5f);
                if(!PointInPolygon(p,polygon))continue;
                float3 wp=WingPoint(side,u+.5f,y+.5f,zOffset);
                int zc=(int)math.round(wp.z);
                for(int dz=-1;dz<=1;dz++)a.Set(o.x+side*u,o.y+y,o.z+zc+dz,material);
            }
        }

        private static bool PointInPolygon(float2 p,float2[] poly)
        {
            bool inside=false;
            for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)
            {
                float2 a=poly[i],b=poly[j];
                bool cross=((a.y>p.y)!=(b.y>p.y)) &&
                    (p.x < (b.x-a.x)*(p.y-a.y)/(b.y-a.y+.00001f)+a.x);
                if(cross)inside=!inside;
            }
            return inside;
        }

        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte material)
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

        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float mr=math.max(r0,r1);
            int3 min=(int3)math.floor(math.min(p0,p1)-mr-1),max=(int3)math.ceil(math.max(p0,p1)+mr+1);
            float3 axis=p1-p0; float l2=math.max(.0001f,math.dot(axis,axis));
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 p=new float3(x+.5f,y+.5f,z+.5f);
                float t=math.saturate(math.dot(p-p0,axis)/l2);
                float3 d=p-(p0+axis*t); float r=math.lerp(r0,r1,t);
                if(math.dot(d,d)<=r*r)a.Set(o.x+x,o.y+y,o.z+z,material);
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
