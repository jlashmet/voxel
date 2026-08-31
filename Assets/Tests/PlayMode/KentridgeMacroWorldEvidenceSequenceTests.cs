using System;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using NUnit.Framework;
using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldEvidenceSequenceTests
    {
        private const BindingFlags InstancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const float SettlementSurveyHeightMetres = 70f;
        private const int SettlementSurveyHorizontalOffsetDm = 60;
        private const int BuildingFoundationInsetDm = 6;
        private const int BuildingRoofDm = 24;
        private const float EvidenceAspect = 1600f / 900f;
        private const float ViewportMargin = 0.04f;

        [Test]
        public void MoordellSettlesBeforeMacroRoadAndLaterTargetsKeepAcceptedOrder()
        {
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver",
                throwOnError: true);
            MethodInfo resolveContinuation = driverType.GetMethod("ResolveMoordellContinuation", StaticPrivate);
            MethodInfo buildTargets = driverType.GetMethod("BuildTargetsAndRoadTraversal", InstancePrivate);
            FieldInfo targetsField = driverType.GetField("_targets", InstancePrivate);

            Assert.That(resolveContinuation, Is.Not.Null);
            Assert.That(buildTargets, Is.Not.Null);
            Assert.That(targetsField, Is.Not.Null);

            AssertContinuation(resolveContinuation, targetCaptured: false, macroRoadCaptured: false, roadArrivalCaptured: false, "Survey");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: false, roadArrivalCaptured: false, "MacroRoad");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: true, roadArrivalCaptured: false, "RoadArrival");
            AssertContinuation(resolveContinuation, targetCaptured: true, macroRoadCaptured: true, roadArrivalCaptured: true, "Advance");

            var host = new GameObject("KentridgeMacroWorldEvidenceSequenceTests");
            host.SetActive(false);
            try
            {
                Component driver = host.AddComponent(driverType);
                buildTargets.Invoke(driver, null);

                var targets = (Array)targetsField.GetValue(driver);
                Assert.That(targets, Is.Not.Null);
                Assert.That(targets.Length, Is.EqualTo(7));

                string[] expected =
                {
                    "moordell",
                    "rossdam",
                    "rossdam-lake-detour",
                    "fairy-village",
                    "orc-village",
                    "southern-ridge-pass",
                    "macro-network-overview"
                };

                for (var i = 0; i < expected.Length; i++)
                {
                    object target = targets.GetValue(i);
                    PropertyInfo labelProperty = target.GetType().GetProperty("Label", BindingFlags.Instance | BindingFlags.Public);
                    Assert.That(labelProperty, Is.Not.Null);
                    Assert.That(labelProperty.GetValue(target), Is.EqualTo(expected[i]), "Unexpected evidence target at index " + i);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void MacroDriverRetainsAutomationAndKeepsQueuedMoordellSurveyCamera()
        {
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver",
                throwOnError: true);
            MethodInfo retainAutomation = driverType.GetMethod("RetainMacroValidationAutomation", StaticPrivate);
            MethodInfo shouldHoldSurvey = driverType.GetMethod("ShouldHoldMoordellSurveyAfterCapture", StaticPrivate);

            Assert.That(retainAutomation, Is.Not.Null);
            Assert.That(shouldHoldSurvey, Is.Not.Null);

            var host = new GameObject("KentridgeMacroWorldEvidenceOwnershipTests");
            host.SetActive(false);
            try
            {
                var slice = host.AddComponent<KentridgePlayableSlice>();
                slice.AutoSurvey = true;
                slice.AutoRecede = true;

                retainAutomation.Invoke(null, new object[] { slice });

                Assert.That(slice.AutoSurvey, Is.False,
                    "The macro validation driver must override a later generic survey toggle before streaming runs.");
                Assert.That(slice.AutoRecede, Is.False,
                    "The macro validation driver must override a later generic recede toggle before streaming runs.");

                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0f }), Is.EqualTo(true));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0.09f }), Is.EqualTo(true));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { true, 0.11f }), Is.EqualTo(false));
                Assert.That(shouldHoldSurvey.Invoke(null, new object[] { false, 0f }), Is.EqualTo(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void SettlementNearFieldCompositionContainsProjectedAuthoredBuildingBounds()
        {
            Type compositionType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldSettlementSurveyComposition",
                throwOnError: true);
            MethodInfo resolveFieldOfView = compositionType.GetMethod(
                "ResolveReadableSurveyFieldOfView",
                StaticPrivate);
            Assert.That(resolveFieldOfView, Is.Not.Null);

            float widened = (float)resolveFieldOfView.Invoke(null, new object[] { 58f });
            float alreadyWide = (float)resolveFieldOfView.Invoke(null, new object[] { 100f });

            Assert.That(widened, Is.EqualTo(90f).Within(0.001f),
                "The validation settlement survey must use the full projected 3D building envelope, not the earlier flat footprint/intersection approximation.");
            Assert.That(alreadyWide, Is.EqualTo(100f).Within(0.001f),
                "The validation composition must not narrow a camera that already has a wider lens.");

            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            AssertProjectedSettlementContainment(
                physical,
                MountingForceTopDownWorldDefinition.Moordell,
                widened);
            AssertProjectedSettlementContainment(
                physical,
                MountingForceTopDownWorldDefinition.Rossdam,
                widened);
        }

        private static void AssertProjectedSettlementContainment(
            TopDownWorldPhysicalPlan physical,
            string nodeId,
            float fieldOfView)
        {
            Assert.That(physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement), Is.True);
            Assert.That(settlement.Buildings.Count, Is.GreaterThanOrEqualTo(4));

            TopDownWorldBuildingBlockoutPlan first = settlement.Buildings[0];
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            var focusDm = new Int2((minX + maxX) / 2, (minZ + maxZ) / 2);
            var cameraDm = new Int2(
                focusDm.X + SettlementSurveyHorizontalOffsetDm,
                focusDm.Y + SettlementSurveyHorizontalOffsetDm);
            int cameraGround = TerrainSampler.HeightAt(cameraDm.X, cameraDm.Y, Seed);
            int focusGround = TerrainSampler.HeightAt(focusDm.X, focusDm.Y, Seed);

            var host = new GameObject("SettlementProjectedContainmentCamera");
            try
            {
                var camera = host.AddComponent<Camera>();
                camera.aspect = EvidenceAspect;
                camera.fieldOfView = fieldOfView;
                camera.transform.position = new Vector3(
                    cameraDm.X * DmToMetres,
                    cameraGround * DmToMetres + SettlementSurveyHeightMetres,
                    cameraDm.Y * DmToMetres);
                Vector3 focus = new Vector3(
                    focusDm.X * DmToMetres,
                    focusGround * DmToMetres + 8f,
                    focusDm.Y * DmToMetres);
                camera.transform.rotation = Quaternion.LookRotation(
                    (focus - camera.transform.position).normalized,
                    Vector3.up);

                for (var i = 0; i < settlement.Buildings.Count; i++)
                {
                    TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                    SampleGroundRange(building, out int minimumGround, out int maximumGround);
                    int minBuildingX = building.CentreDm.X - building.HalfExtentXDm - BuildingFoundationInsetDm;
                    int maxBuildingX = building.CentreDm.X + building.HalfExtentXDm + BuildingFoundationInsetDm;
                    int minBuildingZ = building.CentreDm.Y - building.HalfExtentZDm - BuildingFoundationInsetDm;
                    int maxBuildingZ = building.CentreDm.Y + building.HalfExtentZDm + BuildingFoundationInsetDm;
                    int topDm = maximumGround + building.HeightDm + BuildingRoofDm;

                    AssertProjectedCorner(camera, nodeId, i, minBuildingX, minimumGround, minBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, minBuildingX, topDm, maxBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, maxBuildingX, minimumGround, minBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, maxBuildingX, topDm, maxBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, minBuildingX, topDm, minBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, maxBuildingX, topDm, minBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, minBuildingX, minimumGround, maxBuildingZ);
                    AssertProjectedCorner(camera, nodeId, i, maxBuildingX, minimumGround, maxBuildingZ);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static void SampleGroundRange(
            TopDownWorldBuildingBlockoutPlan building,
            out int minimumGround,
            out int maximumGround)
        {
            minimumGround = int.MaxValue;
            maximumGround = int.MinValue;
            for (var z = 0; z < 5; z++)
            {
                int zDm = Mathf.RoundToInt(Mathf.Lerp(
                    building.CentreDm.Y - building.HalfExtentZDm,
                    building.CentreDm.Y + building.HalfExtentZDm,
                    z / 4f));
                for (var x = 0; x < 5; x++)
                {
                    int xDm = Mathf.RoundToInt(Mathf.Lerp(
                        building.CentreDm.X - building.HalfExtentXDm,
                        building.CentreDm.X + building.HalfExtentXDm,
                        x / 4f));
                    int ground = TerrainSampler.HeightAt(xDm, zDm, Seed);
                    minimumGround = Math.Min(minimumGround, ground);
                    maximumGround = Math.Max(maximumGround, ground);
                }
            }
        }

        private static void AssertProjectedCorner(
            Camera camera,
            string nodeId,
            int buildingIndex,
            int xDm,
            int yDm,
            int zDm)
        {
            Vector3 viewport = camera.WorldToViewportPoint(new Vector3(
                xDm * DmToMetres,
                yDm * DmToMetres,
                zDm * DmToMetres));
            Assert.That(viewport.z, Is.GreaterThan(0f),
                $"{nodeId} building {buildingIndex} corner is behind the settlement survey camera.");
            Assert.That(viewport.x, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                $"{nodeId} building {buildingIndex} corner x={viewport.x:0.000} is not fully contained.");
            Assert.That(viewport.y, Is.InRange(ViewportMargin, 1f - ViewportMargin),
                $"{nodeId} building {buildingIndex} corner y={viewport.y:0.000} is not fully contained.");
        }

        private static void AssertContinuation(
            MethodInfo resolveContinuation,
            bool targetCaptured,
            bool macroRoadCaptured,
            bool roadArrivalCaptured,
            string expected)
        {
            object result = resolveContinuation.Invoke(
                null,
                new object[] { targetCaptured, macroRoadCaptured, roadArrivalCaptured });
            Assert.That(result, Is.Not.Null);
            Assert.That(result.ToString(), Is.EqualTo(expected));
        }
    }
}
