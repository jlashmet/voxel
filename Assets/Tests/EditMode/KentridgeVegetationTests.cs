using System.Collections.Generic;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class KentridgeVegetationTests
    {
        private const uint Seed = 0x4B454E54u;

        [Test]
        public void LayoutIsDeterministicAndUsesUniqueRoots()
        {
            List<VegetationCandidate> first = KentridgeVegetationLayoutPlanner.Build(KentridgeDefinition.Build(Seed));
            List<VegetationCandidate> second = KentridgeVegetationLayoutPlanner.Build(KentridgeDefinition.Build(Seed));
            Assert.That(first, Has.Count.EqualTo(38));
            Assert.That(second, Has.Count.EqualTo(first.Count));

            var roots = new HashSet<long>();
            for (int i = 0; i < first.Count; i++)
            {
                VegetationCandidate a = first[i];
                VegetationCandidate b = second[i];
                Assert.That(a.Ordinal, Is.EqualTo(i));
                Assert.That(a.X, Is.EqualTo(b.X));
                Assert.That(a.Z, Is.EqualTo(b.Z));
                Assert.That(a.HeightUnits, Is.EqualTo(b.HeightUnits));
                Assert.That(a.Species, Is.EqualTo(b.Species));
                Assert.That(a.HeightMode, Is.EqualTo(b.HeightMode));
                Assert.That(roots.Add(((long)a.X << 32) ^ (uint)a.Z), Is.True,
                    $"Duplicate Kentridge tree root at {a.X},{a.Z}.");
            }
        }

        [Test]
        public void LayoutCarriesDistrictAndWildernessVocabulary()
        {
            List<VegetationCandidate> trees = KentridgeVegetationLayoutPlanner.Build(KentridgeDefinition.Build(Seed));
            int pine = 0, oak = 0, maple = 0, birch = 0, dead = 0;
            for (int i = 0; i < trees.Count; i++)
            {
                switch (trees[i].Species)
                {
                    case SemanticTreeSpecies.Pine: pine++; break;
                    case SemanticTreeSpecies.Oak: oak++; break;
                    case SemanticTreeSpecies.Maple: maple++; break;
                    case SemanticTreeSpecies.Birch: birch++; break;
                    case SemanticTreeSpecies.Dead: dead++; break;
                }
            }
            Assert.That(pine, Is.GreaterThanOrEqualTo(8));
            Assert.That(oak, Is.GreaterThanOrEqualTo(6));
            Assert.That(maple, Is.GreaterThanOrEqualTo(4));
            Assert.That(birch, Is.GreaterThanOrEqualTo(4));
            Assert.That(dead, Is.EqualTo(1));
        }

        [Test]
        public void CountrysideEcology_IsGrassOnlyDenseSuppressesAmbientLifeAndAuthorsExclusions()
        {
            RegionEcologyPolicy policy = KentridgeDefinition.CountrysideEcology;
            Assert.That(policy.VegetationKinds, Has.Count.EqualTo(1));
            Assert.That(policy.AllowsVegetation("Grass"), Is.True);
            Assert.That(policy.AllowsVegetation("Flower"), Is.False);
            Assert.That(policy.TreeKinds, Is.Empty);
            Assert.That(policy.AmbientAnimalKinds, Is.Empty);
            Assert.That(policy.VegetationDensity, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(policy.VegetationSampleSpacingMetres, Is.EqualTo(0.8f).Within(0.001f),
                "A dense sub-metre grid must also span the bounded packed-grass budget through the opening player view; 0.4 m exhausted the cap behind the camera.");
            Assert.That(policy.RouteClearanceMetres, Is.GreaterThanOrEqualTo(5f));
            Assert.That(policy.Excludes(RegionEcologyExclusion.Route), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.BuiltContent), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.Water), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.Cultivated), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.SteepOrCliff), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.OtherInvalid), Is.True);
        }

        [Test]
        public void EcologyExclusions_AreIndependentlyAuthorable()
        {
            var policy = new RegionEcologyPolicy(
                new[] { "Grass" }, System.Array.Empty<string>(), System.Array.Empty<string>(),
                0.5f, 1f, 25f, 2f,
                exclusions: RegionEcologyExclusion.Route | RegionEcologyExclusion.Cultivated);
            Assert.That(policy.Excludes(RegionEcologyExclusion.Route), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.Cultivated), Is.True);
            Assert.That(policy.Excludes(RegionEcologyExclusion.BuiltContent), Is.False);
            Assert.That(policy.Excludes(RegionEcologyExclusion.Water), Is.False);
            Assert.That(policy.Excludes(RegionEcologyExclusion.SteepOrCliff), Is.False);
            Assert.That(policy.Excludes(RegionEcologyExclusion.OtherInvalid), Is.False);
        }

        [Test]
        public void CountrysideEcology_ProductionPlacementProducesConnectedThreeThousandBladeMeadow()
        {
            RegionEcologyPolicy policy = KentridgeDefinition.CountrysideEcology;
            var samples = new List<VegetationSurfaceSample>();
            var eligible = new List<RegionEcologyGridCell>();
            var cellByPosition = new Dictionary<long, RegionEcologyGridCell>();
            AddEligibleBlock(samples, eligible, cellByPosition, 0, 90, 0, 50, 20, 30, 20, 30, policy);
            AddEligibleBlock(samples, eligible, cellByPosition, 110, 130, 0, 50, -1, -1, -1, -1, policy);

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(Seed);
            settings.Density = policy.VegetationDensity;
            settings.MaxGroundSlopeDegrees = policy.MaxVegetationSlopeDegrees;
            settings.RestrictKinds = true;
            settings.AllowedKindsMask = 1UL << (int)VegetationKind.Grass;
            var generated = new List<VegetationInstance>();
            VegetationPlacement.Generate(samples, in settings, generated);

            var occupied = new List<RegionEcologyGridCell>();
            var weights = new Dictionary<RegionEcologyGridCell, int>();
            int totalBlades = 0;
            for (int i = 0; i < generated.Count; i++)
            {
                VegetationInstance instance = generated[i];
                Assert.That(instance.Kind, Is.EqualTo(VegetationKind.Grass));
                Assert.That(cellByPosition.TryGetValue(PositionKey(instance.PositionMetres), out RegionEcologyGridCell cell), Is.True);
                int blades = ProceduralGrassPresentation.BladeCountForSeed(instance.Seed);
                Assert.That(blades, Is.InRange(ProceduralGrassPresentation.MinBladesPerInstance, ProceduralGrassPresentation.MaxBladesPerInstance));
                Assert.That(blades, Is.EqualTo(ProceduralGrassBatch.BladeCountForSeed(instance.Seed)));
                occupied.Add(cell);
                weights[cell] = blades;
                totalBlades += blades;
            }

            int primaryGrass = RegionEcologyConnectivity.LargestConnectedOccupiedCount(eligible, occupied);
            int primaryBlades = RegionEcologyConnectivity.LargestConnectedOccupiedWeight(eligible, weights);
            Assert.That(primaryGrass, Is.GreaterThanOrEqualTo(3000));
            Assert.That(primaryBlades, Is.GreaterThanOrEqualTo(3000));
            Assert.That(primaryBlades, Is.LessThan(totalBlades));
        }

        [Test]
        public void ProceduralGrassBladeExpansion_IsDeterministicBoundedAndSharedWithRenderer()
        {
            var distinct = new HashSet<int>();
            for (uint seed = 1; seed <= 1024; seed++)
            {
                int first = ProceduralGrassPresentation.BladeCountForSeed(seed);
                Assert.That(ProceduralGrassPresentation.BladeCountForSeed(seed), Is.EqualTo(first));
                Assert.That(ProceduralGrassBatch.BladeCountForSeed(seed), Is.EqualTo(first));
                Assert.That(first, Is.InRange(5, 15));
                distinct.Add(first);
            }
            Assert.That(distinct.Count, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void EcologyConnectivity_DoesNotBridgeAcrossExcludedCells()
        {
            var eligible = new List<RegionEcologyGridCell>
            {
                new RegionEcologyGridCell(0, 0), new RegionEcologyGridCell(1, 0),
                new RegionEcologyGridCell(3, 0), new RegionEcologyGridCell(4, 0),
            };
            var weights = new Dictionary<RegionEcologyGridCell, int>();
            for (int i = 0; i < eligible.Count; i++) weights[eligible[i]] = 10;
            Assert.That(RegionEcologyConnectivity.LargestConnectedOccupiedCount(eligible, eligible), Is.EqualTo(2));
            Assert.That(RegionEcologyConnectivity.LargestConnectedOccupiedWeight(eligible, weights), Is.EqualTo(20));
        }

        private static void AddEligibleBlock(
            List<VegetationSurfaceSample> samples,
            List<RegionEcologyGridCell> eligible,
            Dictionary<long, RegionEcologyGridCell> cellByPosition,
            int fromX, int toX, int fromZ, int toZ,
            int excludedFromX, int excludedToX, int excludedFromZ, int excludedToZ,
            RegionEcologyPolicy policy)
        {
            float spacing = policy.VegetationSampleSpacingMetres;
            for (int z = fromZ; z < toZ; z++)
            for (int x = fromX; x < toX; x++)
            {
                if (x >= excludedFromX && x < excludedToX && z >= excludedFromZ && z < excludedToZ) continue;
                var cell = new RegionEcologyGridCell(x, z);
                float3 position = new float3(x * spacing, 0f, z * spacing);
                eligible.Add(cell);
                cellByPosition[PositionKey(position)] = cell;
                samples.Add(new VegetationSurfaceSample
                {
                    PositionMetres = position,
                    Normal = new float3(0f, 1f, 0f),
                    Surface = VegetationSurface.Ground,
                    Moisture = 0.5f,
                    Shade = 0.3f,
                    ArcaneSaturation = 0f,
                });
            }
        }

        private static long PositionKey(float3 position)
        {
            int x = (int)math.round(position.x * 1000f);
            int z = (int)math.round(position.z * 1000f);
            return ((long)x << 32) ^ (uint)z;
        }
    }
}
