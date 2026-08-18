using System.Collections.Generic;
using Game.Materials.Api;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class TempleConfigTests
    {
        [Test]
        public void ClassicalAndCourtyardPresetsAreValidAndDifferent()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig classical = TemplePresets.ClassicalColumned(in palette);
            TempleConfig courtyard = TemplePresets.CourtyardTemple(in palette);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(classical.IsWellFormed);
                Assert.IsTrue(courtyard.IsWellFormed);
                Assert.IsTrue(classical.ColonnadeEnabled);
                Assert.IsTrue(courtyard.ColonnadeEnabled);
                Assert.IsFalse(classical.CourtyardEnabled);
                Assert.IsTrue(courtyard.CourtyardEnabled);
                Assert.AreEqual(RoofStyle.Gable, classical.SanctuaryRoof.Style);
                Assert.AreEqual(RoofStyle.Hip, courtyard.SanctuaryRoof.Style);
                Assert.AreNotEqual(classical.PlatformWidth, courtyard.PlatformWidth);
                Assert.AreNotEqual(classical.PlatformDepth, courtyard.PlatformDepth);
            });
        }

        [TestCase(Facing.South)]
        [TestCase(Facing.East)]
        [TestCase(Facing.North)]
        [TestCase(Facing.West)]
        public void ClassicalTempleIsDeterministicForEveryCardinalOrientation(Facing facing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig config = TemplePresets.ClassicalColumned(in palette);
            config.EntryFacing = facing;
            var a = new RecordingSession();
            var b = new RecordingSession();

            TempleAuthoring.Author(a, new int3(100, 30, -200), in config);
            TempleAuthoring.Author(b, new int3(100, 30, -200), in config);

            Assert.Multiple(() =>
            {
                Assert.IsTrue(config.IsWellFormed);
                CollectionAssert.AreEqual(a.Operations, b.Operations);
                Assert.That(a.Cylinders, Is.GreaterThan(8));
                Assert.That(a.Boxes, Is.GreaterThan(config.PlatformHeight));
            });
        }

        [TestCase(Facing.South)]
        [TestCase(Facing.East)]
        public void SanctuaryInteriorIsReachableFromMainDoor(Facing facing)
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig config = TemplePresets.ClassicalColumned(in palette);
            config.EntryFacing = facing;
            const int navY = 50;
            var origin = new int3(160, 40, -160);
            var session = new SliceSession(navY);

            TempleAuthoring.Author(session, origin, in config);

            int localFront = -config.SanctuaryDepth / 2;
            int2 outsideLocal = new int2(0, localFront - 1);
            int2 insideLocal = new int2(0, localFront + config.WallThickness + 1);
            int2 outside = WorldXZ(origin, StructureCardinalTransform.Point(outsideLocal, facing));
            int2 inside = WorldXZ(origin, StructureCardinalTransform.Point(insideLocal, facing));

            Assert.Multiple(() =>
            {
                Assert.IsFalse(session.Solid.Contains(outside));
                Assert.IsFalse(session.Solid.Contains(inside));
                Assert.IsTrue(IsReachable(session.Solid, outside, inside, origin, config, facing));
            });
        }

        [Test]
        public void ValidationRejectsUnsupportedOrImpossibleTempleComposition()
        {
            StructureMaterialPalette palette = CastleStructurePalette.Compatibility;
            TempleConfig valid = TemplePresets.CourtyardTemple(in palette);
            TempleConfig badFacing = valid;
            badFacing.EntryFacing = Facing.Up;
            TempleConfig badDoor = valid;
            badDoor.SanctuaryDoor.Width = badDoor.SanctuaryWidth;
            TempleConfig badCourtyard = valid;
            badCourtyard.CourtyardWidth = badCourtyard.SanctuaryWidth;
            TempleConfig badColumns = valid;
            badColumns.Columns.Spacing = badColumns.Columns.Width - 1;

            Assert.Multiple(() =>
            {
                Assert.IsTrue(valid.IsWellFormed);
                Assert.IsFalse(badFacing.IsWellFormed);
                Assert.IsFalse(badDoor.IsWellFormed);
                Assert.IsFalse(badCourtyard.IsWellFormed);
                Assert.IsFalse(badColumns.IsWellFormed);
            });
        }

        private static int2 WorldXZ(int3 origin, int2 local) => new int2(origin.x + local.x, origin.z + local.y);

        private static bool IsReachable(HashSet<int2> solid, int2 start, int2 target,
            int3 origin, in TempleConfig config, Facing facing)
        {
            StructureFootprintRect world = StructureCardinalTransform.Rect(in config.Footprint.Primary, facing);
            int2 min = new int2(origin.x + world.Min.x - config.ApproachStairs.TotalRun - 4,
                origin.z + world.Min.y - config.ApproachStairs.TotalRun - 4);
            int2 max = new int2(origin.x + world.Min.x + world.Size.x + config.ApproachStairs.TotalRun + 4,
                origin.z + world.Min.y + world.Size.y + config.ApproachStairs.TotalRun + 4);
            var queue = new Queue<int2>();
            var visited = new HashSet<int2> { start };
            queue.Enqueue(start);
            int2[] steps = { new int2(1,0), new int2(-1,0), new int2(0,1), new int2(0,-1) };
            while (queue.Count > 0)
            {
                int2 cur = queue.Dequeue();
                if (cur.Equals(target)) return true;
                foreach (int2 step in steps)
                {
                    int2 next = cur + step;
                    if (next.x < min.x || next.x > max.x || next.y < min.y || next.y > max.y ||
                        solid.Contains(next) || !visited.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            return false;
        }

        private sealed class RecordingSession : IStructureAuthoringSession
        {
            public readonly List<string> Operations = new List<string>();
            public int Boxes { get; private set; }
            public int Cylinders { get; private set; }
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => Operations.Count;
            public byte Get(int x,int y,int z)=>0;
            public byte GetCoating(int x,int y,int z)=>0;
            public bool IsSolid(int x,int y,int z)=>false;
            public void Set(int x,int y,int z,byte material){}
            public void SetStyled(int x,int y,int z,byte material,ushort surfaceStyle,byte coating=Coatings.None,VoxelSurfaceFlags flags=VoxelSurfaceFlags.None){}
            public void Coat(int x,int y,int z,byte coating){}
            public void FillBulk(int3 min,int3 size,byte material)=>Box(min,size,material);
            public void FillColumnBulk(int x,int minY,int maxYExclusive,int z,byte material){}
            public void Box(int3 min,int3 size,byte material){Boxes++;Operations.Add($"box:{min}:{size}:{material}");}
            public void HollowBox(int3 min,int3 size,int thickness,byte material,bool floor,bool ceiling)=>Operations.Add($"hollow:{min}:{size}:{thickness}:{material}");
            public void Cylinder(int cx,int baseY,int cz,int radius,int height,byte material,int innerRadius=0){Cylinders++;Operations.Add($"cyl:{cx}:{baseY}:{cz}:{radius}:{height}:{material}");}
            public void Disc(int cx,int y,int cz,int radius,byte material){}
            public void Cone(int cx,int baseY,int cz,int radius,int height,byte material){}
            public void HangingCone(int cx,int ceilingY,int cz,int radius,int height,byte material){}
            public void Gable(int3 min,int3 size,bool alongX,byte material)=>Operations.Add($"gable:{min}:{size}:{alongX}:{material}");
            public void Crenellate(int3 start,int3 step,int count,int width,int height,int merlon,int gap,byte material){}
            public void CrenellateRing(int cx,int y,int cz,int radius,int height,byte material){}
            public void Arch(int3 min,int width,int height,int depth,int depthAxis,byte material){}
            public void Stairs(int3 min,int width,int steps,int rise,int run,int axis,byte material){}
            public void SpiralStair(int cx,int baseY,int cz,int radius,int height,byte material){}
            public void Carve(int3 min,int3 size){}
            public void Weather(int3 min,int3 size,byte coating,uint seed,int chanceOutOf100){}
        }

        private sealed class SliceSession : IStructureAuthoringSession
        {
            private readonly int _y;
            public readonly HashSet<int2> Solid = new HashSet<int2>();
            public SliceSession(int y)=>_y=y;
            public bool BudgetExceeded=>false; public int WriteBudget=>int.MaxValue; public long TotalVoxelsWritten=>Solid.Count;
            public byte Get(int x,int y,int z)=>0; public byte GetCoating(int x,int y,int z)=>0; public bool IsSolid(int x,int y,int z)=>Solid.Contains(new int2(x,z));
            public void Set(int x,int y,int z,byte material){if(y==_y)Apply(new int2(x,z),material);} public void SetStyled(int x,int y,int z,byte material,ushort style,byte coating=Coatings.None,VoxelSurfaceFlags flags=VoxelSurfaceFlags.None)=>Set(x,y,z,material);
            public void Coat(int x,int y,int z,byte coating){} public void FillBulk(int3 min,int3 size,byte material)=>Box(min,size,material);
            public void FillColumnBulk(int x,int minY,int maxYExclusive,int z,byte material){if(_y>=minY&&_y<maxYExclusive)Apply(new int2(x,z),material);}
            public void Box(int3 min,int3 size,byte material){if(_y<min.y||_y>=min.y+size.y)return;for(int z=min.z;z<min.z+size.z;z++)for(int x=min.x;x<min.x+size.x;x++)Apply(new int2(x,z),material);}
            public void HollowBox(int3 min,int3 size,int t,byte material,bool floor,bool ceiling){if(_y<min.y||_y>=min.y+size.y)return;for(int z=min.z;z<min.z+size.z;z++)for(int x=min.x;x<min.x+size.x;x++)if(x<min.x+t||x>=min.x+size.x-t||z<min.z+t||z>=min.z+size.z-t)Apply(new int2(x,z),material);}
            public void Cylinder(int cx,int baseY,int cz,int radius,int height,byte material,int innerRadius=0){if(_y<baseY||_y>=baseY+height)return;int r2=radius*radius;for(int z=cz-radius;z<=cz+radius;z++)for(int x=cx-radius;x<=cx+radius;x++){int dx=x-cx,dz=z-cz;if(dx*dx+dz*dz<=r2)Apply(new int2(x,z),material);}}
            public void Disc(int cx,int y,int cz,int radius,byte material){} public void Cone(int cx,int baseY,int cz,int radius,int height,byte material){} public void HangingCone(int cx,int ceilingY,int cz,int radius,int height,byte material){} public void Gable(int3 min,int3 size,bool alongX,byte material){} public void Crenellate(int3 start,int3 step,int count,int width,int height,int merlon,int gap,byte material){} public void CrenellateRing(int cx,int y,int cz,int radius,int height,byte material){} public void Arch(int3 min,int width,int height,int depth,int depthAxis,byte material){} public void Stairs(int3 min,int width,int steps,int rise,int run,int axis,byte material){} public void SpiralStair(int cx,int baseY,int cz,int radius,int height,byte material){} public void Carve(int3 min,int3 size)=>Box(min,size,GameMaterialIds.Empty); public void Weather(int3 min,int3 size,byte coating,uint seed,int chanceOutOf100){}
            private void Apply(int2 p,byte material){if(material==GameMaterialIds.Empty)Solid.Remove(p);else Solid.Add(p);}
        }
    }
}
