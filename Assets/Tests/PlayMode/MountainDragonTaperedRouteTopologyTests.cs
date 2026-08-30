using System;
using System.IO;
using Game.WorldBuilder.Voxel;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using VoxelEngine.Showcase;
using VoxelEngine.Structures.Api;

namespace VoxelEngine.Tests.PlayMode
{
    /// <summary>
    /// Regression for the route/core topology isolated after revision 6. The route helper is the
    /// shared source of truth for voxel authoring and the checked-in built-player evidence fixture.
    /// </summary>
    public sealed class MountainDragonTaperedRouteTopologyTests
    {
        private const uint Seed = 0x5EED1234;
        private const byte RockMaterial = 6;
        private const byte PathMaterial = 13;
        private const byte DragonMaterial = 9;
        private const float VoxelSizeMetres = 0.1f;
        private const int LegacySupportSegmentSpan = 64;

        [Test]
        public void TaperedRouteAndVisualAcceptanceAreReadyForBuiltPlayerReplay()
        {
            TaperedRouteIntegratesWithCoreAndEvidenceWithoutSupportCostRegression();
            new MountainDragonVisualFinalAcceptanceTests()
                .ProductionQualityMountainMaterialsAndEncounterAreReadyForBuiltPlayerReplay();
        }

        [Test]
        public void TaperedRouteIntegratesWithCoreAndEvidenceWithoutSupportCostRegression()
        {
            MountainLandmarkSpec spec = ShowcaseMountainDragonLayout.CreateLandmark(Seed);
            MountainPathTierGeometry previous = default;
            bool narrowed = false;

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                int radiusAtEnd = spec.CoreRadiusAtHeight(tier.EndY);
                int coreMinX = spec.CentreLocal - radiusAtEnd;
                int coreMaxX = spec.CentreLocal + radiusAtEnd;
                int coreMinZAtStart = spec.CoreMinLocalZAtHeight(tier.StartY);
                int coreMinZAtEnd = spec.CoreMinLocalZAtHeight(tier.EndY);

                Assert.That(tier.LocalZ, Is.LessThan(coreMinZAtStart),
                    $"Tier {level} must keep an exposed outer edge rather than becoming a trench.");
                Assert.That(tier.LocalZ + spec.PathWidth, Is.GreaterThan(coreMinZAtStart),
                    $"Tier {level} must overlap the natural shell at its low end.");
                Assert.That(
                    coreMinZAtEnd - (tier.LocalZ + spec.PathWidth),
                    Is.LessThanOrEqualTo(spec.PathWidth * 2),
                    $"Tier {level} high end must remain within a modest embankment envelope.");

                Assert.That(tier.MinX, Is.GreaterThanOrEqualTo(coreMinX - spec.PathWidth),
                    $"Tier {level} left edge exceeds the tapered core plus one path-width embankment.");
                Assert.That(tier.MinX + tier.Run, Is.LessThanOrEqualTo(coreMaxX + spec.PathWidth),
                    $"Tier {level} right edge exceeds the tapered core plus one path-width embankment.");

                if (level > 0)
                {
                    Assert.That(tier.LowLandingMinX, Is.EqualTo(previous.HighLandingMinX),
                        $"Tier {level} must start on the exact prior turn landing.");
                    Assert.That(tier.Run, Is.LessThanOrEqualTo(previous.Run),
                        "Switchback run length must be monotonic as the mountain narrows.");
                    narrowed |= tier.Run < previous.Run;
                }

                previous = tier;
            }

            Assert.That(narrowed, Is.True,
                "At least one upper tier must narrow; a constant 360-voxel route recreates revision 6.");

            MountainPathTierGeometry penultimate = spec.PathTier(spec.SwitchbackCount - 2);
            MountainPathTierGeometry final = spec.PathTier(spec.SwitchbackCount - 1);
            Assert.That(penultimate.Run, Is.LessThan(spec.PathRun));
            Assert.That(final.Run, Is.LessThan(penultimate.Run));
            Assert.That(final.Run - spec.PathWidth * 2, Is.GreaterThan(spec.PathRise * 2),
                "The narrowest ramp must retain more than 2:1 horizontal run-to-rise for normal traversal.");

            AssertEvidenceRouteUsesSharedTierGeometry(in spec);
            AssertSupportRasterProxyReducedFromRevision6(in spec);
        }

