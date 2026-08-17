using Game.Materials.Api;
using Game.Structures.Api;
using Unity.Mathematics;
using VoxelEngine.Structures.Api;
using SharedOpeningAuthoring = VoxelEngine.Structures.Runtime.StructureOpeningAuthoring;
using SharedRoofAuthoring = VoxelEngine.Structures.Runtime.StructureRoofAuthoring;

namespace Game.Structures.Runtime
{
    public static class CathedralAuthoring
    {
        private struct Rect
        {
            public int3 Min;
            public int Width;
            public int Depth;
        }

        public static void Author(IStructureAuthoringSession a, int3 origin, in CathedralConfig c)
        {
            if (a == null) throw new System.ArgumentNullException(nameof(a));
            if (!c.IsWellFormed) throw new System.ArgumentException("Cathedral configuration is invalid.", nameof(c));
            Foundation(a, origin, in c);
            ChurchConfig church = c.Church;
            church.Footprint.FoundationStyle = StructureFoundationStyle.None;
            church.Footprint.FoundationDepth = 0;
            ChurchAuthoring.Author(a, origin, in church);
            ExtraAisles(a, origin, in c);
            Transept(a, origin, in c);
            Chapels(a, origin, in c);
            Rose(a, origin, in c);
            FrontTowers(a, origin, in c);
            CrossingTower(a, origin, in c);
            Crypt(a, origin, in c);
        }

        private static void Foundation(IStructureAuthoringSession a, int3 origin, in CathedralConfig c)
        {
            if (c.Footprint.FoundationStyle == StructureFoundationStyle.None) return;
            Rect r = Resolve(in c.Footprint.Primary, origin, c.Church.EntryFacing);
            a.Box(new int3(r.Min.x, origin.y-c.Footprint.FoundationDepth, r.Min.z),
                new int3(r.Width,c.Footprint.FoundationDepth,r.Depth),
                c.Church.Palette.Resolve(c.Footprint.FoundationMaterial));
        }

        private static void ExtraAisles(IStructureAuthoringSession a, int3 origin, in CathedralConfig c)
        {
            if (c.ExtraAisleCountPerSide<=0) return;
            ChurchConfig ch=c.Church; int front=ch.Footprint.Primary.Min.y;
            Facing west=World(Facing.West,in ch), east=World(Facing.East,in ch);
            int arches=c.ExtraAisleArch.MaxCountForSpan(ch.NaveLength);
            int windows=c.ExtraAisleWindow.MaxCountForSpan(ch.NaveLength);
            StructureFootprintRect pwLocal=new StructureFootprintRect(new int2(-ch.NaveWidth/2-ch.AisleWidth,front),new int2(ch.AisleWidth,ch.NaveLength));
            StructureFootprintRect peLocal=new StructureFootprintRect(new int2(ch.NaveWidth/2,front),new int2(ch.AisleWidth,ch.NaveLength));
            int pwHeight=ch.AisleHeight, peHeight=ch.AisleHeight;
            for(int level=0;level<c.ExtraAisleCountPerSide;level++)
            {
                int half=c.BaseAssemblyWidth/2+level*c.ExtraAisleWidth;
                StructureFootprintRect wl=new StructureFootprintRect(new int2(-half-c.ExtraAisleWidth,front),new int2(c.ExtraAisleWidth,ch.NaveLength));
                StructureFootprintRect el=new StructureFootprintRect(new int2(half,front),new int2(c.ExtraAisleWidth,ch.NaveLength));
                Rect w=Resolve(in wl,origin,ch.EntryFacing), e=Resolve(in el,origin,ch.EntryFacing);
                Rect pw=Resolve(in pwLocal,origin,ch.EntryFacing), pe=Resolve(in peLocal,origin,ch.EntryFacing);
                Shell(a,in w,c.ExtraAisleHeight,in ch); Shell(a,in e,c.ExtraAisleHeight,in ch);
                Open(a,in pw,pwHeight,in c.ExtraAisleArch,arches,west,c.ExtraAisleArch.Spacing,0,in ch);
                Open(a,in w,c.ExtraAisleHeight,in c.ExtraAisleArch,arches,east,c.ExtraAisleArch.Spacing,0,in ch);
                Open(a,in pe,peHeight,in c.ExtraAisleArch,arches,east,c.ExtraAisleArch.Spacing,0,in ch);
                Open(a,in e,c.ExtraAisleHeight,in c.ExtraAisleArch,arches,west,c.ExtraAisleArch.Spacing,0,in ch);
                Roof(a,in w,origin.y+c.ExtraAisleHeight,in c.ExtraAisleRoof,in ch);
                Roof(a,in e,origin.y+c.ExtraAisleHeight,in c.ExtraAisleRoof,in ch);
                if(level==c.ExtraAisleCountPerSide-1)
                {
                    Open(a,in w,c.ExtraAisleHeight,in c.ExtraAisleWindow,windows,west,c.ExtraAisleWindow.Spacing,0,in ch);
                    Open(a,in e,c.ExtraAisleHeight,in c.ExtraAisleWindow,windows,east,c.ExtraAisleWindow.Spacing,0,in ch);
                }
                pwLocal=wl; peLocal=el; pwHeight=c.ExtraAisleHeight; peHeight=c.ExtraAisleHeight;
            }
        }

