using Unity.Mathematics;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Api
{
    public struct CathedralConfig
    {
        public ChurchConfig Church;
        public StructureFootprintConfig Footprint;
        public int TranseptWidth, TranseptDepth, TranseptHeight, TranseptCentreFromNaveFront;
        public RoofConfig TranseptRoof;
        public int CrossingClearanceHeight;
        public int ExtraAisleCountPerSide, ExtraAisleWidth, ExtraAisleHeight;
        public RoofConfig ExtraAisleRoof;
        public OpeningConfig ExtraAisleArch, ExtraAisleWindow;
        public bool SideChapelsEnabled;
        public int SideChapelCountPerSide, SideChapelWidth, SideChapelDepth, SideChapelHeight, SideChapelSpacing;
        public RoofConfig SideChapelRoof;
        public OpeningConfig SideChapelArch;
        public bool WestFrontTowersEnabled;
        public TowerConfig WestFrontTower;
        public int WestTowerCentreOffset;
        public bool WestTowerSpiresEnabled;
        public int WestTowerSpireHeight;
        public bool CrossingTowerEnabled;
        public TowerConfig CrossingTower;
        public bool CrossingSpireEnabled;
        public int CrossingSpireHeight;
        public bool RoseWindowEnabled;
        public OpeningConfig RoseWindow;
        public bool CryptEnabled;
        public int CryptWidth, CryptDepth, CryptHeight, CryptTopOffset;
        public AttachmentAnchorConfig CryptAnchor, CaveAnchor;

        public int BaseAssemblyWidth => Church.NaveWidth + (Church.AislesEnabled ? Church.AisleWidth * 2 : 0);
        public int NaveAssemblyWidth => BaseAssemblyWidth + ExtraAisleCountPerSide * ExtraAisleWidth * 2;
        public int SideChapelAssemblyWidth => SideChapelsEnabled ? Church.SanctuaryWidth + SideChapelDepth * 2 : Church.SanctuaryWidth;
        public int OverallWidth => math.max(math.max(NaveAssemblyWidth, TranseptWidth), SideChapelAssemblyWidth);
        public int OverallLength => Church.OverallLength;

        public bool IsWellFormed
        {
            get
            {
                if (!Church.IsWellFormed || !Footprint.IsWellFormed ||
                    Footprint.Primary.Size.x != OverallWidth || Footprint.Primary.Size.y != OverallLength)
                    return false;
                if (TranseptWidth <= NaveAssemblyWidth || TranseptDepth <= Church.WallThickness * 2 ||
                    TranseptHeight <= Church.WallThickness * 2 || TranseptCentreFromNaveFront < TranseptDepth / 2 ||
                    TranseptCentreFromNaveFront + TranseptDepth / 2 > Church.NaveLength || !TranseptRoof.IsWellFormed ||
                    CrossingClearanceHeight <= 2 || CrossingClearanceHeight >= math.min(TranseptHeight, Church.NaveWalls.Height))
                    return false;
                if (ExtraAisleCountPerSide < 0 || ExtraAisleCountPerSide > 2) return false;
                if (ExtraAisleCountPerSide > 0 && !ExtraAislesWellFormed()) return false;
                if (SideChapelsEnabled && !SideChapelsWellFormed()) return false;
                if (WestFrontTowersEnabled &&
                    (!TowerFits(in WestFrontTower) || WestTowerCentreOffset <= WestFrontTower.Width / 2 ||
                     WestTowerCentreOffset + WestFrontTower.Width / 2 > OverallWidth / 2 ||
                     Church.MainPortal.Width >= TowerInnerWidth(in WestFrontTower) ||
                     (WestTowerSpiresEnabled && WestTowerSpireHeight <= 0))) return false;
                if (CrossingTowerEnabled &&
                    (!TowerFits(in CrossingTower) || CrossingTower.Width > NaveAssemblyWidth || CrossingTower.Depth > TranseptDepth ||
                     (CrossingSpireEnabled && CrossingSpireHeight <= 0))) return false;
                if (RoseWindowEnabled &&
                    (!RoseWindow.IsWellFormed || RoseWindow.Kind != StructureOpeningKind.Window ||
                     RoseWindow.Width >= Church.NaveWidth || RoseWindow.Height + RoseWindow.BottomOffset >= Church.NaveWalls.Height)) return false;
                if (CryptEnabled &&
                    (CryptWidth <= Church.WallThickness * 2 || CryptDepth <= Church.WallThickness * 2 || CryptHeight <= 4 ||
                     CryptTopOffset < 2 || CryptWidth > Church.SanctuaryWidth ||
                     CryptDepth > Church.SanctuaryLength + TranseptDepth ||
                     CryptAnchor.Kind != StructureAttachmentKind.Crypt || CaveAnchor.Kind != StructureAttachmentKind.Cave ||
                     !CryptAnchor.IsWellFormed || !CaveAnchor.IsWellFormed)) return false;
                return true;
            }
        }

        public int3 ResolveCryptAnchor(int3 origin) => ResolveAttachmentPosition(origin, in CryptAnchor);
        public int3 ResolveCaveAnchor(int3 origin) => ResolveAttachmentPosition(origin, in CaveAnchor);
        public Facing ResolveCaveFacing() => StructureCardinalTransform.FacingDirection(CaveAnchor.Facing, Church.EntryFacing);

        private bool ExtraAislesWellFormed() => Church.AislesEnabled &&
            ExtraAisleWidth > Church.WallThickness * 2 && ExtraAisleHeight > Church.WallThickness * 2 && ExtraAisleHeight < Church.AisleHeight &&
            ExtraAisleRoof.IsWellFormed && ExtraAisleArch.IsWellFormed && ExtraAisleArch.Kind == StructureOpeningKind.Arch &&
            ExtraAisleArch.Height + ExtraAisleArch.BottomOffset < ExtraAisleHeight && ExtraAisleArch.MaxCountForSpan(Church.NaveLength) > 0 &&
            ExtraAisleWindow.IsWellFormed && ExtraAisleWindow.Kind == StructureOpeningKind.Window &&
            ExtraAisleWindow.Height + ExtraAisleWindow.BottomOffset < ExtraAisleHeight && ExtraAisleWindow.MaxCountForSpan(Church.NaveLength) > 0;

        private bool SideChapelsWellFormed()
        {
            if (SideChapelCountPerSide < 1 || SideChapelCountPerSide > 8 || SideChapelWidth <= Church.WallThickness * 2 ||
                SideChapelDepth <= Church.WallThickness * 2 || SideChapelHeight <= Church.WallThickness * 2 ||
                SideChapelSpacing < SideChapelWidth || !SideChapelRoof.IsWellFormed || !SideChapelArch.IsWellFormed ||
                SideChapelArch.Kind != StructureOpeningKind.Arch || SideChapelArch.Width >= SideChapelWidth ||
                SideChapelArch.Height + SideChapelArch.BottomOffset >= SideChapelHeight) return false;
            long groupLength = SideChapelWidth + (long)(SideChapelCountPerSide - 1) * SideChapelSpacing;
            return groupLength <= Church.SanctuaryLength - Church.WallThickness * 2;
        }

        private bool TowerFits(in TowerConfig tower)
        {
            if (!tower.IsWellFormed || tower.Shape != StructureTowerShape.Square ||
                tower.Placement != StructureTowerPlacement.Explicit || tower.TopStyle != StructureTowerTopStyle.Roof || tower.Count != 1)
                return false;
            return !tower.OpeningsEnabled ||
                   (tower.Opening.Width < TowerInnerWidth(in tower) && tower.Opening.Height + tower.Opening.BottomOffset < tower.Height);
        }

        private int TowerInnerWidth(in TowerConfig tower) => math.min(tower.Width, tower.Depth) - Church.WallThickness * 2;

        private int3 ResolveAttachmentPosition(int3 origin, in AttachmentAnchorConfig attachment)
        {
            int2 rotated = StructureCardinalTransform.Point(new int2(attachment.LocalPosition.x, attachment.LocalPosition.z), Church.EntryFacing);
            return new int3(origin.x + rotated.x, origin.y + attachment.LocalPosition.y, origin.z + rotated.y);
        }
    }

    public static class CathedralPresets
    {
        public static CathedralConfig Simple(in StructureMaterialPalette palette)
        {
            ChurchConfig church = CathedralChurch(72,150,64,20,40,58,48,56,30,50,26,in palette);
            CathedralConfig c = Base(in church);
            c.TranseptWidth=116; c.TranseptDepth=34; c.TranseptHeight=60; c.TranseptCentreFromNaveFront=112;
            c.TranseptRoof=Roof(RoofStyle.Gable,RoofAxis.X,16,24); c.CrossingClearanceHeight=42;
            c.WestFrontTowersEnabled=true; c.WestFrontTower=Tower(30,30,82,in church.Window); c.WestTowerCentreOffset=22;
            c.RoseWindowEnabled=true; c.RoseWindow=Rose(22,22,32); RebuildFootprint(ref c); return c;
        }

        public static CathedralConfig Gothic(in StructureMaterialPalette palette)
        {
            ChurchConfig church = CathedralChurch(88,220,86,24,52,70,70,72,38,68,36,in palette);
            church.MainPortal=Door(20,40,4); church.Window=Window(12,26,14,30,16); church.ClerestoryWindow=Window(10,18,58,28,16);
            CathedralConfig c=Base(in church);
            c.TranseptWidth=190; c.TranseptDepth=42; c.TranseptHeight=80; c.TranseptCentreFromNaveFront=164;
            c.TranseptRoof=Roof(RoofStyle.Gable,RoofAxis.X,22,28); c.CrossingClearanceHeight=58;
            c.ExtraAisleCountPerSide=1; c.ExtraAisleWidth=18; c.ExtraAisleHeight=38;
            c.ExtraAisleRoof=Roof(RoofStyle.Shed,RoofAxis.Z,8,24); c.ExtraAisleArch=Arch(12,26,28,16); c.ExtraAisleWindow=Window(9,16,10,28,16);
            c.SideChapelsEnabled=true; c.SideChapelCountPerSide=3; c.SideChapelWidth=18; c.SideChapelDepth=20; c.SideChapelHeight=36; c.SideChapelSpacing=20;
            c.SideChapelRoof=Roof(RoofStyle.Gable,RoofAxis.X,10,18); c.SideChapelArch=Arch(10,24,0,0);
            c.WestFrontTowersEnabled=true; c.WestFrontTower=Tower(36,36,112,in church.Window); c.WestTowerCentreOffset=31;
            c.WestTowerSpiresEnabled=true; c.WestTowerSpireHeight=58;
            c.CrossingTowerEnabled=true; c.CrossingTower=Tower(44,40,66,in church.Window); c.CrossingSpireEnabled=true; c.CrossingSpireHeight=62;
            c.RoseWindowEnabled=true; c.RoseWindow=Rose(28,28,46);
            c.CryptEnabled=true; c.CryptWidth=56; c.CryptDepth=54; c.CryptHeight=18; c.CryptTopOffset=6;
            int frontZ=church.Footprint.Primary.Min.y; int sanctuaryCentreZ=frontZ+church.NaveLength+church.SanctuaryLength/2;
            c.CryptAnchor=new AttachmentAnchorConfig{Kind=StructureAttachmentKind.Crypt,LocalPosition=new int3(0,-c.CryptTopOffset-2,sanctuaryCentreZ),Facing=Facing.Down,SnapToGround=false};
            c.CaveAnchor=new AttachmentAnchorConfig{Kind=StructureAttachmentKind.Cave,LocalPosition=new int3(0,-c.CryptTopOffset-c.CryptHeight/2,sanctuaryCentreZ+c.CryptDepth/2-church.WallThickness-1),Facing=Facing.North,SnapToGround=false};
            RebuildFootprint(ref c); return c;
        }

        private static CathedralConfig Base(in ChurchConfig church)=>new CathedralConfig{
            Church=church,ExtraAisleCountPerSide=0,ExtraAisleWidth=16,ExtraAisleHeight=32,
            ExtraAisleRoof=Roof(RoofStyle.Shed,RoofAxis.Z,8,24),ExtraAisleArch=Arch(10,22,28,14),ExtraAisleWindow=Window(8,14,10,28,14),
            SideChapelCountPerSide=1,SideChapelWidth=18,SideChapelDepth=18,SideChapelHeight=32,SideChapelSpacing=20,
            SideChapelRoof=Roof(RoofStyle.Gable,RoofAxis.X,10,18),SideChapelArch=Arch(10,22,0,0),WestTowerSpireHeight=48,CrossingSpireHeight=52,
            RoseWindow=Rose(20,20,30),CryptAnchor=new AttachmentAnchorConfig{Kind=StructureAttachmentKind.Crypt,LocalPosition=new int3(0,-8,0),Facing=Facing.Down,SnapToGround=false},
            CaveAnchor=new AttachmentAnchorConfig{Kind=StructureAttachmentKind.Cave,LocalPosition=new int3(0,-14,0),Facing=Facing.North,SnapToGround=false}};

        private static ChurchConfig CathedralChurch(int naveWidth,int naveLength,int naveHeight,int aisleWidth,int aisleHeight,int sanctuaryWidth,int sanctuaryLength,int sanctuaryHeight,int apseRadius,int apseHeight,int apseRoofHeight,in StructureMaterialPalette palette)
        {
            ChurchConfig church=ChurchPresets.ParishChurch(in palette);
            church.NaveWalls.Length=naveWidth; church.NaveWalls.Height=naveHeight; church.NaveLength=naveLength; church.AisleWidth=aisleWidth; church.AisleHeight=aisleHeight;
            church.SanctuaryWidth=sanctuaryWidth; church.SanctuaryLength=sanctuaryLength; church.SanctuaryHeight=sanctuaryHeight;
            church.ApseEnabled=true; church.ApseRadius=apseRadius; church.ApseHeight=apseHeight; church.ApseRoofHeight=apseRoofHeight;
            church.BellTowerPlacement=ChurchBellTowerPlacement.None; church.SpireEnabled=false; church.MainPortal=Door(18,34,3);
            church.Window=Window(10,22,12,28,14); church.ClerestoryEnabled=true; church.ClerestoryWindow=Window(9,14,aisleHeight+8,28,14);
            church.AisleArch.Height=math.min(aisleHeight-8,30); church.SanctuaryArch.Width=math.min(32,sanctuaryWidth-church.WallThickness*2-2);
            church.SanctuaryArch.Height=math.min(40,math.min(naveHeight,sanctuaryHeight)-10);
            church.Footprint.Primary=new StructureFootprintRect(new int2(-church.OverallWidth/2,-church.OverallLength/2),new int2(church.OverallWidth,church.OverallLength)); return church;
        }

        private static void RebuildFootprint(ref CathedralConfig c)=>c.Footprint=new StructureFootprintConfig{
            Primary=new StructureFootprintRect(new int2(-c.OverallWidth/2,c.Church.Footprint.Primary.Min.y),new int2(c.OverallWidth,c.OverallLength)),
            BasePlane=BasePlaneRule.FixedAltitude,FoundationStyle=StructureFoundationStyle.Slab,FoundationDepth=c.Church.Footprint.FoundationDepth,FoundationMaterial=StructureMaterialRole.Foundation};
        private static TowerConfig Tower(int width,int depth,int height,in OpeningConfig opening)=>new TowerConfig{Shape=StructureTowerShape.Square,Placement=StructureTowerPlacement.Explicit,TopStyle=StructureTowerTopStyle.Roof,Width=width,Depth=depth,Height=height,TaperPercent=0,Count=1,Spacing=0,Roof=Roof(RoofStyle.Gable,RoofAxis.Z,18,24),OpeningsEnabled=true,Opening=opening,WallMaterialRole=StructureMaterialRole.PrimaryWall,TrimMaterialRole=StructureMaterialRole.Trim};
        private static OpeningConfig Door(int width,int height,int frame)=>new OpeningConfig{Kind=StructureOpeningKind.Door,Width=width,Height=height,BottomOffset=0,Spacing=0,StartMargin=0,EndMargin=0,FrameThickness=frame,LintelThickness=frame,FrameMaterialRole=StructureMaterialRole.Trim,FillMaterialRole=StructureMaterialRole.Opening};
        private static OpeningConfig Window(int width,int height,int bottom,int spacing,int margin)=>new OpeningConfig{Kind=StructureOpeningKind.Window,Width=width,Height=height,BottomOffset=bottom,Spacing=spacing,StartMargin=margin,EndMargin=margin,FrameThickness=2,LintelThickness=2,FrameMaterialRole=StructureMaterialRole.Trim,FillMaterialRole=StructureMaterialRole.Glass};
        private static OpeningConfig Rose(int width,int height,int bottom)=>Window(width,height,bottom,0,0);
        private static OpeningConfig Arch(int width,int height,int spacing,int margin)=>new OpeningConfig{Kind=StructureOpeningKind.Arch,Width=width,Height=height,BottomOffset=0,Spacing=spacing,StartMargin=margin,EndMargin=margin,FrameThickness=1,LintelThickness=1,FrameMaterialRole=StructureMaterialRole.Trim,FillMaterialRole=StructureMaterialRole.Opening};
        private static RoofConfig Roof(RoofStyle style,RoofAxis axis,int rise,int run)=>new RoofConfig{Style=style,RidgeAxis=axis,PitchRise=rise,PitchRun=run,EaveOverhang=style==RoofStyle.Flat?2:4,Thickness=2,ParapetHeight=0,MaterialRole=StructureMaterialRole.Roof,TrimMaterialRole=StructureMaterialRole.Trim};
    }
}
