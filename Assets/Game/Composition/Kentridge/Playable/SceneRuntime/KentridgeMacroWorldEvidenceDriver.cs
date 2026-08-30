using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Api;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Showcase;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Built-player evidence automation for capture-less macro-world validations. It is dormant in
    /// normal gameplay. When an assigned SceneIssue selects the reusable kentridge-macro-world
    /// validation profile, it exercises the production CharacterMotor in ordinary gameplay, settles
    /// the first semantic settlement target, then exercises that same motor on a generated macro-road
    /// before continuing through the remaining physical-plan targets. Production streaming/rendering
    /// remains authoritative throughout.
    /// </summary>
    internal sealed class KentridgeMacroWorldEvidenceDriver : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const float OpeningEvidenceTimeScale = 12f;
        private const float WalkEvidenceSeconds = 0.65f;
        private const float RoadPrestreamSeconds = 0.50f;
        private const float RoadWalkSeconds = 0.85f;
        private const float TargetMinimumDwellSeconds = 0.35f;
        private const float TargetPostCaptureSeconds = 0.10f;
        private const float ContentPendingLogIntervalSeconds = 1f;
        private const int StableCoverageFrames = 4;
        private const float DmToMetres = 0.1f;
        private const uint Seed = 0x4B454E54u;
        private const float SettlementSurveyHeightMetres = 70f;
        private const int SettlementSurveyHorizontalOffsetDm = 60;

        private static readonly FieldInfo s_WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_YawField = typeof(KentridgePlayableSlice).GetField(
            "_yaw",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_PresentationField = typeof(KentridgePlayableSlice).GetField(
            "_presentation",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private ShowcaseWorld _world;
        private KentridgeCharacterHost _motor;
        private object _openingPresentation;
        private PropertyInfo _pendingDialogueProperty;
        private MethodInfo _dismissPendingDialogueMethod;
        private EvidenceTarget[] _targets;
        private string _screenshotDirectory;
        private float _gameplayStartedAt = -1f;
        private Vector3 _walkStartedAt;
        private bool _walkRecorded;
        private Int2 _roadStartDm;
        private Int2 _roadNextDm;
        private bool _roadPrepared;
        private float _roadSequenceStartedAt = -1f;
        private bool _roadWalkStarted;
        private Vector3 _roadWalkStartedAt;
        private bool _roadWalkRecorded;
        private bool _roadCaptured;
        private float _roadCaptureStartedAt;
        private int _stableCoverageFrames;
        private int _targetIndex = -1;
        private bool _targetContentReadyLogged;
        private float _targetStartedAt;
        private bool _targetCaptured;
        private float _targetCapturedAt;
        private float _nextContentPendingLogAt;
        private bool _moordellRoadArrivalPending;
        private bool _moordellRoadArrivalCaptured;
        private float _moordellRoadArrivalStartedAt;
        private float _originalTimeScale = 1f;
        private bool _timeScaleBoosted;

        private enum MoordellContinuation
        {
            Survey,
            MacroRoad,
            RoadArrival,
            Advance
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro World Evidence");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldEvidenceDriver>();
        }

        private void OnEnable()
        {
            _originalTimeScale = Time.timeScale;
            Time.timeScale = OpeningEvidenceTimeScale;
            _timeScaleBoosted = true;
            Debug.Log($"MACROEVIDENCE opening-time-scale={OpeningEvidenceTimeScale:0.##}");
        }

        private void OnDisable() => RestoreTimeScale();

        private void OnDestroy() => RestoreTimeScale();

        private void Update()
        {
            if (_slice == null)
            {
                _slice = FindFirstObjectByType<KentridgePlayableSlice>();
                if (_slice == null) return;
                if (s_WorldField == null || s_MotorField == null || s_YawField == null || s_PresentationField == null)
                    throw new InvalidOperationException(
                        "Macro evidence driver cannot resolve Kentridge world/CharacterMotor/yaw/presentation host state.");
                _screenshotDirectory = ReadArgument("-voxel-screenshot-dir");
            }

            _world ??= s_WorldField.GetValue(_slice) as ShowcaseWorld;
            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_world == null || _motor == null) return;
            if (!_slice.GameplayControlEnabled)
            {
                DismissPendingOpeningDialogue();
                return;
            }

            RestoreTimeScale();

            if (_gameplayStartedAt < 0f)
            {
                _gameplayStartedAt = Time.realtimeSinceStartup;
                _walkStartedAt = _motor.Position;
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                Debug.Log("MACROEVIDENCE phase=local-character-motor-walk start=" + Format(_walkStartedAt));
                return;
            }

            float elapsed = Time.realtimeSinceStartup - _gameplayStartedAt;
            if (elapsed < WalkEvidenceSeconds)
            {
                _slice.AutoSurvey = false;
                _slice.AutoRecede = false;
                _slice.AutoWalk = true;
                return;
            }

            if (!_walkRecorded)
            {
                _walkRecorded = true;
                Vector3 delta = _motor.Position - _walkStartedAt;
                delta.y = 0f;
                Debug.Log($"MACROEVIDENCE traversal=CharacterMotor-local metres={delta.magnitude:0.00} " +
                          $"from={Format(_walkStartedAt)} to={Format(_motor.Position)}");
                BuildTargetsAndRoadTraversal();
            }

            _slice.AutoSurvey = false;
            _slice.AutoRecede = false;
            if (_targets == null || _targets.Length == 0) return;
            if (_targetIndex < 0)
                BeginTarget(0);

            EvidenceTarget target = _targets[_targetIndex];
            float now = Time.realtimeSinceStartup;

            if (IsMoordell(target) && _targetCaptured)
            {
                MoordellContinuation continuation = ResolveMoordellContinuation(
                    _targetCaptured,
                    _roadCaptured,
                    _moordellRoadArrivalCaptured);
                if (now - _targetCapturedAt < TargetPostCaptureSeconds)
                {
                    _slice.AutoWalk = false;
                    return;
                }

                if (continuation == MoordellContinuation.MacroRoad)
                {
                    RunMacroRoadEvidence(now);
                    return;
                }

                if (continuation == MoordellContinuation.RoadArrival)
                {
                    RunMoordellRoadArrival(now, target);
                    return;
                }

                if (continuation == MoordellContinuation.Advance)
                {
                    _slice.AutoWalk = false;
                    if (_targetIndex + 1 < _targets.Length)
                        BeginTarget(_targetIndex + 1);
                    return;
                }
            }

            _slice.AutoWalk = false;
            PinToTargetDemand(target);
            if (!_targetCaptured && !AreTargetContentSettled(target))
            {
                _stableCoverageFrames = 0;
                return;
            }

            if (!_targetCaptured && !_targetContentReadyLogged)
            {
                _targetContentReadyLogged = true;
                _targetStartedAt = now;
                Debug.Log(
                    $"MACROEVIDENCE content-ready target={target.Label} columns={target.ContentDm.Length}");
            }

            float targetElapsed = now - _targetStartedAt;
            if (!_targetCaptured
                && targetElapsed >= TargetMinimumDwellSeconds
                && AdvanceStableCoverage(true))
            {
                _targetCaptured = true;
                _targetCapturedAt = now;
                Debug.Log(
                    $"MACROEVIDENCE capture-ready target={target.Label} coverage=True stableFrames={_stableCoverageFrames}");
                CaptureTarget(target);
            }

            if (_targetCaptured
                && !IsMoordell(target)
                && now - _targetCapturedAt >= TargetPostCaptureSeconds
                && _targetIndex + 1 < _targets.Length)
                BeginTarget(_targetIndex + 1);
        }

        private void LateUpdate()
        {
            RetainMacroValidationAutomation(_slice);
            if (_targetIndex < 0 || _targets == null || _targetIndex >= _targets.Length || _motor == null)
                return;

            EvidenceTarget target = _targets[_targetIndex];
            if (IsMoordell(target) && _targetCaptured)
            {
                if (ShouldHoldMoordellSurveyAfterCapture(
                        _targetCaptured,
                        Time.realtimeSinceStartup - _targetCapturedAt))
                {
                    ApplySurveyCamera(target);
                    return;
                }

                MoordellContinuation continuation = ResolveMoordellContinuation(
                    _targetCaptured,
                    _roadCaptured,
                    _moordellRoadArrivalCaptured);
                if (continuation == MoordellContinuation.MacroRoad)
                    return;
                if (_moordellRoadArrivalPending)
                {
                    ApplyRoadArrivalCamera(target);
                    return;
                }
            }

            ApplySurveyCamera(target);
        }

        private static MoordellContinuation ResolveMoordellContinuation(
            bool targetCaptured,
            bool macroRoadCaptured,
            bool roadArrivalCaptured)
        {
            if (!targetCaptured) return MoordellContinuation.Survey;
            if (!macroRoadCaptured) return MoordellContinuation.MacroRoad;
            if (!roadArrivalCaptured) return MoordellContinuation.RoadArrival;
            return MoordellContinuation.Advance;
        }

        private static void RetainMacroValidationAutomation(KentridgePlayableSlice slice)
        {
            if (slice == null) return;
            slice.AutoSurvey = false;
            slice.AutoRecede = false;
        }

        private static bool ShouldHoldMoordellSurveyAfterCapture(
            bool targetCaptured,
            float elapsedSinceCapture) =>
            targetCaptured && elapsedSinceCapture < TargetPostCaptureSeconds;

        private void RunMacroRoadEvidence(float now)
        {
            if (_roadSequenceStartedAt < 0f)
            {
                _roadSequenceStartedAt = now;
                _stableCoverageFrames = 0;
                Debug.Log("MACROEVIDENCE phase=macro-road-prestream-after-moordell");
            }

            float roadElapsed = now - _roadSequenceStartedAt;
            if (roadElapsed < RoadPrestreamSeconds)
            {
                _slice.AutoWalk = false;
                PinToRoadStart();
                return;
            }

            if (roadElapsed < RoadPrestreamSeconds + RoadWalkSeconds)
            {
                if (!_roadWalkStarted)
                {
                    PinToRoadStart();
                    _roadWalkStarted = true;
                    _roadWalkStartedAt = _motor.Position;
                    Debug.Log(
                        "MACROEVIDENCE phase=macro-road-character-motor start=" + Format(_roadWalkStartedAt) +
                        $" startDm=({_roadStartDm.X},{_roadStartDm.Y}) nextDm=({_roadNextDm.X},{_roadNextDm.Y})");
                }

                HoldRoadHeading();
                _slice.AutoWalk = true;
                return;
            }

            _slice.AutoWalk = false;
            if (!_roadWalkRecorded)
            {
                _roadWalkRecorded = true;
                _roadCaptureStartedAt = now;
                _stableCoverageFrames = 0;
                Vector3 delta = _motor.Position - _roadWalkStartedAt;
                delta.y = 0f;
                Debug.Log($"MACROEVIDENCE traversal=CharacterMotor-macro-road metres={delta.magnitude:0.00} " +
                          $"from={Format(_roadWalkStartedAt)} to={Format(_motor.Position)}");
            }

            if (_roadCaptured) return;
            HoldRoadHeading();
            _slice.transform.position = _motor.EyePosition;
            if (now - _roadCaptureStartedAt < TargetMinimumDwellSeconds
                || !HasStablePublishedCoverageAt(_motor.Position))
                return;

            _roadCaptured = true;
            _targetCapturedAt = now;
            _stableCoverageFrames = 0;
            Debug.Log(
                $"MACROEVIDENCE capture-ready target=macro-road-character-motor coverage=True stableFrames={StableCoverageFrames}");
            CaptureNamed("macro-road-character-motor");
        }

        private void RunMoordellRoadArrival(float now, EvidenceTarget target)
        {
            _slice.AutoWalk = false;
            if (!_moordellRoadArrivalPending)
            {
                _moordellRoadArrivalPending = true;
                _moordellRoadArrivalStartedAt = now;
                _stableCoverageFrames = 0;
                Debug.Log("MACROEVIDENCE target=moordell-road-arrival playerHeight=True");
            }

            PinToRoadStart();
            if (now - _moordellRoadArrivalStartedAt < TargetMinimumDwellSeconds
                || !HasStablePublishedCoverageAt(_motor.Position))
                return;

            _moordellRoadArrivalCaptured = true;
            _targetCapturedAt = now;
            _stableCoverageFrames = 0;
            Debug.Log(
                "MACROEVIDENCE capture-ready target=moordell-road-arrival coverage=True playerHeight=True");
            CaptureNamed("macro-moordell-road-arrival");
        }

        private void DismissPendingOpeningDialogue()
        {
            _openingPresentation ??= s_PresentationField.GetValue(_slice);
            if (_openingPresentation == null) return;

            Type presentationType = _openingPresentation.GetType();
            _pendingDialogueProperty ??= presentationType.GetProperty(
                "Pending",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _dismissPendingDialogueMethod ??= presentationType.GetMethod(
                "DismissPending",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_pendingDialogueProperty == null || _dismissPendingDialogueMethod == null)
                throw new InvalidOperationException(
                    "Macro evidence driver cannot resolve Kentridge pending-dialogue presentation state.");

            if (_pendingDialogueProperty.GetValue(_openingPresentation) == null) return;
            _dismissPendingDialogueMethod.Invoke(_openingPresentation, null);
            Debug.Log("MACROEVIDENCE opening-dialogue-dismissed");
        }

        private bool HasStablePublishedCoverageAt(Vector3 presentationPoint)
        {
            return AdvanceStableCoverage(
                _world.IsPresentationColumnContentSettled(presentationPoint));
        }

        private bool AreTargetContentSettled(EvidenceTarget target)
        {
            bool allSettled = true;
            float now = Time.realtimeSinceStartup;
            bool logPending = now >= _nextContentPendingLogAt;
            for (var i = 0; i < target.ContentDm.Length; i++)
            {
                Int2 point = target.ContentDm[i];
                if (IsContentSettled(point)) continue;
                allSettled = false;
                if (!logPending) continue;

                int regionX = Mathf.FloorToInt(point.X * DmToMetres / ShowcaseWorld.RegionMetres);
                int regionZ = Mathf.FloorToInt(point.Y * DmToMetres / ShowcaseWorld.RegionMetres);
                Debug.Log(
                    $"MACROEVIDENCE content-pending target={target.Label} index={i}" +
                    $" centreDm=({point.X},{point.Y}) regionXZ=({regionX},{regionZ})");
            }

            if (!allSettled && logPending)
                _nextContentPendingLogAt = now + ContentPendingLogIntervalSeconds;
            return allSettled;
        }

        private bool IsContentSettled(Int2 point)
        {
            int ground = TerrainSampler.HeightAt(point.X, point.Y, Seed);
            var worldPoint = new Vector3(
                point.X * DmToMetres,
                ground * DmToMetres,
                point.Y * DmToMetres);
            return _world.IsPresentationColumnContentSettled(worldPoint);
        }

        private bool AdvanceStableCoverage(bool contentSettled)
        {
            if (!contentSettled
                || !RenderingComposition.HasCompletePublishedNearSurfaceCoverage())
            {
                _stableCoverageFrames = 0;
                return false;
            }

            _stableCoverageFrames++;
            return _stableCoverageFrames >= StableCoverageFrames;
        }

        private void RestoreTimeScale()
        {
            if (!_timeScaleBoosted) return;
            Time.timeScale = _originalTimeScale;
            _timeScaleBoosted = false;
            Debug.Log($"MACROEVIDENCE restored-time-scale={Time.timeScale:0.##}");
        }

        private void BeginTarget(int index)
        {
            _targetIndex = index;
            _targetContentReadyLogged = false;
            _targetStartedAt = Time.realtimeSinceStartup;
            _targetCaptured = false;
            _targetCapturedAt = 0f;
            _nextContentPendingLogAt = 0f;
            _moordellRoadArrivalPending = false;
            _stableCoverageFrames = 0;
            EvidenceTarget target = _targets[index];
            Debug.Log(
                "MACROEVIDENCE target=" + target.Label +
                $" cameraDm=({target.CameraDm.X},{target.CameraDm.Y})" +
                $" focusDm=({target.FocusDm.X},{target.FocusDm.Y})" +
                $" contentColumns={target.ContentDm.Length}" +
                $" cameraHeightM={target.CameraHeightMetres:0.0}");
        }

        private void BuildTargetsAndRoadTraversal()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            TopDownWorldPhysicalRoutePlan moordellArrival = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.Moordell);
            if (moordellArrival.Tiles.Count < 2)
                throw new InvalidOperationException("Moordell macro road has no traversable segment for evidence.");
            int roadIndex = Math.Max(0, moordellArrival.Tiles.Count - 10);
            roadIndex = Math.Min(roadIndex, moordellArrival.Tiles.Count - 2);
            _roadStartDm = moordellArrival.Tiles[roadIndex];
            _roadNextDm = moordellArrival.Tiles[roadIndex + 1];
            _roadPrepared = true;

            var targets = new List<EvidenceTarget>(7)
            {
                SettlementSurvey(physical, MountingForceTopDownWorldDefinition.Moordell),
                SettlementSurvey(physical, MountingForceTopDownWorldDefinition.Rossdam)
            };

            if (!physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.RossdamLake, out TopDownWorldRegionPlan lake))
                throw new InvalidOperationException("Macro evidence plan has no Rossdam lake.");
            TopDownWorldPhysicalRoutePlan rossdamRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.MoordellCorridor,
                MountingForceTopDownWorldDefinition.RossdamApproach);
            Int2 lakeRoutePoint = ClosestRoutePoint(rossdamRoute, lake.CentreDm);
            Vector2 lakeOutward = new Vector2(
                lakeRoutePoint.X - lake.CentreDm.X,
                lakeRoutePoint.Y - lake.CentreDm.Y);
            if (lakeOutward.sqrMagnitude < 1f) lakeOutward = Vector2.right;
            lakeOutward.Normalize();
            var lakeCamera = new Int2(
                lakeRoutePoint.X + Mathf.RoundToInt(lakeOutward.x * 140f),
                lakeRoutePoint.Y + Mathf.RoundToInt(lakeOutward.y * 140f));
            targets.Add(new EvidenceTarget(
                "rossdam-lake-detour",
                lakeCamera,
                lake.CentreDm,
                cameraHeightMetres: 24f,
                elevated: true));

            targets.Add(SettlementSurvey(physical, MountingForceTopDownWorldDefinition.FairyVillage));
            targets.Add(SettlementSurvey(physical, MountingForceTopDownWorldDefinition.OrcVillage));

            if (!physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernRidge, out TopDownWorldRegionPlan ridge)
                || !physical.TryGetRegion(KentridgeTopDownWorldPhysicalIntent.SouthernPass, out TopDownWorldRegionPlan pass))
                throw new InvalidOperationException("Macro evidence plan has no southern ridge/pass.");
            TopDownWorldPhysicalRoutePlan loganRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.SouthFightingArea,
                MountingForceTopDownWorldDefinition.LoganApproach);
            Int2 ridgeRoad = pass.CentreDm;
            for (var i = 0; i < loganRoute.Tiles.Count; i++)
            {
                if (!ridge.Contains(loganRoute.Tiles[i])) continue;
                ridgeRoad = loganRoute.Tiles[i];
                break;
            }
            var ridgeCamera = new Int2(ridgeRoad.X - 190, ridgeRoad.Y - 170);
            targets.Add(new EvidenceTarget(
                "southern-ridge-pass",
                ridgeCamera,
                ridgeRoad,
                cameraHeightMetres: 32f,
                elevated: true));

            TopDownWorldPhysicalRoutePlan orcRoute = FindRoute(
                physical,
                MountingForceTopDownWorldDefinition.SouthFightingArea,
                MountingForceTopDownWorldDefinition.OrcVillage);
            Int2 networkFocus = loganRoute.Tiles[0];
            if (orcRoute.Tiles.Count > 0)
            {
                networkFocus = new Int2(
                    (networkFocus.X + orcRoute.Tiles[0].X) / 2,
                    (networkFocus.Y + orcRoute.Tiles[0].Y) / 2);
            }
            var networkCamera = new Int2(networkFocus.X - 220, networkFocus.Y - 190);
            targets.Add(new EvidenceTarget(
                "macro-network-overview",
                networkCamera,
                networkFocus,
                cameraHeightMetres: 40f,
                elevated: true));
            _targets = targets.ToArray();
            Debug.Log("MACROEVIDENCE target-order=moordell-road-rossdam-lake-fairy-orc-ridge-network");
        }

        private static EvidenceTarget SettlementSurvey(
            TopDownWorldPhysicalPlan physical,
            string nodeId)
        {
            if (!physical.TryGetSettlement(nodeId, out TopDownWorldSettlementPlan settlement))
                throw new InvalidOperationException("Macro evidence plan has no settlement '" + nodeId + "'.");
            if (settlement.Buildings.Count < 4)
                throw new InvalidOperationException(
                    "Macro evidence settlement '" + nodeId + "' does not expose the expected four blockout plots.");

            var contentDm = new Int2[settlement.Buildings.Count];
            TopDownWorldBuildingBlockoutPlan first = settlement.Buildings[0];
            contentDm[0] = first.CentreDm;
            int minX = first.CentreDm.X - first.HalfExtentXDm;
            int maxX = first.CentreDm.X + first.HalfExtentXDm;
            int minZ = first.CentreDm.Y - first.HalfExtentZDm;
            int maxZ = first.CentreDm.Y + first.HalfExtentZDm;
            for (var i = 1; i < settlement.Buildings.Count; i++)
            {
                TopDownWorldBuildingBlockoutPlan building = settlement.Buildings[i];
                contentDm[i] = building.CentreDm;
                minX = Math.Min(minX, building.CentreDm.X - building.HalfExtentXDm);
                maxX = Math.Max(maxX, building.CentreDm.X + building.HalfExtentXDm);
                minZ = Math.Min(minZ, building.CentreDm.Y - building.HalfExtentZDm);
                maxZ = Math.Max(maxZ, building.CentreDm.Y + building.HalfExtentZDm);
            }

            var focusDm = new Int2((minX + maxX) / 2, (minZ + maxZ) / 2);
            var surveyCamera = new Int2(
                focusDm.X + SettlementSurveyHorizontalOffsetDm,
                focusDm.Y + SettlementSurveyHorizontalOffsetDm);

            return new EvidenceTarget(
                nodeId,
                surveyCamera,
                focusDm,
                cameraHeightMetres: SettlementSurveyHeightMetres,
                elevated: true,
                contentDm: contentDm);
        }

        private static Int2 ClosestRoutePoint(TopDownWorldPhysicalRoutePlan route, Int2 point)
        {
            Int2 closest = route.Tiles[0];
            long bestDistanceSquared = long.MaxValue;
            for (var i = 0; i < route.Tiles.Count; i++)
            {
                long dx = route.Tiles[i].X - point.X;
                long dz = route.Tiles[i].Y - point.Y;
                long distanceSquared = dx * dx + dz * dz;
                if (distanceSquared >= bestDistanceSquared) continue;
                closest = route.Tiles[i];
                bestDistanceSquared = distanceSquared;
            }
            return closest;
        }

        private void PinToRoadStart()
        {
            if (!_roadPrepared) return;
            int ground = TerrainSampler.HeightAt(_roadStartDm.X, _roadStartDm.Y, Seed);
            _motor.Position = new Vector3(
                _roadStartDm.X * DmToMetres,
                ground * DmToMetres + 0.1f,
                _roadStartDm.Y * DmToMetres);
            _motor.Velocity = Vector3.zero;
            HoldRoadHeading();
            _slice.transform.position = _motor.EyePosition;
        }

        private void HoldRoadHeading()
        {
            Vector3 direction = new Vector3(
                _roadNextDm.X - _roadStartDm.X,
                0f,
                _roadNextDm.Y - _roadStartDm.Y);
            if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            s_YawField.SetValue(_slice, yaw);
            _slice.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static TopDownWorldPhysicalRoutePlan FindRoute(
            TopDownWorldPhysicalPlan physical,
            string fromId,
            string toId)
        {
            if (!physical.TryGetRoute(fromId, toId, out TopDownWorldPhysicalRoutePlan route))
                throw new InvalidOperationException("Macro evidence plan has no route '" + fromId + "->" + toId + "'.");
            return route;
        }

        private void PinToTargetDemand(EvidenceTarget target)
        {
            int x = target.FocusDm.X;
            int z = target.FocusDm.Y;
            int ground = TerrainSampler.HeightAt(x, z, Seed);
            _motor.Position = new Vector3(
                x * DmToMetres,
                ground * DmToMetres + 0.1f,
                z * DmToMetres);
            _motor.Velocity = Vector3.zero;
        }

        private void ApplySurveyCamera(EvidenceTarget target)
        {
            if (_slice == null) return;
            int cameraGround = TerrainSampler.HeightAt(target.CameraDm.X, target.CameraDm.Y, Seed);
            _slice.transform.position = new Vector3(
                target.CameraDm.X * DmToMetres,
                cameraGround * DmToMetres + target.CameraHeightMetres,
                target.CameraDm.Y * DmToMetres);
            int focusGround = TerrainSampler.HeightAt(target.FocusDm.X, target.FocusDm.Y, Seed);
            Vector3 focus = new Vector3(
                target.FocusDm.X * DmToMetres,
                focusGround * DmToMetres + (target.Elevated ? 8f : 5f),
                target.FocusDm.Y * DmToMetres);
            Vector3 direction = focus - _slice.transform.position;
            if (direction.sqrMagnitude > 0.01f)
                _slice.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void ApplyRoadArrivalCamera(EvidenceTarget target)
        {
            if (_slice == null || _motor == null) return;
            _slice.transform.position = _motor.EyePosition;
            int focusGround = TerrainSampler.HeightAt(target.FocusDm.X, target.FocusDm.Y, Seed);
            Vector3 focus = new Vector3(
                target.FocusDm.X * DmToMetres,
                focusGround * DmToMetres + 5f,
                target.FocusDm.Y * DmToMetres);
            Vector3 direction = focus - _slice.transform.position;
            if (direction.sqrMagnitude > 0.01f)
                _slice.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private static bool IsMoordell(EvidenceTarget target) =>
            string.Equals(
                target.Label,
                MountingForceTopDownWorldDefinition.Moordell,
                StringComparison.Ordinal);

        private void CaptureTarget(EvidenceTarget target) => CaptureNamed("macro-" + target.Label);

        private void CaptureNamed(string name)
        {
            if (string.IsNullOrWhiteSpace(_screenshotDirectory)) return;
            Directory.CreateDirectory(_screenshotDirectory);
            string path = Path.Combine(_screenshotDirectory, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log("MACROEVIDENCE screenshot=" + path);
        }

        private static bool TryReadValidationProfile(out string profile)
        {
            profile = null;
            string path = ReadArgument("-voxel-scene-issue");
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return false;
            string json = File.ReadAllText(path);
            const string key = "\"validationProfile\"";
            int keyIndex = json.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex < 0) return false;
            int colon = json.IndexOf(':', keyIndex + key.Length);
            int firstQuote = colon >= 0 ? json.IndexOf('"', colon + 1) : -1;
            int secondQuote = firstQuote >= 0 ? json.IndexOf('"', firstQuote + 1) : -1;
            if (firstQuote < 0 || secondQuote <= firstQuote) return false;
            profile = json.Substring(firstQuote + 1, secondQuote - firstQuote - 1);
            return true;
        }

        private static string ReadArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal)) return args[i + 1];
            return null;
        }

        private static string Format(Vector3 value) =>
            $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";

        private readonly struct EvidenceTarget
        {
            public string Label { get; }
            public Int2 CameraDm { get; }
            public Int2 FocusDm { get; }
            public float CameraHeightMetres { get; }
            public bool Elevated { get; }
            public Int2[] ContentDm { get; }

            public EvidenceTarget(
                string label,
                Int2 cameraDm,
                Int2 focusDm,
                float cameraHeightMetres,
                bool elevated,
                Int2[] contentDm = null)
            {
                Label = label;
                CameraDm = cameraDm;
                FocusDm = focusDm;
                CameraHeightMetres = cameraHeightMetres;
                Elevated = elevated;
                ContentDm = contentDm ?? new[] { focusDm };
            }
        }
    }
}