        private static void Transept(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            ChurchConfig ch=c.Church; int front=ch.Footprint.Primary.Min.y;
            int z=front+c.TranseptCentreFromNaveFront-c.TranseptDepth/2;
            StructureFootprintRect local=new StructureFootprintRect(new int2(-c.TranseptWidth/2,z),new int2(c.TranseptWidth,c.TranseptDepth));
            Rect r=Resolve(in local,origin,ch.EntryFacing); Shell(a,in r,c.TranseptHeight,in ch);
            StructureFootprintRect crossingLocal=new StructureFootprintRect(
                new int2(-c.NaveAssemblyWidth/2+ch.WallThickness,z),
                new int2(c.NaveAssemblyWidth-ch.WallThickness*2,c.TranseptDepth));
            Rect crossing=Resolve(in crossingLocal,origin,ch.EntryFacing);
            a.Box(crossing.Min,new int3(crossing.Width,c.CrossingClearanceHeight,crossing.Depth),ch.Palette.Resolve(StructureMaterialRole.Opening));
            Roof(a,in r,origin.y+c.TranseptHeight,in c.TranseptRoof,in ch);
        }

        private static void Chapels(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            if(!c.SideChapelsEnabled) return;
            ChurchConfig ch=c.Church; int front=ch.Footprint.Primary.Min.y;
            int start=front+ch.NaveLength, centre=start+ch.SanctuaryLength/2;
            StructureFootprintRect sanctuaryLocal=new StructureFootprintRect(new int2(-ch.SanctuaryWidth/2,start),new int2(ch.SanctuaryWidth,ch.SanctuaryLength));
            Rect sanctuary=Resolve(in sanctuaryLocal,origin,ch.EntryFacing);
            Facing west=World(Facing.West,in ch), east=World(Facing.East,in ch);
            int group=c.SideChapelWidth+(c.SideChapelCountPerSide-1)*c.SideChapelSpacing;
            int first=centre-group/2+c.SideChapelWidth/2;
            for(int i=0;i<c.SideChapelCountPerSide;i++)
            {
                int cz=first+i*c.SideChapelSpacing, offset=cz-centre;
                StructureFootprintRect wl=new StructureFootprintRect(new int2(-ch.SanctuaryWidth/2-c.SideChapelDepth,cz-c.SideChapelWidth/2),new int2(c.SideChapelDepth,c.SideChapelWidth));
                StructureFootprintRect el=new StructureFootprintRect(new int2(ch.SanctuaryWidth/2,cz-c.SideChapelWidth/2),new int2(c.SideChapelDepth,c.SideChapelWidth));
                Rect w=Resolve(in wl,origin,ch.EntryFacing), e=Resolve(in el,origin,ch.EntryFacing);
                Shell(a,in w,c.SideChapelHeight,in ch); Shell(a,in e,c.SideChapelHeight,in ch);
                Open(a,in sanctuary,ch.SanctuaryHeight,in c.SideChapelArch,1,west,0,offset,in ch);
                Open(a,in w,c.SideChapelHeight,in c.SideChapelArch,1,east,0,0,in ch);
                Open(a,in sanctuary,ch.SanctuaryHeight,in c.SideChapelArch,1,east,0,offset,in ch);
                Open(a,in e,c.SideChapelHeight,in c.SideChapelArch,1,west,0,0,in ch);
                Roof(a,in w,origin.y+c.SideChapelHeight,in c.SideChapelRoof,in ch);
                Roof(a,in e,origin.y+c.SideChapelHeight,in c.SideChapelRoof,in ch);
            }
        }

