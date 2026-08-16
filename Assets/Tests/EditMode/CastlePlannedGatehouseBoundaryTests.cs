using System.IO;
using NUnit.Framework;

namespace VoxelEngine.Tests.EditMode
{
    public sealed class CastlePlannedGatehouseBoundaryTests
    {
        private static string RepoRoot
        {
            get
            {
                var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
                while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets")))
                    dir = dir.Parent;

                Assert.NotNull(dir, "Could not locate project root containing Assets/.");
                return dir.FullName;
            }
        }

        [Test]
        public void PlannedGatehouseConsumesFrozenRecipeInsteadOfRederivingDimensions()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePlannedGatehouseRealizer.cs"));

            StringAssert.Contains("CastleGatehousePlanValidator.RequireValid", source);
            StringAssert.Contains("gatehouse.TowerSpacing", source);
            StringAssert.Contains("gatehouse.LeftTowerHeight", source);
            StringAssert.Contains("gatehouse.RightTowerHeight", source);
            StringAssert.Contains("gatehouse.BlockHeight", source);
            StringAssert.Contains("gatehouse.OpeningHeight", source);
            StringAssert.Contains("gatehouse.BridgeNearDistance", source);
            StringAssert.Contains("gatehouse.BridgeLength", source);
            StringAssert.Contains("gatehouse.BridgeWidth", source);
            StringAssert.Contains("gatehouse.BridgeSupportOffset", source);
            StringAssert.Contains("gatehouse.BridgeRailYOffset", source);

            StringAssert.DoesNotContain("CastleGatehousePlanner.Create", source);
            StringAssert.DoesNotContain("plan.GateTowerHeight + 38", source);
            StringAssert.DoesNotContain("plan.GateTowerHeight + 12", source);
            StringAssert.DoesNotContain("nearDistance + 150", source);
        }

        [Test]
        public void LegacyPerimeterGatehouseIsOnlyCompatibilityDelegation()
        {
            string source = File.ReadAllText(Path.Combine(
                RepoRoot, "Assets", "VoxelEngine", "Structures", "Runtime",
                "CastlePerimeterRealizer.cs"));

            StringAssert.Contains("CastleGatehousePlanner.Create(in plan)", source);
            StringAssert.Contains("CastlePlannedGatehouseRealizer.Build(", source);
            StringAssert.DoesNotContain("plan.GateTowerHeight + 38", source);
            StringAssert.DoesNotContain("plan.GateTowerHeight + 12", source);
            StringAssert.DoesNotContain("private static void BuildGateLeaf", source);
            StringAssert.DoesNotContain("private static void ApproachBridge", source);
        }
    }
}
