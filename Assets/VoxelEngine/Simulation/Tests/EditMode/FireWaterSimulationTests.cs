using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Composition;
using VoxelEngine.Rendering.Runtime;
using VoxelEngine.Simulation.Api;
using VoxelEngine.Simulation.Runtime;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class FireWaterSimulationTests
    {
        private const byte Stone = 1;
        private const byte Wood = 2;

        private RegionTable _table;
        private BrickPool _pool;
        private RegionReadSource _reads;
        private RegionMutationStore _mutations;
        private MaterialPalette _palette;

        [SetUp]
        public void SetUp()
        {
            _pool = new BrickPool(256, Allocator.Temp);
            _table = new RegionTable(4, Allocator.Temp);
            _table.LoadRegion(int3.zero);
            _reads = new RegionReadSource(in _table, in _pool);
            _mutations = new RegionMutationStore(in _table, in _pool);

            _palette = default;
            _palette.Register(Stone, 255, DestructionClass.Crumble);
            _palette.Register(Wood, 64, DestructionClass.Splinter);
        }

        [TearDown]
        public void TearDown()
        {
            _table.Dispose();
            _pool.Dispose();
        }

        [Test]
        public void CompositionCreatesFireWaterThroughApiContract()
        {
            MaterialSimulationView materials = _palette.SimulationView;

            IFireWaterSimulation simulation = SimulationComposition.CreateFireWater(
                _reads,
                _mutations,
                in materials);

            Assert.NotNull(simulation);
            Assert.AreEqual(FireWaterConfig.Default.FireLifetimeTicks,
                simulation.Config.FireLifetimeTicks);
        }

        [Test]
        public void SplinterMaterialCanBurnWithoutChangingDestructionBehavior()
        {
            Assert.AreEqual(DestructionClass.Splinter, _palette.GetDestructionClass(Wood));
            Assert.IsTrue(_palette.IsFlammable(Wood));

            _palette.SetFlammable(Wood, false);

            Assert.AreEqual(DestructionClass.Splinter, _palette.GetDestructionClass(Wood));
            Assert.IsFalse(_palette.IsFlammable(Wood));
            Assert.IsFalse(_palette.SimulationView.IsFlammable(Wood));
        }

        [Test]
        public void FireCoatingHasHotVisiblePresentation()
        {
            Assert.Greater(VoxelPresentationCatalogue.CoatingTint[Coatings.Fire].x, 1f);
            Assert.Greater(VoxelPresentationCatalogue.CoatingSampling[Coatings.Fire].w, 0.5f);
        }

        [Test]
        public void IgniteRequiresFlammableMaterialAndAppliesFireCoating()
        {
            int3 wood = new int3(0, 0, 0);
            int3 stone = new int3(1, 0, 0);
            SetCell(wood, new VoxelCell { BaseMaterialId = Wood });
            SetCell(stone, new VoxelCell { BaseMaterialId = Stone });
            FireWaterSimulation simulation = CreateSimulation();

            Assert.IsTrue(simulation.Ignite(wood));
            Assert.IsFalse(simulation.Ignite(stone));

            Assert.IsTrue(_reads.TryRead(wood, out VoxelCell burning));
            Assert.AreEqual(Wood, burning.BaseMaterialId);
            Assert.AreEqual(Coatings.Fire, burning.Surface.CoatingId);
            Assert.IsTrue(simulation.IsBurning(wood));
        }

        [Test]
        public void FireSpreadsToAdjacentFlammableVoxel()
        {
            int3 first = new int3(0, 0, 0);
            int3 second = new int3(1, 0, 0);
            SetCell(first, new VoxelCell { BaseMaterialId = Wood });
            SetCell(second, new VoxelCell { BaseMaterialId = Wood });

            FireWaterConfig config = FireWaterConfig.Default;
            config.FireSpreadIntervalTicks = 1;
            config.FireSpreadChancePercent = 100;
            config.FireLifetimeTicks = 10;
            FireWaterSimulation simulation = CreateSimulation(config);

            Assert.IsTrue(simulation.Ignite(first));
            simulation.Tick();

            Assert.IsTrue(simulation.IsBurning(second));
            Assert.IsTrue(_reads.TryRead(second, out VoxelCell burning));
            Assert.AreEqual(Coatings.Fire, burning.Surface.CoatingId);
        }

        [Test]
        public void BurningVoxelBurnsOutAndBecomesEmpty()
        {
            int3 wood = new int3(0, 0, 0);
            SetCell(wood, new VoxelCell { BaseMaterialId = Wood });

            FireWaterConfig config = FireWaterConfig.Default;
            config.FireLifetimeTicks = 1;
            config.FireSpreadChancePercent = 0;
            FireWaterSimulation simulation = CreateSimulation(config);

            simulation.Ignite(wood);
            simulation.Tick();

            Assert.IsFalse(simulation.IsBurning(wood));
            Assert.IsTrue(_reads.TryRead(wood, out VoxelCell burned));
            Assert.AreEqual(VoxelGrid.MaterialEmpty, burned.BaseMaterialId);
        }

        [Test]
        public void WaterFallsBeforeItSpreadsSideways()
        {
            int3 source = new int3(0, 2, 0);
            int3 below = new int3(0, 1, 0);
            SetCell(new int3(0, 0, 0), new VoxelCell { BaseMaterialId = Stone });
            FireWaterSimulation simulation = CreateSimulation();

            Assert.IsTrue(simulation.AddWaterSource(source));
            simulation.Tick();

            AssertMaterial(source, simulation.Config.WaterMaterial);
            AssertMaterial(below, simulation.Config.CascadeMaterial);
            AssertMaterial(new int3(1, 2, 0), VoxelGrid.MaterialEmpty);
        }

        [Test]
        public void SupportedWaterSpreadsAcrossHorizontalNeighboursWithLowerLevel()
        {
            int3 source = new int3(0, 1, 0);
            SetCell(new int3(0, 0, 0), new VoxelCell { BaseMaterialId = Stone });
            FireWaterSimulation simulation = CreateSimulation();

            simulation.AddWaterSource(source);
            simulation.Tick();

            int3 neighbour = new int3(1, 1, 0);
            AssertMaterial(neighbour, simulation.Config.WaterMaterial);
            Assert.IsTrue(simulation.TryGetWaterState(neighbour, out WaterVoxelState state));
            Assert.AreEqual(simulation.Config.WaterMaxLevel - 1, state.Level);
            Assert.IsFalse(state.IsFalling);
        }

        [Test]
        public void WaterExtinguishesAdjacentFireAndRestoresSurface()
        {
            int3 source = new int3(0, 1, 0);
            int3 wood = new int3(1, 1, 0);
            SetCell(wood, new VoxelCell
            {
                BaseMaterialId = Wood,
                Surface = new VoxelSurfaceSemantics { CoatingId = Coatings.Moss }
            });
            FireWaterSimulation simulation = CreateSimulation();

            simulation.Ignite(wood);
            simulation.AddWaterSource(source);
            simulation.Tick();

            Assert.IsFalse(simulation.IsBurning(wood));
            Assert.IsTrue(_reads.TryRead(wood, out VoxelCell extinguished));
            Assert.AreEqual(Wood, extinguished.BaseMaterialId);
            Assert.AreEqual(Coatings.Moss, extinguished.Surface.CoatingId);
        }

        [Test]
        public void RemovingSourceLetsUnfedWaterRecede()
        {
            int3 source = new int3(0, 1, 0);
            SetCell(new int3(0, 0, 0), new VoxelCell { BaseMaterialId = Stone });
            FireWaterSimulation simulation = CreateSimulation();

            simulation.AddWaterSource(source);
            simulation.Tick();
            Assert.IsTrue(simulation.RemoveWaterSource(source));

            for (int i = 0; i < simulation.Config.WaterMaxLevel + 2; i++)
                simulation.Tick();

            Assert.AreEqual(0, simulation.ActiveWaterCount);
            AssertMaterial(source, VoxelGrid.MaterialEmpty);
        }

        private FireWaterSimulation CreateSimulation()
        {
            MaterialSimulationView materials = _palette.SimulationView;
            return new FireWaterSimulation(_reads, _mutations, in materials);
        }

        private FireWaterSimulation CreateSimulation(FireWaterConfig config)
        {
            MaterialSimulationView materials = _palette.SimulationView;
            return new FireWaterSimulation(_reads, _mutations, in materials, config);
        }

        private void SetCell(int3 voxel, VoxelCell cell)
        {
            int3 worldBlock = voxel >> VoxelReadGrid.BlockEdgeLog2;
            Assert.IsTrue(_mutations.TryBeginCellBlock(worldBlock, false, out VoxelBlockMutation mutation));
            Assert.IsTrue(mutation.IsCreated);
            bool changed = mutation.SetCell(VoxelIndex(voxel), in cell);
            Assert.IsTrue(_mutations.CompletePartialBlock(ref mutation, changed));
        }

        private void AssertMaterial(int3 voxel, byte expected)
        {
            Assert.IsTrue(_reads.TryRead(voxel, out VoxelCell cell));
            Assert.AreEqual(expected, cell.BaseMaterialId);
        }

        private static int VoxelIndex(int3 voxel)
        {
            int x = voxel.x & VoxelReadGrid.BlockEdgeMask;
            int y = voxel.y & VoxelReadGrid.BlockEdgeMask;
            int z = voxel.z & VoxelReadGrid.BlockEdgeMask;
            return x
                 | (y << VoxelReadGrid.BlockEdgeLog2)
                 | (z << (VoxelReadGrid.BlockEdgeLog2 * 2));
        }
    }
}