        private static void AssertEvidenceRouteUsesSharedTierGeometry(in MountainLandmarkSpec spec)
        {
            string routePath = Path.GetFullPath(Path.Combine(
                Application.dataPath,
                "..",
                "SceneIssues",
                "open",
                "20260828-180417-000-VoxelShowcaseMountainDragonCutscene",
                "mountain-dragon-evidence-route.json"));
            Assert.That(File.Exists(routePath), Is.True, "Mountain Dragon evidence route is missing.");

            EvidenceRoute route = JsonUtility.FromJson<EvidenceRoute>(File.ReadAllText(routePath));
            Assert.That(route, Is.Not.Null);
            Assert.That(route.waypoints, Is.Not.Null);

            for (int level = 0; level < spec.SwitchbackCount; level++)
            {
                MountainPathTierGeometry tier = spec.PathTier(level);
                EvidenceWaypoint low = FindWaypoint(
                    route,
                    level == 0 ? "path-base" : $"switchback-{level}-low");
                EvidenceWaypoint high = FindWaypoint(route, $"switchback-{level}-high");

                AssertWorldCoordinate(low.x, spec.Origin.x + tier.LowCentreX, low.name + " x");
                AssertWorldCoordinate(low.z, spec.Origin.z + tier.CentreZ, low.name + " z");
                AssertWorldCoordinate(high.x, spec.Origin.x + tier.HighCentreX, high.name + " x");
                AssertWorldCoordinate(high.z, spec.Origin.z + tier.CentreZ, high.name + " z");

                if (level > 0)
                    Assert.That(low.expectedYOffset, Is.EqualTo(tier.StartY * VoxelSizeMetres).Within(0.001f));
                Assert.That(high.expectedYOffset, Is.EqualTo(tier.EndY * VoxelSizeMetres).Within(0.001f));
            }

            MountainPathTierGeometry last = spec.PathTier(spec.SwitchbackCount - 1);
            EvidenceWaypoint summitRamp = FindWaypoint(route, "summit-ramp-high");
            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            AssertWorldCoordinate(
                summitRamp.x,
                spec.Origin.x + last.HighCentreX,
                summitRamp.name + " x");
            AssertWorldCoordinate(
                summitRamp.z,
                spec.Origin.z + summitZ + spec.PathWidth / 2,
                summitRamp.name + " z");

            EvidenceWaypoint proximity = FindWaypoint(route, "summit-proximity");
            AssertWorldCoordinate(
                proximity.x,
                spec.Origin.x + spec.SummitApproachLocalX,
                proximity.name + " x");
            AssertWorldCoordinate(
                proximity.z,
                spec.Origin.z + summitZ + spec.PathWidth / 2,
                proximity.name + " z");
        }

        private static void AssertSupportRasterProxyReducedFromRevision6(in MountainLandmarkSpec spec)
        {
            FeatureCatalogue catalogue = WorldBuilderMountainLandmarkCatalogue.Build(
                in spec,
                RockMaterial,
                PathMaterial,
                DragonMaterial,
                Allocator.Temp);
            try
            {
                FeatureDefinition landform = catalogue.Definitions[0];
                long actualProxy = 0;
                int supportFrustums = 0;
                int additiveFrustums = 0;

                for (int pc = landform.ProgramOffset;
                     pc < landform.ProgramOffset + landform.ProgramLength;)
                {
                    ShapeOp op = (ShapeOp)catalogue.Program[pc];
                    if (op == ShapeOp.End) break;
                    int length = ShapeOps.InstructionLength(op);
                    Assert.That(length, Is.GreaterThan(0));

                    if (op == ShapeOp.EmitFrustum
                        && (PrimitiveMode)catalogue.Program[pc + 12] == PrimitiveMode.FillIfEmpty
                        && catalogue.Program[pc + 9] == RockMaterial)
                    {
                        if (additiveFrustums >= 3)
                        {
                            supportFrustums++;
                            actualProxy += ConservativeFrustumRasterProxy(
                                catalogue.Program[pc + 5],
                                catalogue.Program[pc + 6]);
                        }

                        additiveFrustums++;
                    }

                    pc += length;
                }

                long legacyProxy = Revision6ConstantRunSupportProxy(in spec, out int legacySupportFrustums);
                Assert.That(supportFrustums, Is.LessThan(legacySupportFrustums),
                    "Tapered topology must remove support segments rather than adding geometry.");
                Assert.That(actualProxy, Is.LessThan(legacyProxy),
                    "Tapered topology must reduce the conservative support raster-cost proxy.");
            }
            finally
            {
                catalogue.Dispose();
            }
        }

