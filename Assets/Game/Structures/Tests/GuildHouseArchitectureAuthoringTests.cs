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
    public sealed class GuildHouseArchitectureAuthoringTests
    {
        [Test]
        public void MultiStoreyHallAuthorsOccupancyBackedStairsAndLeavesUpperStairwellOpen()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Adventurers,
                DecorationRegionTheme.Kentridge,
                17u,
                0x1111u,
                new int3(0, 16, 0),
                128,
                128,
                requestedRooms: 5,
                requestedStoreys: 2,
                requestedFloorHeight: 30);
            DecorationRegionProfile region = DecorationRegionProfiles.Resolve(DecorationRegionTheme.Kentridge);
            var authoring = new RecordingAuthoringSession();

            GuildHousePrototypeAuthoring.Author(authoring, in prototype);

            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            int upperFloorY = plan.Origin.y + plan.FloorHeight;
            Assert.That(plan.FloorCount, Is.EqualTo(2));
            Assert.That(authoring.HasBox(box =>
                    box.Material == region.SecondaryMaterial &&
                    box.Min.y == plan.Origin.y + 2 &&
                    box.Size.x == 10 &&
                    box.Size.y == plan.FloorHeight &&
                    box.Size.z == 1),
                Is.True,
                "the top stair tread must reach the upper finished-floor elevation using solid occupancy");
            Assert.That(authoring.HasBox(box =>
                    box.Material == region.SecondaryMaterial &&
                    box.Min.y == upperFloorY &&
                    math.all(box.Size == new int3(plan.Width, 2, plan.Depth))),
                Is.False,
                "upper storey slab must reserve a stairwell instead of sealing circulation");
            Assert.That(authoring.CountBoxes(box =>
                    box.Material == region.SecondaryMaterial &&
                    box.Min.y == upperFloorY &&
                    box.Size.y == 2),
                Is.GreaterThanOrEqualTo(4),
                "upper floor should be authored as production slab regions around the stairwell");
        }

        [Test]
        public void ExplicitTwoStoreyLodgeAuthorsBothShellLevelsAndOneStairFlight()
        {
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                GuildHouseKind.Druids,
                DecorationRegionTheme.Kentridge,
                23u,
                0x2222u,
                int3.zero,
                120,
                112,
                requestedRooms: 5,
                requestedStoreys: 2,
                requestedFloorHeight: 28);
            DecorationRegionProfile region = DecorationRegionProfiles.Resolve(DecorationRegionTheme.Kentridge);
            var authoring = new RecordingAuthoringSession();

            GuildHousePrototypeAuthoring.Author(authoring, in prototype);

            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            Assert.That(plan.ShellStyle, Is.EqualTo(GuildHouseShellStyle.Lodge));
            Assert.That(plan.FloorCount, Is.EqualTo(2));
            for (int floor = 0; floor < 2; floor++)
            {
                int wallY = plan.Origin.y + floor * plan.FloorHeight + 2;
                Assert.That(authoring.HasBox(box =>
                        box.Material == region.PrimaryMaterial &&
                        box.Min.x == plan.Origin.x &&
                        box.Min.y == wallY &&
                        math.all(box.Size == new int3(3, plan.FloorHeight - 2, plan.Depth))),
                    Is.True,
                    $"lodge shell level {floor} should be authored from the planned storey count");
            }
            Assert.That(authoring.HasBox(box =>
                    box.Material == region.SecondaryMaterial &&
                    box.Min.y == plan.Origin.y + 2 &&
                    box.Size.x == 10 &&
                    box.Size.y == plan.FloorHeight &&
                    box.Size.z == 1),
                Is.True);
        }

        [Test]
        public void GenericProviderAuthorsSdfGlassOpeningsAndMatchingCirculationThroughProductionShell()
        {
            Assert.That(
                ArchitectureRegistry.TryGetProvider(GuildHouseArchitectureProvider.ProviderKey, out IArchitectureProvider provider),
                Is.True);
            var request = new ArchitectureGenerationRequest(
                "kentridge",
                GuildHouseArchitectureProvider.ProviderKey,
                "adventurers",
                31u,
                0x3333u,
                new int3(0, 16, 0),
                new ArchitectureGenerationParameters(
                    width: 128,
                    depth: 128,
                    storeyCount: 2,
                    floorHeight: 30,
                    roomCount: 5));
            var authoring = new RecordingAuthoringSession();

            Assert.That(provider.TryAuthor(authoring, in request, out ArchitectureGenerationResult result, out string error),
                Is.True, error);
            Assert.That(authoring.BoxCount, Is.GreaterThan(0));
            Assert.That(authoring.RoundedBoxCount, Is.EqualTo(1),
                "the production provider should exercise the rounded/SDF facade path");
            Assert.That(authoring.CarveCount, Is.EqualTo(result.Summary.WindowCount),
                "every semantic window must be a real aperture through the authoritative wall occupancy");
            Assert.That(authoring.CountBoxes(box => box.Material == GameMaterialIds.Glass),
                Is.EqualTo(result.Summary.WindowCount),
                "every aperture must receive the production Glass material rather than a fake lit panel");
            Assert.That(result.Summary.StoreyCount, Is.EqualTo(2));
            Assert.That(result.Summary.StairCount, Is.EqualTo(1));
            Assert.That(
                (result.Summary.Capabilities & ArchitecturePresentationCapabilities.CollisionFromOccupancy) != 0,
                Is.True);
            Assert.That(
                (result.Summary.Capabilities & ArchitecturePresentationCapabilities.MultiStoreyCirculation) != 0,
                Is.True);
            Assert.That(
                (result.Summary.Capabilities & ArchitecturePresentationCapabilities.SignedDistancePresentation) != 0,
                Is.True);
            Assert.That(
                (result.Summary.Capabilities & ArchitecturePresentationCapabilities.ProductionMaterialTextures) != 0,
                Is.True);
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

        private sealed class RecordingAuthoringSession : ICurvedStructureAuthoringSession
        {
            private readonly List<BoxOperation> _boxes = new List<BoxOperation>();

            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => OperationCount;
            public int OperationCount { get; private set; }
            public int BoxCount { get; private set; }
            public int RoundedBoxCount { get; private set; }
            public int CarveCount { get; private set; }

            public bool HasBox(System.Predicate<BoxOperation> predicate)
            {
                for (int i = 0; i < _boxes.Count; i++)
                    if (predicate(_boxes[i]))
                        return true;
                return false;
            }

            public int CountBoxes(System.Predicate<BoxOperation> predicate)
            {
                int count = 0;
                for (int i = 0; i < _boxes.Count; i++)
                    if (predicate(_boxes[i]))
                        count++;
                return count;
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
            public void RoundedBox(int3 min, int3 size, int radius, byte material,
                ushort surfaceStyle = SurfaceStyles.ArchitecturalRounded,
                byte coating = Coatings.None,
                VoxelSurfaceFlags flags = VoxelSurfaceFlags.PreserveFeature)
            {
                OperationCount++;
                RoundedBoxCount++;
                _boxes.Add(new BoxOperation(min, size, material));
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
            public void Carve(int3 min, int3 size)
            {
                OperationCount++;
                CarveCount++;
            }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) => OperationCount++;
        }
    }
}
