using System;
using System.Collections.Generic;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Mathematics;
using VoxelEngine.Storage.Api;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class TownArchitectureExtensibilityTests
    {
        [Test]
        public void RegisteredSeventhStyleComposesExistingCapabilitiesWithoutCentralDispatch()
        {
            string[] baselineIds =
            {
                WorldBuilderTownArchitectureIds.Kentridge,
                WorldBuilderTownArchitectureIds.Hightown,
                WorldBuilderTownArchitectureIds.Moordell,
                WorldBuilderTownArchitectureIds.Rossdam,
                WorldBuilderTownArchitectureIds.FairyVillage,
                WorldBuilderTownArchitectureIds.OrcVillage,
            };
            CollectionAssert.AreEqual(baselineIds, WorldBuilderTownArchitecture.AllStyleIds,
                "The six accepted reference-driven styles must remain the built-in catalogue.");

            const string proofId = "test-river-trade";
            WorldBuilderTownArchitecture.Unregister(proofId);
            try
            {
                TownArchitectureDefinition proof = RiverTradeDefinition(proofId);
                Assert.IsTrue(WorldBuilderTownArchitecture.Register(proof));
                Assert.IsTrue(WorldBuilderTownArchitecture.IsRegistered(proofId));
                CollectionAssert.Contains(WorldBuilderTownArchitecture.AllStyleIds, proofId);

                TownArchitectureProgram canonical = WorldBuilderTownArchitecture.Resolve(proofId);
                TownArchitectureProgram same = WorldBuilderTownArchitecture.Resolve(proofId, 0x52495652u);
                TownArchitectureProgram variant = WorldBuilderTownArchitecture.Resolve(proofId, 0x52495653u);
                Assert.AreEqual(0x52495652u, canonical.Seed);
                Assert.AreEqual(canonical.DeterministicSignature, same.DeterministicSignature);
                Assert.AreNotEqual(canonical.DeterministicSignature, variant.DeterministicSignature);

                // This semantic combination was rejected by the former silhouette-to-roof switch. It is now
                // descriptive style data while each role independently composes reusable realization capabilities.
                Assert.AreEqual(TownArchitectureSilhouette.PastoralTimberFrame, canonical.Silhouette);
                Assert.AreEqual(TownArchitectureRoofForm.TwinGable, canonical.RoofForm);
                Assert.AreEqual(TownArchitectureOpeningStyle.OrderedStone, canonical.OpeningStyle);
                Assert.AreEqual(4, canonical.Composition.Roles.Count);
                foreach (TownArchitectureStructureRole role in Enum.GetValues(typeof(TownArchitectureStructureRole)))
                    Assert.IsTrue(canonical.IncludesRole(role));
                Assert.IsTrue(canonical.IncludesDetail("market-awning"));
                Assert.IsTrue(canonical.IncludesDetail("civic-arch"));

                var palette = new TownArchitectureVoxelPalette(1, 2, 3, 4, 5, 6);
                var first = new RecordingStructureAuthoringSession();
                var repeat = new RecordingStructureAuthoringSession();
                var shifted = new RecordingStructureAuthoringSession();
                WorldBuilderTownArchitectureVoxelAuthoring.Author(first, int2.zero, (x, z) => 0, canonical, in palette);
                WorldBuilderTownArchitectureVoxelAuthoring.Author(repeat, int2.zero, (x, z) => 0, same, in palette);
                WorldBuilderTownArchitectureVoxelAuthoring.Author(shifted, int2.zero, (x, z) => 0, variant, in palette);

                Assert.AreEqual(first.MacroProfile, repeat.MacroProfile,
                    "Same registered recipe and seed must produce deterministic reusable macro composition.");
                Assert.Greater(first.GableCount, 0, "River-trade proof must compose steep/twin gabled capabilities.");
                Assert.Greater(first.ArchCount, 0, "River-trade proof must compose civic arch capability.");
                Assert.Greater(first.CylinderCount, 0, "River-trade proof must compose chimney capability.");
                Assert.Greater(first.HollowBoxCount, 0);
                Assert.Greater(first.BoxCount, 0);

                var baselineProfiles = new HashSet<string>();
                foreach (string styleId in baselineIds)
                {
                    TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);
                    Assert.GreaterOrEqual(program.ReferenceScreenshots.Count, 5, styleId + " lost reference evidence");
                    Assert.GreaterOrEqual(program.DetailVocabulary.Count, 10, styleId + " lost detail vocabulary");
                    foreach (TownArchitectureStructureRole role in Enum.GetValues(typeof(TownArchitectureStructureRole)))
                        Assert.IsTrue(program.IncludesRole(role), styleId + " lost " + role);

                    var recording = new RecordingStructureAuthoringSession();
                    WorldBuilderTownArchitectureVoxelAuthoring.Author(recording, int2.zero, (x, z) => 0, program, in palette);
                    Assert.IsTrue(baselineProfiles.Add(recording.MacroProfile),
                        styleId + " collapsed onto another baseline physical macro profile");
                }
                Assert.AreEqual(6, baselineProfiles.Count);
                Assert.IsFalse(baselineProfiles.Contains(first.MacroProfile),
                    "Seventh proof town must be physically distinct rather than a recolor of a baseline recipe.");

                Assert.Greater(new RecordingFor(WorldBuilderTownArchitectureIds.Kentridge, palette).CylinderCount, 0);
                Assert.Greater(new RecordingFor(WorldBuilderTownArchitectureIds.Hightown, palette).ArchCount, 0);
                RecordingStructureAuthoringSession moordell = new RecordingFor(WorldBuilderTownArchitectureIds.Moordell, palette);
                Assert.GreaterOrEqual(moordell.HollowBoxCount, moordell.GableCount,
                    "Moordell must retain attached low-stone/lean-to shell composition.");
                Assert.Greater(new RecordingFor(WorldBuilderTownArchitectureIds.Rossdam, palette).CrenellateCount, 0);
                Assert.Greater(new RecordingFor(WorldBuilderTownArchitectureIds.FairyVillage, palette).DiscCount, 0);
                Assert.Greater(new RecordingFor(WorldBuilderTownArchitectureIds.OrcVillage, palette).ConeCount, 0);
            }
            finally
            {
                WorldBuilderTownArchitecture.Unregister(proofId);
            }
        }

        private static TownArchitectureDefinition RiverTradeDefinition(string styleId)
        {
            var materials = new TownArchitectureMaterialFamily(
                "river-cut-stone-and-plaster", "steep-red-tile", "dark-river-oak", "quayside-stone",
                "iron-and-weathered-timber", "lantern-amber-and-blue-cloth");
            var composition = new TownArchitectureComposition(
                new TownArchitectureRoleRecipe(
                    TownArchitectureStructureRole.Residential, TownArchitectureMassing.GabledFrame,
                    TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.OrderedStone,
                    TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.Balcony | TownArchitectureDetailFeatures.Chimney,
                    38, 30, 27, 16),
                new TownArchitectureRoleRecipe(
                    TownArchitectureStructureRole.Commercial, TownArchitectureMassing.StoneGabled,
                    TownArchitectureRoofForm.SteepGable, TownArchitectureOpeningStyle.TimberFramed,
                    TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.TimberFrame | TownArchitectureDetailFeatures.Awning,
                    44, 32, 28, 17),
                new TownArchitectureRoleRecipe(
                    TownArchitectureStructureRole.CivicCommunal, TownArchitectureMassing.StoneGabled,
                    TownArchitectureRoofForm.TwinGable, TownArchitectureOpeningStyle.OrderedStone,
                    TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Balcony,
                    46, 36, 32, 19),
                new TownArchitectureRoleRecipe(
                    TownArchitectureStructureRole.LandmarkInfrastructure, TownArchitectureMassing.FortifiedParapet,
                    TownArchitectureRoofForm.FortifiedParapet, TownArchitectureOpeningStyle.OrderedStone,
                    TownArchitectureDetailFeatures.MasonryCourses | TownArchitectureDetailFeatures.CivicArch | TownArchitectureDetailFeatures.Buttress,
                    42, 34, 34, 0));
            return new TownArchitectureDefinition(
                styleId, "River Trade", "synthetic-proof", 0x52495652u, 1,
                TownArchitectureSilhouette.PastoralTimberFrame,
                TownArchitectureRoofForm.TwinGable,
                TownArchitectureOpeningStyle.OrderedStone,
                in materials,
                composition,
                new string[0],
                new[]
                {
                    "stone-lower-storey", "timber-upper-frame", "steep-roof", "recessed-window",
                    "projecting-sill-lintel", "balcony-rail", "market-awning", "stone-course",
                    "civic-arch", "quayside-buttress", "chimney-cap", "lantern-bracket"
                });
        }

        private sealed class RecordingFor : RecordingStructureAuthoringSession
        {
            public RecordingFor(string styleId, TownArchitectureVoxelPalette palette)
            {
                TownArchitectureProgram program = WorldBuilderTownArchitecture.Resolve(styleId);
                WorldBuilderTownArchitectureVoxelAuthoring.Author(this, int2.zero, (x, z) => 0, program, in palette);
            }
        }

        private class RecordingStructureAuthoringSession : IStructureAuthoringSession
        {
            public bool BudgetExceeded => false;
            public int WriteBudget => int.MaxValue;
            public long TotalVoxelsWritten => 0;
            public int BoxCount { get; private set; }
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
            public int CarveCount { get; private set; }

            public string MacroProfile =>
                $"box={BoxCount};hollow={HollowBoxCount};cylinder={CylinderCount};disc={DiscCount};cone={ConeCount};" +
                $"hanging={HangingConeCount};gable={GableCount};cren={CrenellateCount};crenring={CrenellateRingCount};" +
                $"arch={ArchCount};stairs={StairsCount};spiral={SpiralStairCount};carve={CarveCount}";

            public byte Get(int x, int y, int z) => 0;
            public byte GetCoating(int x, int y, int z) => Coatings.None;
            public bool IsSolid(int x, int y, int z) => false;
            public void Set(int x, int y, int z, byte material) { }
            public void SetStyled(int x, int y, int z, byte material, ushort surfaceStyle,
                byte coating = Coatings.None, VoxelSurfaceFlags flags = VoxelSurfaceFlags.None) { }
            public void Coat(int x, int y, int z, byte coating) { }
            public void FillBulk(int3 min, int3 size, byte material) { }
            public void FillColumnBulk(int x, int minY, int maxYExclusive, int z, byte material) { }
            public void Box(int3 min, int3 size, byte material) => BoxCount++;
            public void HollowBox(int3 min, int3 size, int thickness, byte material, bool floor, bool ceiling) => HollowBoxCount++;
            public void Cylinder(int cx, int baseY, int cz, int radius, int height, byte material, int innerRadius = 0) => CylinderCount++;
            public void Disc(int cx, int y, int cz, int radius, byte material) => DiscCount++;
            public void Cone(int cx, int baseY, int cz, int radius, int height, byte material) => ConeCount++;
            public void HangingCone(int cx, int ceilingY, int cz, int radius, int height, byte material) => HangingConeCount++;
            public void Gable(int3 min, int3 size, bool alongX, byte material) => GableCount++;
            public void Crenellate(int3 start, int3 step, int count, int width, int height, int merlon, int gap, byte material) => CrenellateCount++;
            public void CrenellateRing(int cx, int y, int cz, int radius, int height, byte material) => CrenellateRingCount++;
            public void Arch(int3 min, int width, int height, int depth, int depthAxis, byte material) => ArchCount++;
            public void Stairs(int3 min, int width, int steps, int rise, int run, int axis, byte material) => StairsCount++;
            public void SpiralStair(int cx, int baseY, int cz, int radius, int height, byte material) => SpiralStairCount++;
            public void Carve(int3 min, int3 size) => CarveCount++;
            public void Weather(int3 min, int3 size, byte coating, uint seed, int chanceOutOf100) { }
        }
    }
}