        private static long Revision6ConstantRunSupportProxy(
            in MountainLandmarkSpec spec,
            out int supportFrustums)
        {
            long proxy = 0;
            supportFrustums = 0;

            for (int level = 1; level < spec.SwitchbackCount; level++)
            {
                int supportTopY = level * spec.PathRise;
                AddLegacySupportProxy(spec.PathRun, supportTopY, spec.PathWidth, ref supportFrustums, ref proxy);
            }

            for (int level = 0; level + 1 < spec.SwitchbackCount; level++)
            {
                int startY = level * spec.PathRise;
                int endY = startY + spec.PathRise;
                int z = LegacyRampLocalZ(in spec, startY);
                int nextZ = LegacyRampLocalZ(in spec, endY);
                int zSize = Math.Abs(nextZ - z) + spec.PathWidth;
                AddLegacySupportProxy(zSize, endY, spec.PathWidth, ref supportFrustums, ref proxy);
            }

            int finalStartY = (spec.SwitchbackCount - 1) * spec.PathRise;
            int finalEndY = spec.SwitchbackCount * spec.PathRise;
            int lastZ = LegacyRampLocalZ(in spec, finalStartY);
            int summitZ = spec.CentreLocal - spec.SummitRadius - spec.PathWidth;
            int finalZSize = Math.Abs(summitZ - lastZ) + spec.PathWidth;
            AddLegacySupportProxy(
                finalZSize,
                finalEndY,
                spec.PathWidth,
                ref supportFrustums,
                ref proxy);

            int pathMinX = spec.CentreLocal - spec.PathRun / 2;
            bool finalReverse = ((spec.SwitchbackCount - 1) & 1) != 0;
            int lastHighX = finalReverse
                ? pathMinX
                : pathMinX + spec.PathRun - spec.PathWidth;
            int topSizeX = Math.Abs(spec.SummitApproachLocalX - lastHighX) + spec.PathWidth;
            AddLegacySupportProxy(
                topSizeX,
                spec.MountainHeight,
                spec.PathWidth,
                ref supportFrustums,
                ref proxy);

            return proxy;
        }

        private static int LegacyRampLocalZ(in MountainLandmarkSpec spec, int startY) =>
            spec.CentreLocal
            - spec.CoreRadiusAtHeight(startY)
            - spec.PathWidth
            - 10;

        private static void AddLegacySupportProxy(
            int longSize,
            int supportTopY,
            int pathWidth,
            ref int count,
            ref long proxy)
        {
            if (supportTopY <= 0) return;
            int segments = Math.Max(1, (longSize + LegacySupportSegmentSpan - 1) / LegacySupportSegmentSpan);
            int topRadius = Math.Max(40, pathWidth + 18);
            int flare = Math.Min(112, Math.Max(16, supportTopY * 2 / 5));
            int baseRadius = topRadius + flare;
            count += segments;
            proxy += segments * ConservativeFrustumRasterProxy(supportTopY + 1, baseRadius);
        }

        private static long ConservativeFrustumRasterProxy(int height, int baseRadius)
        {
            long diameter = baseRadius * 2L + 1L;
            return height * diameter * diameter;
        }

        private static EvidenceWaypoint FindWaypoint(EvidenceRoute route, string name)
        {
            foreach (EvidenceWaypoint waypoint in route.waypoints)
            {
                if (waypoint != null && string.Equals(waypoint.name, name, StringComparison.Ordinal))
                    return waypoint;
            }

            Assert.Fail("Evidence route is missing waypoint '" + name + "'.");
            return null;
        }

        private static void AssertWorldCoordinate(float actualMetres, int expectedVoxel, string label)
        {
            Assert.That(
                actualMetres,
                Is.EqualTo(expectedVoxel * VoxelSizeMetres).Within(0.001f),
                label + " must derive from MountainLandmarkSpec.PathTier.");
        }

        [Serializable]
        private sealed class EvidenceRoute
        {
            public EvidenceWaypoint[] waypoints;
        }

        [Serializable]
        private sealed class EvidenceWaypoint
        {
            public string name;
            public float x;
            public float z;
            public float expectedYOffset;
        }
    }
}
