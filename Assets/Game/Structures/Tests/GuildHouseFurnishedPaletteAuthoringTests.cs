using System.Collections.Generic;
using Game.Structures.Api;
using Game.Structures.Runtime;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace Game.Structures.Tests
{
    public sealed class GuildHouseFurnishedPaletteAuthoringTests
    {
        [Test]
        public void ExplicitPaletteAuthorsThroughProductionHouseAndDecorationEmitters()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Wizards,
                DecorationRegionTheme.Kentridge,
                0x1234ABCDu,
                712u,
                int3.zero,
                128,
                128,
                requestedRooms: 16);
            Assert.That(
                GuildHouseFurnishingPalette.TryCreate(
                    GuildHouseKind.Wizards,
                    new ushort[] { 127, 233, 400 },
                    out GuildHouseFurnishingPalette palette),
                Is.True);

            var authoring = new RecordingAuthoringSession();
            Assert.That(
                GuildHouseFurnishedPrototypeAuthoring.TryAuthor(
                    authoring,
                    in prototype,
                    in palette,
                    out GuildHouseUnplacedFurnishing[] unplaced),
                Is.True);
            Assert.That(authoring.OperationCount, Is.GreaterThan(0));
            Assert.That(authoring.BoxCount, Is.GreaterThan(0), "production shell/prop emitters should author boxes");
            for (int i = 0; i < unplaced.Length; i++)
                Assert.That(unplaced[i].IsWellFormed, Is.True);
        }

        [Test]
        public void PublicHallAuthorsProductionFacadeWindowsCanopyAndRaisedRoofSilhouette()
        {
            GuildHouseProgram program = GuildHouseProgramCatalog.Get(GuildHouseKind.Knights);
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Knights,
                DecorationRegionTheme.Kentridge,
                0x22002200u,
                713u,
                int3.zero,
                128,
                128,
                requestedRooms: program.MinimumRooms);
            DecorationRegionProfile region = DecorationRegionProfiles.Resolve(DecorationRegionTheme.Kentridge);
            var authoring = new RecordingAuthoringSession();

            GuildHousePrototypeAuthoring.Author(authoring, in prototype);

            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            int roofPlateY = plan.Origin.y + plan.FloorCount * plan.FloorHeight;
            Assert.That(plan.ShellStyle, Is.EqualTo(GuildHouseShellStyle.Hall));
            Assert.That(authoring.HasBox(region.MagicMaterial, box =>
                box.Min.z == plan.Origin.z - 1 && box.Min.y >= plan.Origin.y + 8),
                Is.True,
                "public production facade should expose region-driven window/magic panels");
            Assert.That(authoring.HasBox(region.AccentMaterial, box =>
                box.Min.z <= plan.Origin.z - 7 && box.Min.y > plan.Origin.y + 20),
                Is.True,
                "production entrance should have a facade-connected canopy/trim element");
            Assert.That(authoring.HasBox(region.PrimaryMaterial, box => box.Min.y >= roofPlateY + 5),
                Is.True,
                "hall roof should rise above the legacy flat plate so the exterior has a readable silhouette");
        }

        private readonly struct BoxOperation
        {
            public readonly int3 Min;
            public readonly int3 Size;
            public readonly byte Material;

            public BoxOperation(int3 min, int3 size, byte material)
            {
                Min = min;
                Size = size;
                Material = material;
            }
        }

        private sealed class RecordingAuthoringSession : IStructureAuthoringSession
        {
            private readonly List<BoxOperation> _boxes = new List<BoxOperation>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => OperationCount;
            public int OperationCount { get; private set; }
            public int BoxCount { get; private set; }

            public bool HasBox(byte material, System.Predicate<BoxOperation> predicate)
            {
                for (int i = 0; i < _boxes.Count; i++)
                    if (_boxes[i].Material == material && predicate(_boxes[i]))
                        return true;
                return false;
            }

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => 0;
            public bool IsSolid(int x, int y, int z) => false;

            public void Set(int x, int y, int z, byte material) => OperationCount++;
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) => OperationCount++;
            public void Coat(int x, int y, int z, byte coating) => OperationCount++;
            public void FillBulk(int3 min, int3 size, byte material) => OperationCount++;
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) => OperationCount++;
            public void Box(int3 min, int3 size, byte material)
            {
                OperationCount++;
                BoxCount++;
                _boxes.Add(new BoxOperation(min, size, material));
            }
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling)
            {
                OperationCount++;
                BoxCount++;
            }
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) => OperationCount++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => OperationCount++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => OperationCount++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => OperationCount++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => OperationCount++;
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) => OperationCount++;
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) => OperationCount++;
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) => OperationCount++;
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) => OperationCount++;
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) => OperationCount++;
            public void Carve(int3 min, int3 size) => OperationCount++;
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) => OperationCount++;
        }
    }
}