        private static void Rose(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            if(!c.RoseWindowEnabled) return;
            ChurchConfig ch=c.Church;
            StructureFootprintRect local=new StructureFootprintRect(new int2(-ch.NaveWidth/2,ch.Footprint.Primary.Min.y),new int2(ch.NaveWidth,ch.NaveLength));
            Rect r=Resolve(in local,origin,ch.EntryFacing);
            Open(a,in r,ch.NaveWalls.Height,in c.RoseWindow,1,World(Facing.South,in ch),0,0,in ch);
        }

        private static void FrontTowers(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            if(!c.WestFrontTowersEnabled) return;
            FrontTower(a,origin,-c.WestTowerCentreOffset,in c); FrontTower(a,origin,c.WestTowerCentreOffset,in c);
        }

        private static void FrontTower(IStructureAuthoringSession a,int3 origin,int cx,in CathedralConfig c)
        {
            ChurchConfig ch=c.Church; TowerConfig t=c.WestFrontTower; int front=ch.Footprint.Primary.Min.y;
            StructureFootprintRect local=new StructureFootprintRect(new int2(cx-t.Width/2,front),new int2(t.Width,t.Depth));
            Rect r=Resolve(in local,origin,ch.EntryFacing); TowerShell(a,in r,origin.y,in t,in ch);
            Open(a,in r,t.Height,in ch.MainPortal,1,World(Facing.North,in ch),0,0,in ch); TowerWindows(a,in r,in t,in ch);
            Roof(a,in r,origin.y+t.Height,in t.Roof,in ch);
            if(c.WestTowerSpiresEnabled)
            {
                int2 centre=StructureCardinalTransform.Point(new int2(cx,front+t.Depth/2),ch.EntryFacing);
                a.Cone(origin.x+centre.x,origin.y+t.Height+math.max(2,t.Roof.PitchRise/2),origin.z+centre.y,
                    math.max(2,math.min(t.Width,t.Depth)/2),c.WestTowerSpireHeight,ch.Palette.Resolve(t.Roof.MaterialRole));
            }
        }

        private static void CrossingTower(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            if(!c.CrossingTowerEnabled) return;
            ChurchConfig ch=c.Church; TowerConfig t=c.CrossingTower;
            int cz=ch.Footprint.Primary.Min.y+c.TranseptCentreFromNaveFront;
            StructureFootprintRect local=new StructureFootprintRect(new int2(-t.Width/2,cz-t.Depth/2),new int2(t.Width,t.Depth));
            Rect r=Resolve(in local,origin,ch.EntryFacing); int baseY=origin.y+math.max(ch.NaveWalls.Height,c.TranseptHeight); r.Min.y=baseY;
            TowerShell(a,in r,baseY,in t,in ch); TowerWindows(a,in r,in t,in ch); Roof(a,in r,baseY+t.Height,in t.Roof,in ch);
            if(c.CrossingSpireEnabled)
            {
                int2 centre=StructureCardinalTransform.Point(new int2(0,cz),ch.EntryFacing);
                a.Cone(origin.x+centre.x,baseY+t.Height+math.max(2,t.Roof.PitchRise/2),origin.z+centre.y,
                    math.max(2,math.min(t.Width,t.Depth)/2),c.CrossingSpireHeight,ch.Palette.Resolve(t.Roof.MaterialRole));
            }
        }

