using Game.Materials.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// V11 keeps the V10 low torso as hidden mass, then deliberately removes and re-sculpts every
    /// silhouette-critical area exposed by the production captures: head/neck, wings, limbs, haunch
    /// edges and tail. The goal is the reference's elegant seated dragon rather than a procedural
    /// creature assembled from obvious tubes and sheets.
    /// </summary>
    public static class DragonStatueConceptV11ReferenceSilhouetteAuthoring
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

            DragonStatueConceptV10HardSurfaceAuthoring.Author(a, o);
            RemoveV10SilhouetteFailures(a, o);

            AuthorTorsoBridges(a, o);
            AuthorNeck(a, o);
            AuthorHead(a, o);
            AuthorForeleg(a, o, -1);
            AuthorForeleg(a, o, 1);
            AuthorRearLeg(a, o, -1);
            AuthorRearLeg(a, o, 1);
            AuthorWing(a, o, 1, 1.00f, 8f);
            AuthorWing(a, o, -1, .84f, -14f);
            AuthorTail(a, o);
            AuthorVentralArmor(a, o);
            AuthorScaleLanguage(a, o);
            AuthorPatina(a, o);
        }

        private static void RemoveV10SilhouetteFailures(IStructureAuthoringSession a, int3 o)
        {
            // Delete the rectangular flag wings. Keep the torso hidden underneath.
            ClearBox(a, o, new int3(13, 44, -10), new int3(122, 112, 57));
            ClearBox(a, o, new int3(-135, 37, -32), new int3(122, 119, 66));

            // Rebuild the entire head and upper neck; this also removes the asymmetric V10 mouth cut.
            ClearBox(a, o, new int3(-33, 98, -110), new int3(66, 58, 102));
            ClearBox(a, o, new int3(-20, 55, -72), new int3(40, 48, 67));

            // Strip the dangling forelegs and the outer haunch/feet silhouettes.
            ClearBox(a, o, new int3(10, 0, -80), new int3(39, 67, 87));
            ClearBox(a, o, new int3(-49, 0, -80), new int3(39, 67, 87));
            ClearBox(a, o, new int3(18, 0, -48), new int3(43, 46, 100));
            ClearBox(a, o, new int3(-61, 0, -48), new int3(43, 46, 100));

            // Remove V10's hose-like foreground tail completely before the new sweep is laid in.
            ClearBox(a, o, new int3(10, 0, -66), new int3(97, 38, 121));
        }

        private static void AuthorTorsoBridges(IStructureAuthoringSession a, int3 o)
        {
            // Reconnect the chest after the silhouette clears and create a distinct sternum wedge.
            Ellipsoid(a, o, new float3(0, 53, -12), new float3(21, 17, 20), Body);
            Ellipsoid(a, o, new float3(0, 40, 1), new float3(24, 17, 22), Body);
            ChamferedBox(a, o, new float3(0, 51, -28), new float3(13, 12, 7), Body);
            ChamferedBox(a, o, new float3(0, 38, -26), new float3(15, 9, 6), Shadow);
        }

        private static void AuthorNeck(IStructureAuthoringSession a, int3 o)
        {
            // A proud S-neck: broad at the shoulder girdle, quickly tapering toward the skull.
            Capsule(a, o, new float3(0, 58, -18), new float3(1, 72, -28), 14.0f, 12.0f, Body);
            Capsule(a, o, new float3(1, 71, -28), new float3(-1, 86, -39), 12.0f, 10.0f, Body);
            Capsule(a, o, new float3(-1, 85, -39), new float3(-2, 101, -51), 10.0f, 8.4f, Body);
            Capsule(a, o, new float3(-2, 100, -51), new float3(-2, 114, -62), 8.4f, 7.2f, Body);

            // Side armor is embedded into the neck, not floating white lumps.
            for (int row = 0; row < 7; row++)
            {
                float t = row / 6f;
                float y = math.lerp(66f, 111f, t);
                float z = math.lerp(-26f, -59f, t);
                float lateral = math.lerp(10.3f, 6.7f, t);
                float size = math.lerp(4.1f, 2.8f, t);
                for (int side = -1; side <= 1; side += 2)
                {
                    ShieldPlate(a, o, new float3(lateral * side, y, z - 1.5f), size, size * 1.3f, 2.2f, Scale);
                    if (row < 6)
                        ShieldPlate(a, o, new float3((lateral + 4.2f) * side, y - 3.2f, z + 2.8f), size * .78f, size, 1.7f, Scale);
                }
            }
        }

        private static void AuthorHead(IStructureAuthoringSession a, int3 o)
        {
            // Compact wedge skull. The muzzle is narrower and the jaw shorter than V10.
            ChamferedPrismZ(a, o, 0, 119, -77, -56, 11.5f, 17.0f, 6.4f, 10.0f, Body);
            ChamferedBox(a, o, new float3(-14, 116, -63), new float3(6.8f, 7.5f, 8.0f), Body);
            ChamferedBox(a, o, new float3(14, 116, -63), new float3(6.8f, 7.5f, 8.0f), Body);

            ChamferedPrismZ(a, o, 0, 116, -101, -75, 5.2f, 11.0f, 3.0f, 5.5f, Body);
            ChamferedBox(a, o, new float3(0, 119, -96), new float3(6.0f, 3.6f, 4.0f), Body);
            ShieldPlate(a, o, new float3(0, 125, -84), 6.8f, 5.2f, 2.4f, Scale);

            // Symmetric nostrils.
            ChamferedBox(a, o, new float3(-4.5f, 118, -99), new float3(1.4f, 1.1f, 1.6f), Empty);
            ChamferedBox(a, o, new float3(4.5f, 118, -99), new float3(1.4f, 1.1f, 1.6f), Empty);

            // Roaring mouth: a tapered cavity, with enough upper/lower lip left to avoid the bar-jaw read.
            ChamferedPrismZ(a, o, 0, 110, -95, -70, 3.6f, 8.4f, 2.4f, 4.0f, Empty);
            ChamferedPrismZ(a, o, 0, 105, -92, -68, 4.0f, 8.8f, 1.8f, 3.1f, Body);
            ChamferedBox(a, o, new float3(-10.5f, 110, -67), new float3(4.8f, 5.0f, 5.0f), Body);
            ChamferedBox(a, o, new float3(10.5f, 110, -67), new float3(4.8f, 5.0f, 5.0f), Body);

            // Brow/eye planes point backward, creating the predatory reference silhouette.
            HardSegment(a, o, new float3(-2, 129, -77), new float3(-16, 128, -64), 3.2f, 1.0f, Shadow);
            HardSegment(a, o, new float3(2, 129, -77), new float3(16, 128, -64), 3.2f, 1.0f, Shadow);
            ChamferedBox(a, o, new float3(-10.5f, 123, -75), new float3(2.6f, 2.1f, 2.2f), Empty);
            ChamferedBox(a, o, new float3(10.5f, 123, -75), new float3(2.6f, 2.1f, 2.2f), Empty);
            ChamferedBox(a, o, new float3(-10.5f, 123, -77), new float3(1.25f, 1.1f, 1.0f), Eye);
            ChamferedBox(a, o, new float3(10.5f, 123, -77), new float3(1.25f, 1.1f, 1.0f), Eye);

            // Main horn pair: rooted, swept backward, then elegantly kicked upward at the tips.
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                float3 p0 = new float3(8.5f * s, 133, -59);
                float3 p1 = new float3(12.5f * s, 140, -51);
                float3 p2 = new float3(16.5f * s, 145, -40);
                float3 p3 = new float3(18.0f * s, 146, -28);
                float3 p4 = new float3(16.0f * s, 152, -17);
                HardSegment(a, o, p0, p1, 4.0f, 3.4f, Body);
                HardSegment(a, o, p1, p2, 3.4f, 2.4f, Scale);
                HardSegment(a, o, p2, p3, 2.4f, 1.25f, Scale);
                HardSegment(a, o, p3, p4, 1.25f, .25f, Scale);

                // Layered swept-back crown and cheek fins.
                HardSegment(a, o, new float3(10*s, 132, -61), new float3(24*s, 136, -48), 2.4f, .25f, Scale);
                HardSegment(a, o, new float3(13*s, 125, -65), new float3(29*s, 128, -52), 2.0f, .22f, Scale);
                HardSegment(a, o, new float3(14*s, 117, -66), new float3(27*s, 115, -53), 1.8f, .20f, Scale);
                HardSegment(a, o, new float3(11*s, 109, -68), new float3(20*s, 103, -57), 1.5f, .18f, Scale);
            }

            // Small teeth trace the lip line; none are independent floating fragments.
            int[] toothZ = { -77, -84, -91 };
            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < toothZ.Length; i++)
            {
                float x = side * (3.6f + i * .9f);
                HardSegment(a, o, new float3(x, 114, toothZ[i]), new float3(x + .15f*side, 110, toothZ[i] - 1.0f), 1.2f, .25f, Scale);
            }

            // Facial armor breaks the remaining broad surfaces into sculpted planes.
            ShieldPlate(a, o, new float3(-13, 129, -63), 4.5f, 4.2f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(13, 129, -63), 4.5f, 4.2f, 2.0f, Scale);
            ShieldPlate(a, o, new float3(-16, 119, -67), 4.0f, 3.8f, 1.8f, Scale);
            ShieldPlate(a, o, new float3(16, 119, -67), 4.0f, 3.8f, 1.8f, Scale);
        }

        private static void AuthorForeleg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 shoulder = new float3(17*s, 58, -20);
            float3 elbow = new float3(27*s, 42, -31);
            float3 wrist = new float3(22*s, 23, -43);
            float3 palm = new float3(24*s, 8, -55);

            Ellipsoid(a, o, shoulder, new float3(10.5f, 11.5f, 10.0f), Body);
            Capsule(a, o, shoulder, elbow, 8.0f, 6.0f, Body);
            ChamferedBox(a, o, elbow, new float3(6.4f, 6.3f, 6.8f), Body);
            Capsule(a, o, elbow, wrist, 5.8f, 4.2f, Body);
            ChamferedBox(a, o, wrist, new float3(4.5f, 4.5f, 5.2f), Shadow);
            Capsule(a, o, wrist, palm, 4.0f, 3.4f, Body);
            ChamferedBox(a, o, palm + new float3(0,0,-2), new float3(8.0f, 4.0f, 7.8f), Body);

            ShieldPlate(a, o, new float3(21*s, 60, -29), 4.8f, 4.6f, 2.1f, Scale);
            ShieldPlate(a, o, new float3(27*s, 43, -36), 4.0f, 4.0f, 1.9f, Scale);

            float[] offsets = { -5.4f, -1.8f, 1.8f, 5.4f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 2.3f : 0f;
                float x = 24*s + offsets[i]*s;
                float3 knuckle = new float3(x, 7.5f, -58);
                float3 toe = new float3(x + .8f*s, 4.3f, -65 - extra);
                float3 claw = new float3(x + 2.0f*s, 1.5f, -72 - extra);
                HardSegment(a, o, knuckle, toe, 2.2f, 1.55f, Body);
                HardSegment(a, o, toe, claw, 1.55f, .25f, Scale);
            }
        }

        private static void AuthorRearLeg(IStructureAuthoringSession a, int3 o, int side)
        {
            float s = side;
            float3 hip = new float3(24*s, 29, 18);
            float3 knee = new float3(34*s, 21, 4);
            float3 hock = new float3(29*s, 12, -10);
            float3 foot = new float3(31*s, 7, -24);

            Ellipsoid(a, o, hip, new float3(15.0f, 14.5f, 17.0f), Body);
            Capsule(a, o, hip, knee, 9.0f, 6.6f, Body);
            ChamferedBox(a, o, knee, new float3(6.5f, 6.2f, 7.2f), Body);
            Capsule(a, o, knee, hock, 6.2f, 4.5f, Body);
            Capsule(a, o, hock, foot, 4.4f, 3.4f, Body);
            ChamferedBox(a, o, foot, new float3(8.6f, 3.8f, 9.0f), Body);

            ShieldPlate(a, o, new float3(30*s, 29, -1), 4.5f, 4.2f, 1.9f, Scale);
            ShieldPlate(a, o, new float3(34*s, 20, -5), 3.8f, 3.7f, 1.8f, Scale);

            float[] offsets = { -5.4f, -1.8f, 1.8f, 5.4f };
            for (int i = 0; i < offsets.Length; i++)
            {
                float extra = (i == 1 || i == 2) ? 1.8f : 0f;
                float x = 31*s + offsets[i]*s;
                float3 knuckle = new float3(x, 6.5f, -27);
                float3 toe = new float3(x + .6f*s, 3.8f, -34 - extra);
                float3 claw = new float3(x + 1.8f*s, 1.4f, -41 - extra);
                HardSegment(a, o, knuckle, toe, 2.0f, 1.45f, Body);
                HardSegment(a, o, toe, claw, 1.45f, .24f, Scale);
            }
        }

        private static void AuthorWing(IStructureAuthoringSession a, int3 o, int side, float scale, float zOffset)
        {
            // A tall arched leading edge and four deep trailing bays, matching the reference's wing rhythm.
            float2[] outline =
            {
                new float2(15,62), new float2(34,84), new float2(56,109), new float2(82,134),
                new float2(106,150), new float2(124,148),
                new float2(120,121), new float2(108,111),
                new float2(111,85), new float2(92,92),
                new float2(95,67), new float2(74,76),
                new float2(73,55), new float2(53,65), new float2(35,59)
            };
            for (int i=0;i<outline.Length;i++) outline[i] *= scale;
            FillCurvedWingPolygon(a, o, side, outline, zOffset, Membrane);

            float3 root = WingPoint(side, 15*scale, 62*scale, zOffset);
            float3 elbow = WingPoint(side, 34*scale, 84*scale, zOffset);
            float3 wrist = WingPoint(side, 56*scale, 109*scale, zOffset);
            float3 arch = WingPoint(side, 82*scale, 134*scale, zOffset);
            float3 crown = WingPoint(side, 106*scale, 150*scale, zOffset);
            float3 hook = WingPoint(side, 124*scale, 148*scale, zOffset);

            HardSegment(a, o, root, elbow, 7.0f*scale, 5.8f*scale, Body);
            HardSegment(a, o, elbow, wrist, 5.8f*scale, 4.2f*scale, Body);
            HardSegment(a, o, wrist, arch, 4.2f*scale, 3.0f*scale, Body);
            HardSegment(a, o, arch, crown, 3.0f*scale, 1.8f*scale, Scale);
            HardSegment(a, o, crown, hook, 1.8f*scale, .28f, Scale);

            float2[] fingers =
            {
                new float2(120,121), new float2(111,85), new float2(95,67), new float2(73,55)
            };
            for (int i=0;i<fingers.Length;i++)
            {
                float3 end = WingPoint(side, fingers[i].x*scale, fingers[i].y*scale, zOffset);
                float3 fingerRoot = i < 2 ? math.lerp(wrist, arch, .18f + i*.18f) : wrist;
                float3 bend = math.lerp(fingerRoot, end, .50f);
                bend += new float3((4.0f-i*.55f)*scale*side, 3.0f-i*.35f, 2.6f*scale);
                float r0 = math.lerp(2.7f, 1.7f, i/3f) * scale;
                HardSegment(a, o, fingerRoot, bend, r0, r0*.62f, Body);
                HardSegment(a, o, bend, end, r0*.62f, .24f, Scale);
            }

            // Overlapping leading-edge plates emphasize the graceful arch instead of a smooth tube.
            float3[] spar = { root, elbow, wrist, arch, crown };
            for (int seg=0;seg<spar.Length-1;seg++)
            for (int j=1;j<=3;j++)
            {
                float3 p = math.lerp(spar[seg], spar[seg+1], j/4f);
                float size = math.lerp(4.2f, 2.6f, (seg*3+j)/13f)*scale;
                ShieldPlate(a, o, p + new float3(-1.0f*side, 1.5f, -1), size, size*.95f, 1.8f*scale, Scale);
            }
        }

        private static void AuthorTail(IStructureAuthoringSession a, int3 o)
        {
            // Broad root, sweeping right and forward across the ground, then tapering back toward center.
            float3[] p =
            {
                new float3(12,31,35), new float3(31,25,44), new float3(52,18,47),
                new float3(72,12,42), new float3(89,7,28), new float3(98,4,9),
                new float3(96,3,-11), new float3(85,3,-29), new float3(68,3,-43),
                new float3(48,2.7f,-53), new float3(28,2.4f,-59), new float3(10,2.2f,-61)
            };
            float[] r = { 12.0f,10.8f,9.3f,7.7f,6.2f,5.0f,4.1f,3.2f,2.4f,1.7f,.95f,.28f };
            for (int i=0;i<p.Length-1;i++) Capsule(a,o,p[i],p[i+1],r[i],r[i+1],Body);

            for (int i=1;i<p.Length-2;i++)
            {
                float t=(i-1)/(float)(p.Length-4);
                ShieldPlate(a,o,p[i]+new float3(0,r[i]*.72f,-1),math.lerp(4.2f,1.7f,t),math.lerp(4.0f,2.0f,t),1.7f,Scale);
                if (i>=3)
                {
                    float3 spikeRoot=p[i]+new float3(0,r[i]*.78f,2.0f);
                    HardSegment(a,o,spikeRoot,spikeRoot+new float3(0,math.lerp(6f,3f,t),4.5f),math.lerp(1.5f,.8f,t),.22f,Scale);
                }
            }
        }

        private static void AuthorVentralArmor(IStructureAuthoringSession a, int3 o)
        {
            // Overlap the warm plates so they read as one armored throat/breast rather than floating medallions.
            float3[] centers =
            {
                new float3(0,105,-61), new float3(0,96,-55), new float3(0,86,-47),
                new float3(0,75,-39), new float3(0,64,-33), new float3(0,53,-29),
                new float3(0,42,-26)
            };
            float[] widths = { 6.2f,7.4f,8.7f,10.2f,11.8f,13.2f,13.8f };
            for (int i=0;i<centers.Length;i++)
                ShieldPlate(a,o,centers[i],widths[i],8.0f + i*.45f,2.5f + i*.12f,Armor);
        }

        private static void AuthorScaleLanguage(IStructureAuthoringSession a, int3 o)
        {
            // Shoulder/haunch plates follow muscle flow and mask remaining smooth low-frequency masses.
            for (int side=-1;side<=1;side+=2)
            {
                float s=side;
                float3[] shoulder =
                {
                    new float3(18*s,62,-25), new float3(24*s,58,-21), new float3(27*s,53,-15),
                    new float3(27*s,48,-8), new float3(23*s,44,-3)
                };
                for(int i=0;i<shoulder.Length;i++) ShieldPlate(a,o,shoulder[i],3.8f,4.6f,1.8f,Scale);

                float3[] haunch =
                {
                    new float3(23*s,38,5), new float3(29*s,35,8), new float3(33*s,31,9),
                    new float3(34*s,26,7), new float3(31*s,22,3), new float3(25*s,20,0)
                };
                for(int i=0;i<haunch.Length;i++) ShieldPlate(a,o,haunch[i],4.0f,4.6f,1.8f,Scale);
            }

            // A restrained dorsal crest: larger near shoulders/head, tapering into the tail.
            float3[] crest =
            {
                new float3(0,113,-56),new float3(0,102,-47),new float3(0,91,-38),
                new float3(0,80,-29),new float3(0,69,-19),new float3(0,58,-9),
                new float3(0,47,3),new float3(0,38,15)
            };
            for(int i=0;i<crest.Length;i++)
            {
                float t=i/(float)(crest.Length-1);
                HardSegment(a,o,crest[i],crest[i]+new float3(0,math.lerp(7.5f,4.0f,t),4.5f),math.lerp(1.8f,1.0f,t),.22f,Scale);
            }
        }

        private static void AuthorPatina(IStructureAuthoringSession a, int3 o)
        {
            // Sparse moss only where it supports age and form; never enough to obscure the silhouette.
            ChamferedBox(a,o,new float3(8,92,-43),new float3(2.2f,1.1f,1.4f),Moss);
            ChamferedBox(a,o,new float3(20,60,-5),new float3(2.8f,1.2f,1.7f),Moss);
            ChamferedBox(a,o,new float3(31,34,20),new float3(2.7f,1.2f,1.8f),Moss);
            ChamferedBox(a,o,new float3(60,17,44),new float3(2.3f,1.0f,1.5f),Moss);
            ChamferedBox(a,o,new float3(73,118,26),new float3(2.0f,1.0f,1.4f),Moss);
        }

        // ---- Local geometry helpers ---------------------------------------------------------------

        private static void ClearBox(IStructureAuthoringSession a,int3 o,int3 min,int3 size)
        {
            int3 max=min+size;
            for(int y=min.y;y<max.y;y++)
            for(int z=min.z;z<max.z;z++)
            for(int x=min.x;x<max.x;x++) a.Set(o.x+x,o.y+y,o.z+z,Empty);
        }

        private static void ChamferedPrismZ(IStructureAuthoringSession a,int3 o,float cx,float cy,int z0,int z1,float halfX0,float halfX1,float halfY0,float halfY1,byte material)
        {
            int minZ=math.min(z0,z1),maxZ=math.max(z0,z1),dz=math.max(1,math.abs(z1-z0));
            for(int z=minZ;z<=maxZ;z++)
            {
                float t=(z-minZ)/(float)dz;
                FillChamferedSliceXY(a,o,cx,cy,z,math.lerp(halfX0,halfX1,t),math.lerp(halfY0,halfY1,t),material);
            }
        }

        private static void FillChamferedSliceXY(IStructureAuthoringSession a,int3 o,float cx,float cy,int z,float hx,float hy,byte material)
        {
            int minX=(int)math.floor(cx-hx),maxX=(int)math.ceil(cx+hx),minY=(int)math.floor(cy-hy),maxY=(int)math.ceil(cy+hy);
            for(int y=minY;y<=maxY;y++)
            for(int x=minX;x<=maxX;x++)
            {
                float ax=math.abs(x+.5f-cx)/math.max(.5f,hx),ay=math.abs(y+.5f-cy)/math.max(.5f,hy);
                if(ax<=1f && ay<=1f && ax+ay<=1.48f) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void ChamferedBox(IStructureAuthoringSession a,int3 o,float3 c,float3 half,byte material)
        {
            int3 min=(int3)math.floor(c-half),max=(int3)math.ceil(c+half);
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float ax=math.abs(x+.5f-c.x)/math.max(.5f,half.x),ay=math.abs(y+.5f-c.y)/math.max(.5f,half.y),az=math.abs(z+.5f-c.z)/math.max(.5f,half.z);
                if(ax<=1f && ay<=1f && az<=1f && ax+ay+az<=2.15f) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void ShieldPlate(IStructureAuthoringSession a,int3 o,float3 c,float halfWidth,float height,float depth,byte material)
        {
            int top=(int)math.ceil(c.y+height*.45f),bottom=(int)math.floor(c.y-height*.55f),front=(int)math.floor(c.z-depth),back=(int)math.ceil(c.z+depth*.25f),rows=math.max(1,top-bottom);
            for(int y=bottom;y<=top;y++)
            {
                float fromTop=(top-y)/(float)rows;
                float widthFactor=fromTop<.45f?math.lerp(.78f,1f,fromTop/.45f):math.lerp(1f,.18f,(fromTop-.45f)/.55f);
                int hw=math.max(1,(int)math.round(halfWidth*widthFactor));
                for(int z=front;z<=back;z++)
                for(int x=(int)math.round(c.x)-hw;x<=(int)math.round(c.x)+hw;x++) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void HardSegment(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float len=math.length(p1-p0); int steps=math.max(1,(int)math.ceil(len*1.7f));
            for(int i=0;i<=steps;i++)
            {
                float t=i/(float)steps; float3 p=math.lerp(p0,p1,t); float r=math.lerp(r0,r1,t);
                ChamferedBox(a,o,p,new float3(r,r*.78f,r*.78f),material);
            }
        }

        private static float3 WingPoint(int side,float u,float y,float zOffset)
        {
            float nu=math.saturate((u-14f)/112f);
            // More camber than V10: root recedes, outer wing comes forward, preventing billboard-flat reads.
            float z=zOffset + .13f*u + .045f*(y-62f) + 6.0f*math.sin(math.PI*nu);
            return new float3(side*u,y,z);
        }

        private static void FillCurvedWingPolygon(IStructureAuthoringSession a,int3 o,int side,float2[] polygon,float zOffset,byte material)
        {
            float minU=float.MaxValue,maxU=float.MinValue,minY=float.MaxValue,maxY=float.MinValue;
            for(int i=0;i<polygon.Length;i++){minU=math.min(minU,polygon[i].x);maxU=math.max(maxU,polygon[i].x);minY=math.min(minY,polygon[i].y);maxY=math.max(maxY,polygon[i].y);}
            for(int y=(int)math.floor(minY);y<=(int)math.ceil(maxY);y++)
            for(int u=(int)math.floor(minU);u<=(int)math.ceil(maxU);u++)
            {
                float2 q=new float2(u+.5f,y+.5f); if(!PointInPolygon(q,polygon))continue;
                float3 wp=WingPoint(side,u+.5f,y+.5f,zOffset); int zc=(int)math.round(wp.z);
                for(int dz=-1;dz<=1;dz++) a.Set(o.x+side*u,o.y+y,o.z+zc+dz,material);
            }
        }

        private static bool PointInPolygon(float2 p,float2[] poly)
        {
            bool inside=false;
            for(int i=0,j=poly.Length-1;i<poly.Length;j=i++)
            {
                float2 q=poly[i],r=poly[j];
                bool cross=((q.y>p.y)!=(r.y>p.y)) && (p.x < (r.x-q.x)*(p.y-q.y)/(r.y-q.y+.00001f)+q.x);
                if(cross)inside=!inside;
            }
            return inside;
        }

        private static void Ellipsoid(IStructureAuthoringSession a,int3 o,float3 c,float3 r,byte material)
        {
            int3 min=(int3)math.floor(c-r-1),max=(int3)math.ceil(c+r+1); float3 safe=math.max(r,new float3(.5f));
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 q=(new float3(x+.5f,y+.5f,z+.5f)-c)/safe;
                if(math.dot(q,q)<=1f) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }

        private static void Capsule(IStructureAuthoringSession a,int3 o,float3 p0,float3 p1,float r0,float r1,byte material)
        {
            float mr=math.max(r0,r1); int3 min=(int3)math.floor(math.min(p0,p1)-mr-1),max=(int3)math.ceil(math.max(p0,p1)+mr+1);
            float3 axis=p1-p0; float l2=math.max(.0001f,math.dot(axis,axis));
            for(int y=min.y;y<=max.y;y++)
            for(int z=min.z;z<=max.z;z++)
            for(int x=min.x;x<=max.x;x++)
            {
                float3 p=new float3(x+.5f,y+.5f,z+.5f); float t=math.saturate(math.dot(p-p0,axis)/l2); float3 d=p-(p0+axis*t); float r=math.lerp(r0,r1,t);
                if(math.dot(d,d)<=r*r) a.Set(o.x+x,o.y+y,o.z+z,material);
            }
        }
    }
}
