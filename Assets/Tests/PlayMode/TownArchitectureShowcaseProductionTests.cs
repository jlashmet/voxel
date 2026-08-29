using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Showcase;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Focused production-path guard for the town-architecture showcase. The single-test SceneIssue
    /// request pairs this regression with the standalone WorldbuildingGalleryShowcase built-player audit.
    /// </summary>
    public sealed class TownArchitectureShowcaseProductionTests
    {
        [Test]
        public void SixTownProgramsPreserveRolesFormsDetailsSeedsAndBounds()
        {
            string[] styleIds =
            {
                WorldBuilderTownArchitectureIds.Kentridge,
                WorldBuilderTownArchitectureIds.Hightown,
                WorldBuilderTownArchitectureIds.Moordell,
                WorldBuilderTownArchitectureIds.Rossdam,
                WorldBuilderTownArchitectureIds.FairyVillage,
                WorldBuilderTownArchitectureIds.OrcVillage,
            };
            TownArchitectureRoofForm[] roofForms =
            {
                TownArchitectureRoofForm.SteepGable,
                TownArchitectureRoofForm.TwinGable,
                TownArchitectureRoofForm.GableWithLeanTo,
                TownArchitectureRoofForm.FortifiedParapet,
                TownArchitectureRoofForm.OrganicCanopySpire,
                TownArchitectureRoofForm.StockadeJagged,
            };
            TownArchitectureStructureRole[] roles =
            {
                TownArchitectureStructureRole.Residential,
                TownArchitectureStructureRole.Commercial,
                TownArchitectureStructureRole.CivicCommunal,
                TownArchitectureStructureRole.LandmarkInfrastructure,
            };

            CollectionAssert.AreEqual(styleIds, WorldBuilderTownArchitecture.AllStyleIds);
            var forms = new HashSet<string>();
            var materials = new HashSet<string>();
            var seeds = new HashSet<uint>();
            var physicalProfiles = new HashSet<string>();
            var physicalByStyle = new Dictionary<string, RecordingStructureAuthoringSession>();
            var palette = new TownArchitectureVoxelPalette(1, 2, 3, 4, 5, 6);

            for (int i = 0; i < styleIds.Length; i++)
            {
                string styleId = styleIds[i];
                TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);
                Assert.AreEqual(styleId, program.StyleId);
                Assert.AreEqual(roofForms[i], program.RoofForm, styleId + " roof/form intent changed");
                Assert.AreEqual(WorldBuilderTownArchitecture.CanonicalSeed(styleId), program.Seed);
                Assert.AreEqual(1, program.DetailUnitBlocks);
                Assert.GreaterOrEqual(program.ReferenceScreenshots.Count, 5);
                Assert.GreaterOrEqual(program.DetailVocabulary.Count, 10);
                foreach (TownArchitectureStructureRole role in roles)
                    Assert.IsTrue(program.IncludesRole(role), styleId + " missing " + role);

                Assert.IsTrue(forms.Add(program.FormSignature), styleId + " form collapsed onto another town");
                Assert.IsTrue(materials.Add(program.MaterialFamily.Signature), styleId + " material family collapsed onto another town");
                Assert.IsTrue(seeds.Add(program.Seed), styleId + " canonical seed is not unique");
                Assert.AreEqual(program.DeterministicSignature,
                    WorldBuilderTownArchitecture.Resolve(styleId).DeterministicSignature);

                var recording = new RecordingStructureAuthoringSession();
                WorldBuilderTownArchitectureVoxelAuthoring.Author(
                    recording,
                    int2.zero,
                    (x, z) => 0,
                    program,
                    in palette);
                physicalByStyle.Add(styleId, recording);
                Assert.Greater(recording.MacroOperationCount, 0, styleId + " emitted no physical macro primitives");
                Assert.IsTrue(
                    physicalProfiles.Add(recording.MacroProfile),
                    styleId + " production realization collapsed onto another town macro profile: " + recording.MacroProfile);
            }

            Assert.AreEqual(6, forms.Count);
            Assert.AreEqual(6, materials.Count);
            Assert.AreEqual(6, seeds.Count);
            Assert.AreEqual(6, physicalProfiles.Count,
                "All six roof/form intents must remain physically distinct through the production voxel authorer.");

            Assert.Greater(physicalByStyle[WorldBuilderTownArchitectureIds.Kentridge].CylinderCount, 0,
                "Kentridge landmark/well vocabulary must survive physical realization.");
            Assert.Greater(physicalByStyle[WorldBuilderTownArchitectureIds.Hightown].ArchCount, 0,
                "Hightown civic arch vocabulary must survive physical realization.");
            Assert.Greater(
                physicalByStyle[WorldBuilderTownArchitectureIds.Moordell].HollowBoxCount,
                physicalByStyle[WorldBuilderTownArchitectureIds.Moordell].GableCount,
                "Moordell lean-to additions must remain physical shells rather than collapsing to the base gables.");
            Assert.Greater(physicalByStyle[WorldBuilderTownArchitectureIds.Rossdam].CrenellateCount, 0,
                "Rossdam fortified form must realize crenellation primitives.");
            Assert.Greater(physicalByStyle[WorldBuilderTownArchitectureIds.FairyVillage].DiscCount, 0,
                "Fairy organic canopy form must realize disc/canopy primitives.");
            Assert.Greater(physicalByStyle[WorldBuilderTownArchitectureIds.OrcVillage].ConeCount, 0,
                "Orc stockade form must realize jagged spike/roof primitives.");

            TownArchitectureProgram rossdam = WorldBuilderTownArchitecture.Resolve(WorldBuilderTownArchitectureIds.Rossdam);
            Assert.AreEqual(TownArchitectureSilhouette.RoyalFortified, rossdam.Silhouette);
            Assert.AreEqual(TownArchitectureRoofForm.FortifiedParapet, rossdam.RoofForm);
            Assert.AreEqual(TownArchitectureOpeningStyle.FortifiedReveal, rossdam.OpeningStyle);

            string[] fortificationDetails =
            {
                "arrow-slit-reveal", "layered-coping", "crenellation", "tower-wall-transition",
                "buttress-cap", "access-stair", "gate-frame", "gate-hardware",
            };
            foreach (string detail in fortificationDetails)
                Assert.IsTrue(rossdam.IncludesDetail(detail), "Rossdam missing " + detail);

            Assert.AreEqual(82, TownArchitectureDistrictBounds.HalfWidthVoxels);
            Assert.AreEqual(66, TownArchitectureDistrictBounds.HalfDepthVoxels);
            Assert.AreEqual(164, TownArchitectureDistrictBounds.WidthVoxels);
            Assert.AreEqual(132, TownArchitectureDistrictBounds.DepthVoxels);
            Assert.AreEqual(78, TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.WidthVoxels, 192);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.DepthVoxels, 160);
            Assert.LessOrEqual(TownArchitectureDistrictBounds.EstimatedMaxHeightVoxels, 96);

            // The same canonical origins drive the stale-bake production probe and the player evidence
            // framing. Keep them deterministic, unique, and safely inside each public district footprint.
            int2[] districtCentres =
            {
                new(-1140, -520), new(-920, -520), new(-700, -520),
                new(-1140, -720), new(-920, -720), new(-700, -720),
            };
            int2[] expectedResidences =
            {
                new(-1188, -532), new(-966, -532), new(-747, -532),
                new(-1187, -732), new(-969, -732), new(-748, -732),
            };
            var residenceAnchors = new HashSet<int2>();
            for (int i = 0; i < districtCentres.Length; i++)
            {
                int2 residence = ShowcaseWorld.WorldbuildingGalleryTownResidenceOriginXZ(i);
                Assert.AreEqual(expectedResidences[i], residence, styleIds[i] + " representative anchor drifted");
                Assert.IsTrue(residenceAnchors.Add(residence), styleIds[i] + " representative anchor is not unique");
                Assert.LessOrEqual(math.abs(residence.x - districtCentres[i].x), TownArchitectureDistrictBounds.HalfWidthVoxels);
                Assert.LessOrEqual(math.abs(residence.y - districtCentres[i].y), TownArchitectureDistrictBounds.HalfDepthVoxels);
            }

            Assert.AreEqual(new int2(-1100, -686), ShowcaseWorld.WorldbuildingGalleryTownLandmarkOriginXZ(3),
                "Rossdam fortified audit landmark drifted away from the authored gatehouse origin.");
        }

        private sealed class RecordingStructureAuthoringSession : IStructureAuthoringSession
        {
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;

            public int HollowBoxCount { get; private set; }
            public int CylinderCount { get; private set; }
            public int DiscCount { get; private set; }
            public int ConeCount { get; private set; }
            public int HangingConeCount { get; private set; }
            public int GableCount { get; private set; }
            public int CrenellateCount { get; private set; }
            public int CrenellateRingCount { get; private set; }
            public int ArchCount { get; private set; }
            public int StairsCount { get; private set; }
            public int SpiralStairCount { get; private set; }

            public int MacroOperationCount =>
                HollowBoxCount + CylinderCount + DiscCount + ConeCount + HangingConeCount +
                GableCount + CrenellateCount + CrenellateRingCount + ArchCount + StairsCount + SpiralStairCount;

            public string MacroProfile =>
                $"hollow={HollowBoxCount};cylinder={CylinderCount};disc={DiscCount};cone={ConeCount};" +
                $"hanging={HangingConeCount};gable={GableCount};cren={CrenellateCount};" +
                $"crenring={CrenellateRingCount};arch={ArchCount};stairs={StairsCount};spiral={SpiralStairCount}";

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }

            public void SetStyled(
                int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }

            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) { }

            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling)
                => HollowBoxCount++;

            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0)
                => CylinderCount++;

            public void Disc(int cx, int y, int cz, int radius, byte material) => DiscCount++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => ConeCount++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => HangingConeCount++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => GableCount++;

            public void Crenellate(
                int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material)
                => CrenellateCount++;

            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material)
                => CrenellateRingCount++;

            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material)
                => ArchCount++;

            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material)
                => StairsCount++;

            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material)
                => SpiralStairCount++;

            public void Carve(int3 min, int3 size) { }
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