        private static void Crypt(IStructureAuthoringSession a,int3 origin,in CathedralConfig c)
        {
            if(!c.CryptEnabled) return;
            ChurchConfig ch=c.Church;
            int2 centre=new int2(c.CryptAnchor.LocalPosition.x,c.CryptAnchor.LocalPosition.z);
            StructureFootprintRect local=new StructureFootprintRect(centre-new int2(c.CryptWidth/2,c.CryptDepth/2),new int2(c.CryptWidth,c.CryptDepth));
            Rect r=Resolve(in local,origin,ch.EntryFacing); int bottom=origin.y-c.CryptTopOffset-c.CryptHeight;
            int3 min=new int3(r.Min.x,bottom,r.Min.z), size=new int3(r.Width,c.CryptHeight,r.Depth);
            a.Box(min,size,GameMaterialIds.Empty);
            a.HollowBox(min,size,ch.WallThickness,ch.Palette.Resolve(StructureMaterialRole.Underground),true,true);
        }

        private static void Shell(IStructureAuthoringSession a,in Rect r,int height,in ChurchConfig ch)=>
            a.HollowBox(r.Min,new int3(r.Width,height,r.Depth),ch.WallThickness,ch.Palette.Resolve(ch.NaveWalls.PrimaryMaterial),false,false);

        private static void TowerShell(IStructureAuthoringSession a,in Rect r,int baseY,in TowerConfig t,in ChurchConfig ch)=>
            a.HollowBox(new int3(r.Min.x,baseY,r.Min.z),new int3(r.Width,t.Height,r.Depth),ch.WallThickness,ch.Palette.Resolve(t.WallMaterialRole),false,false);

        private static void TowerWindows(IStructureAuthoringSession a,in Rect r,in TowerConfig t,in ChurchConfig ch)
        {
            if(!t.OpeningsEnabled) return;
            Open(a,in r,t.Height,in t.Opening,1,World(Facing.South,in ch),0,0,in ch);
            Open(a,in r,t.Height,in t.Opening,1,World(Facing.North,in ch),0,0,in ch);
            Open(a,in r,t.Height,in t.Opening,1,World(Facing.West,in ch),0,0,in ch);
            Open(a,in r,t.Height,in t.Opening,1,World(Facing.East,in ch),0,0,in ch);
        }

        private static void Open(IStructureAuthoringSession a,in Rect r,int height,in OpeningConfig opening,int count,Facing facade,int spacing,int offset,in ChurchConfig ch)=>
            SharedOpeningAuthoring.AuthorRepeated(a,r.Min,r.Width,height,r.Depth,ch.WallThickness,in opening,count,facade,offset,spacing,in ch.Palette);

        private static void Roof(IStructureAuthoringSession a,in Rect r,int y,in RoofConfig local,in ChurchConfig ch)
        {
            RoofConfig roof=local; roof.RidgeAxis=StructureCardinalTransform.Axis(local.RidgeAxis,ch.EntryFacing);
            SharedRoofAuthoring.Author(a,r.Min,r.Width,r.Depth,y,in roof,ch.Palette.Resolve(roof.MaterialRole));
        }

        private static Rect Resolve(in StructureFootprintRect local,int3 origin,Facing facing)
        {
            StructureFootprintRect world=StructureCardinalTransform.Rect(in local,facing);
            return new Rect{Min=new int3(origin.x+world.Min.x,origin.y,origin.z+world.Min.y),Width=world.Size.x,Depth=world.Size.y};
        }

        private static Facing World(Facing local,in ChurchConfig ch)=>StructureCardinalTransform.FacingDirection(local,ch.EntryFacing);
    }
}
