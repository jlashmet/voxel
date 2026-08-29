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
            SettlementPlan plan = KentridgeDefinition.Build(Seed);
            List<VegetationCandidate> a = KentridgeVegetationLayoutPlanner.Build(plan);
            List<VegetationCandidate> b = KentridgeVegetationLayoutPlanner.Build(
                KentridgeDefinition.Build(Seed));

            Assert.AreEqual(38, a.Count,
                "Kentridge should have a sparse authored tree layer with every residential role represented.");
            Assert.AreEqual(a.Count, b.Count);

            var roots = new HashSet<long>();
            for (int i = 0; i < a.Count; i++)
            {
                VegetationCandidate left = a[i];
                VegetationCandidate right = b[i];

                Assert.AreEqual(i, left.Ordinal,
                    "Vegetation ordinals are stable identity input and should remain contiguous.");
                Assert.AreEqual(left.X, right.X);
                Assert.AreEqual(left.Z, right.Z);
                Assert.AreEqual(left.HeightUnits, right.HeightUnits);
                Assert.AreEqual(left.Species, right.Species);
                Assert.AreEqual(left.HeightMode, right.HeightMode);

                long key = ((long)left.X << 32) ^ (uint)left.Z;
                Assert.IsTrue(roots.Add(key),
                    $"Duplicate Kentridge tree root at {left.X},{left.Z}.");
            }
        }

        [Test]
        public void LayoutCarriesDistrictAndWildernessVocabulary()
        {
            List<VegetationCandidate> trees =
                KentridgeVegetationLayoutPlanner.Build(KentridgeDefinition.Build(Seed));

            int pine = 0;
            int oak = 0;
            int maple = 0;
            int birch = 0;
            int dead = 0;

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

            Assert.GreaterOrEqual(pine, 8,
                "The perimeter transition should read as wilderness from a distance.");
            Assert.GreaterOrEqual(oak, 6,
                "Residential/civic districts need mature broadleaf silhouettes.");
            Assert.GreaterOrEqual(maple, 4,
                "Market and noble planting should remain visually distinct.");
            Assert.GreaterOrEqual(birch, 4);
            Assert.AreEqual(1, dead,
                "The abandoned-house yard intentionally owns the one dead specimen tree.");
        }

        [Test]
        public void CountrysideEcology_IsGrassOnlyDenseSuppressesAmbientLifeAndAuthorsExclusions()
        {
            RegionEcologyPolicy policy = KentridgeDefinition.CountrysideEcology;

            Assert.That(policy.VegetationKinds, Has.Count.EqualTo(1));
            Assert.That(policy.AllowsVegetation("Grass"), Is.True);
            Assert.That(policy.AllowsVegetation("Flower"), Is.False);
            Assert.That(policy.TreeKinds, Is.Empty,
                "The current Kentridge countryside policy intentionally suppresses trees.");
            Assert.That(policy.AmbientAnimalKinds, Is.Empty,
                "The current Kentridge countryside policy intentionally suppresses ambient animals.");
            Assert.That(policy.VegetationDensity, Is.GreaterThanOrEqualTo(0.95f));
            Assert.That(policy.VegetationSampleSpacingMetres, Is.LessThanOrEqualTo(0.4f));
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
                vegetationKinds: new[] { "Grass" },
                treeKinds: System.Array.Empty<string>(),
                ambientAnimalKinds: System.Array.Empty<string>(),
                vegetationDensity: 0.5f,
                vegetationSampleSpacingMetres: 1f,
                maxVegetationSlopeDegrees: 25f,
                routeClearanceMetres: 2f,
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

            // Two separated eligible regions model the route-clearance split. The larger meadow is
            // 90x50 cells minus a 10x10 structure exclusion; that hole remains surrounded and does
            // not disconnect the physical meadow. The smaller component proves we do not simply
            // report all generated countryside as one contiguous meadow.
            AddEligibleBlock(samples, eligible, cellByPosition, 0, 90, 0, 50, 20, 30, 20, 30, policy);
            AddEligibleBlock(samples, eligible, cellByPosition, 110, 130, 0, 50, -1, -1, -1, -1, policy);

            VegetationPlacementSettings settings = VegetationPlacementSettings.Default(Seed);
            settings.Density = policy.VegetationDensity;
            settings.MaxGroundSlopeDegrees = policy.MaxVegetationSlopeDegrees;
            settings.RestrictKinds = true;
            settings.AllowedKindsMask = 1UL << (int)VegetationKind.Grass;

            var generated = new List<VegetationInstance>();
            VegetationPlacement.Generate(samples, in settings, generated);

            var occupiedGrass = new List<RegionEcologyGridCell>();
            var bladeWeightByCell = new Dictionary<RegionEcologyGridCell, int>();
            int totalBlades = 0;
            for (int i = 0; i < generated.Count; i++)
            {
                VegetationInstance instance = generated[i];
                Assert.That(instance.Kind, Is.EqualTo(VegetationKind.Grass));
                Assert.That(cellByPosition.TryGetValue(PositionKey(instance.PositionMetres), out RegionEcologyGridCell cell), Is.True,
                    "A generated meadow instance must come from an eligible WorldBuilder sample, never an excluded cell.");

                int blades = ProceduralGrassPresentation.BladeCountForSeed(instance.Seed);
                Assert.That(blades, Is.InRange(
                    ProceduralGrassPresentation.MinBladesPerInstance,
                    ProceduralGrassPresentation.MaxBladesPerInstance));
                Assert.That(blades, Is.EqualTo(ProceduralGrassBatch.BladeCountForSeed(instance.Seed)),
                    "WorldBuilder diagnostics and the packed renderer must use the exact same blade expansion contract.");

                occupiedGrass.Add(cell);
                bladeWeightByCell[cell] = blades;
                totalBlades += blades;
            }

            int primaryMeadowGrass = RegionEcologyConnectivity.LargestConnectedOccupiedCount(
                eligible,
                occupiedGrass);
            int primaryMeadowBlades = RegionEcologyConnectivity.LargestConnectedOccupiedWeight(
                eligible,
                bladeWeightByCell);

            Assert.That(primaryMeadowGrass, Is.GreaterThanOrEqualTo(3000),
                "The dense policy should retain the stronger historical semantic-density guardrail.");
            Assert.That(primaryMeadowBlades, Is.GreaterThanOrEqualTo(3000),
                "The ticket closure metric is actual packed procedural blades in one connected meadow.");
            Assert.That(primaryMeadowBlades, Is.LessThan(totalBlades),
                "Separated eligible countryside must not be misreported as one contiguous meadow.");
        }

        [Test]
        public void ProceduralGrassBladeExpansion_IsDeterministicBoundedAndSharedWithRenderer()
        {
            var distinct = new HashSet<int>();
            for (uint seed = 1; seed <= 1024; seed++)
            {
                int first = ProceduralGrassPresentation.BladeCountForSeed(seed);
                int second = ProceduralGrassPresentation.BladeCountForSeed(seed);
                int renderer = ProceduralGrassBatch.BladeCountForSeed(seed);
                Assert.That(first, Is.EqualTo(second));
                Assert.That(renderer, Is.EqualTo(first));
                Assert.That(first, Is.InRange(5, 15));
                distinct.Add(first);
            }

            Assert.That(distinct.Count, Is.GreaterThanOrEqualTo(8),
                "Per-seed blade expansion should preserve visible local density variation.");
        }

        [Test]
        public void EcologyConnectivity_DoesNotBridgeAcrossExcludedCells()
        {
            var eligible = new List<RegionEcologyGridCell>
            {
                new RegionEcologyGridCell(0, 0),
                new RegionEcologyGridCell(1, 0),
                new RegionEcologyGridCell(3, 0),
                new RegionEcologyGridCell(4, 0),
            };
            var occupied = new List<RegionEcologyGridCell>(eligible);
            var weights = new Dictionary<RegionEcologyGridCell, int>();
            for (int i = 0; i < eligible.Count; i++) weights[eligible[i]] = 10;

            int largest = RegionEcologyConnectivity.LargestConnectedOccupiedCount(eligible, occupied);
            int largestWeight = RegionEcologyConnectivity.LargestConnectedOccupiedWeight(eligible, weights);

            Assert.That(largest, Is.EqualTo(2),
                "Roads, structures, water, and steep invalid cells must break ecology connectivity rather than be bridged by distance.");
            Assert.That(largestWeight, Is.EqualTo(20),
                "Rendered-blade attribution must use the same physical connectivity boundaries.");
        }

        private static void AddEligibleBlock(
            List<VegetationSurfaceSample> samples,
            List<RegionEcologyGridCell> eligible,
            Dictionary<long, RegionEcologyGridCell> cellByPosition,
            int fromX,
            int toXExclusive,
            int fromZ,
            int toZExclusive,
            int excludedFromX,
            int excludedToXExclusive,
            int excludedFromZ,
            int excludedToZExclusive,
            RegionEcologyPolicy policy)
        {
            float spacing = policy.VegetationSampleSpacingMetres;
            for (int z = fromZ; z < toZExclusive; z++)
            for (int x = fromX; x < toXExclusive; x++)
            {
                bool excluded = x >= excludedFromX && x < excludedToXExclusive
                    && z >= excludedFromZ && z < excludedToZExclusive;
                if (excluded) continue;

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
