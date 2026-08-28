using System;
using System.Reflection;
using Game.Cutscenes.Content.Kentridge;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using VoxelEngine.Composition;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Presentation-only treatment for the recovered Kentridge opening.
    ///
    /// The generated pub remains authoritative geometry. During the opening this component raises
    /// the existing stage camera into a slightly oblique top-down view and asks the renderer to hide
    /// only the generated pub volume above the ground-floor walls. That exposes the room without
    /// carving voxels, changing collision, or sectioning actors/terrain with the camera near plane.
    /// It also owns the black fades around the opening so the camera/cutaway handoff never appears
    /// as a pop.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [AddComponentMenu("Game/Kentridge Opening Presentation")]
    public sealed class KentridgeOpeningPresentation : MonoBehaviour
    {
        private const float DecimetresPerMetre = 10f;
        private const float OpeningFocusAboveFloorMetres = 0.9f;

        public enum TransitionPhase : byte
        {
            WaitingForOpening = 0,
            FadingIntoCutscene = 1,
            CutsceneVisible = 2,
            FadingOutCutscene = 3,
            HoldingBlackForHandoff = 4,
            FadingIntoGameplay = 5,
            Complete = 6,
        }

        [Header("Opening camera")]
        [SerializeField] private float m_HeightMetres = 7.2f;
        [SerializeField] private float m_BackMetres = 3.5f;
        [Tooltip("Ground-floor wall height retained by the renderer cutaway. Geometry above this " +
                 "height is hidden only inside the generated Pub footprint.")]
        [SerializeField] private float m_CutawayHeightMetres = 2.8f;
        // Legacy scene data from the former camera-near-plane cutaway. Keep it only as a fallback
        // for older serialized scenes; new presentation uses the bounded world-voxel volume above.
        [SerializeField, HideInInspector] private float m_VisibleDepthMetres = 2.0f;

        [Header("Fades")]
        [SerializeField] private float m_FadeInSeconds = 0.8f;
        [SerializeField] private float m_FadeOutSeconds = 0.65f;
        [SerializeField] private float m_GameplayFadeInSeconds = 0.45f;

        private KentridgePlayableSlice _slice;
        private Camera _camera;
        private float _gameplayNearClip;
        private Vector3 _stageApproach = Vector3.forward;
        private bool _hasStageApproach;
        private bool _wasOpeningCameraActive;
        private float _fadeAlpha = 1f;
        private TransitionPhase _phase = TransitionPhase.WaitingForOpening;
        private FieldInfo _kentridgePlanField;
        private SettlementPlan _kentridgePlan;
        private bool _cutawayBoundsReady;
        private Vector3 _cutawayMinVoxel;
        private Vector3 _cutawayMaxVoxel;

        // SlicePresentation is intentionally private to the playable slice. Until cutscene
        // presentation exposes a general transition cue channel, observe only its public LastCue
        // property through one cached scene-local reflection bridge. No actor/world state is read.
        private FieldInfo _presentationField;
        private object _presentation;
        private PropertyInfo _lastCueProperty;

        public bool OpeningOverheadActive { get; private set; }
        public bool RoofCutawayActive { get; private set; }
        public float FadeAlpha => _fadeAlpha;
        public TransitionPhase FadePhase => _phase;
        public string LastObservedCue { get; private set; } = string.Empty;

        private void Awake()
        {
            _slice = GetComponent<KentridgePlayableSlice>();
            _camera = GetComponent<Camera>();
            if (_slice == null)
                throw new InvalidOperationException(
                    "Kentridge opening presentation requires KentridgePlayableSlice on the same camera.");
            _gameplayNearClip = _camera.nearClipPlane;
            _presentationField = typeof(KentridgePlayableSlice).GetField(
                "_presentation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_presentationField == null)
                throw new InvalidOperationException(
                    "Kentridge opening presentation cannot observe the slice presentation cue channel.");
            _kentridgePlanField = typeof(KentridgePlayableSlice).GetField(
                "_kentridgePlan",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (_kentridgePlanField == null)
                throw new InvalidOperationException(
                    "Kentridge opening presentation cannot resolve the generated Pub footprint.");
        }

        private void OnEnable()
        {
            _fadeAlpha = 1f;
            _phase = TransitionPhase.WaitingForOpening;
            _wasOpeningCameraActive = false;
            OpeningOverheadActive = false;
            RoofCutawayActive = false;
            LastObservedCue = string.Empty;
            _kentridgePlan = null;
            _cutawayBoundsReady = false;
            RenderingComposition.SetCutaway(false, Vector3.zero, Vector3.zero);
        }

        private void OnDisable()
        {
            RestoreGameplayCameraPresentation();
        }

        private void LateUpdate()
        {
            if (_slice == null || _camera == null) return;

            // Measurement cameras deliberately supersede the authored opening shot. Never let the
            // cutscene presentation layer pull a survey/recede/walk capture back into the pub.
            if (_slice.AutoSurvey || _slice.AutoRecede || _slice.AutoWalk)
            {
                RestoreGameplayCameraPresentation();
                _fadeAlpha = 0f;
                _phase = TransitionPhase.Complete;
                _wasOpeningCameraActive = false;
                return;
            }

            bool openingCameraActive =
                _slice.OpeningCutsceneStarted && _slice.OpeningCutsceneCameraActive;

            if (openingCameraActive)
            {
                CaptureStageApproachIfNeeded();
                ApplyOverheadCutaway();
                LastObservedCue = ReadLastPresentationCue();

                if (string.Equals(
                        LastObservedCue,
                        KentridgeOpeningCutscene.FadeOutPresentation.Value,
                        StringComparison.Ordinal))
                {
                    if (_phase != TransitionPhase.FadingOutCutscene
                        && _phase != TransitionPhase.HoldingBlackForHandoff)
                        _phase = TransitionPhase.FadingOutCutscene;
                }
                else if (_phase == TransitionPhase.WaitingForOpening)
                {
                    _phase = TransitionPhase.FadingIntoCutscene;
                }
            }
            else if (_wasOpeningCameraActive)
            {
                RestoreGameplayCameraPresentation();
                // The authored closing hold keeps the opening alive until the fade reaches black.
                // If a future content edit removes that hold, fail visually closed rather than
                // revealing the camera/cutaway snap for a frame.
                _fadeAlpha = Mathf.Max(_fadeAlpha, 0.98f);
                _phase = TransitionPhase.FadingIntoGameplay;
            }

            StepFade(Time.unscaledDeltaTime);
            _wasOpeningCameraActive = openingCameraActive;
        }

        private void CaptureStageApproachIfNeeded()
        {
            if (_hasStageApproach) return;

            Vector3 focus = _slice.OpeningCutsceneCameraFocus;
            Vector3 towardFocus = focus - transform.position;
            towardFocus.y = 0f;
            if (towardFocus.sqrMagnitude < 1e-5f)
            {
                towardFocus = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            }
            if (towardFocus.sqrMagnitude < 1e-5f) towardFocus = Vector3.forward;

            _stageApproach = towardFocus.normalized;
            _hasStageApproach = true;
        }

        private void ApplyOverheadCutaway()
        {
            Vector3 focus = _slice.OpeningCutsceneCameraFocus;
            Vector3 position = focus - _stageApproach * Mathf.Max(0f, m_BackMetres)
                                     + Vector3.up * Mathf.Max(0.5f, m_HeightMetres);
            Quaternion rotation = Quaternion.LookRotation(focus - position, Vector3.up);

            transform.SetPositionAndRotation(position, rotation);

            // The former cutaway advanced this plane to roughly six metres, which sliced every
            // renderer in camera space — including actors and foreground terrain. Keep projection
            // identical to normal gameplay and cut only bounded voxel geometry instead.
            _camera.nearClipPlane = _gameplayNearClip;

            Vector3 cutawayMin;
            Vector3 cutawayMax;
            if (TryResolvePubCutawayBounds(out cutawayMin, out cutawayMax))
            {
                RenderingComposition.SetCutaway(true, cutawayMin, cutawayMax);
                RoofCutawayActive = true;
            }
            else
            {
                RenderingComposition.SetCutaway(false, Vector3.zero, Vector3.zero);
                RoofCutawayActive = false;
            }

            OpeningOverheadActive = true;
        }

        private bool TryResolvePubCutawayBounds(out Vector3 minVoxel, out Vector3 maxVoxel)
        {
            if (_cutawayBoundsReady)
            {
                minVoxel = _cutawayMinVoxel;
                maxVoxel = _cutawayMaxVoxel;
                return true;
            }

            if (_kentridgePlan == null)
                _kentridgePlan = _kentridgePlanField.GetValue(_slice) as SettlementPlan;
            if (_kentridgePlan == null)
            {
                minVoxel = default;
                maxVoxel = default;
                return false;
            }

            for (int i = 0; i < _kentridgePlan.Plots.Count; i++)
            {
                BuildingPlot plot = _kentridgePlan.Plots[i];
                if (plot.RoleId != (int)KentridgeRole.Pub) continue;

                Int3 envelope = KentridgeDefinition.FootprintDm(plot.Archetype);
                int roofOverhang = KentridgeDefinition.Theme.RoofOverhangDm;
                float floorMetres = _slice.OpeningCutsceneCameraFocus.y
                                   - OpeningFocusAboveFloorMetres;
                float requestedWallHeight = m_CutawayHeightMetres > 0.1f
                    ? m_CutawayHeightMetres
                    : Mathf.Max(0.1f, m_VisibleDepthMetres);
                float minimumWallHeight = KentridgeDefinition.Theme.DoorHeightDm
                                        / DecimetresPerMetre;
                float maximumWallHeight = (KentridgeDefinition.Theme.FloorHeightDm - 1)
                                        / DecimetresPerMetre;
                float wallHeightMetres = Mathf.Clamp(
                    requestedWallHeight,
                    minimumWallHeight,
                    maximumWallHeight);
                float floorVoxelY = floorMetres * DecimetresPerMetre;

                _cutawayMinVoxel = new Vector3(
                    plot.PositionDm.X - roofOverhang,
                    floorVoxelY + wallHeightMetres * DecimetresPerMetre,
                    plot.PositionDm.Y - roofOverhang);
                _cutawayMaxVoxel = new Vector3(
                    plot.PositionDm.X + envelope.X + roofOverhang,
                    floorVoxelY + envelope.Y,
                    plot.PositionDm.Y + envelope.Z + roofOverhang);
                _cutawayBoundsReady = true;
                minVoxel = _cutawayMinVoxel;
                maxVoxel = _cutawayMaxVoxel;
                return true;
            }

            minVoxel = default;
            maxVoxel = default;
            return false;
        }

        private void RestoreGameplayCameraPresentation()
        {
            if (_camera != null) _camera.nearClipPlane = _gameplayNearClip;
            RenderingComposition.SetCutaway(false, Vector3.zero, Vector3.zero);
            OpeningOverheadActive = false;
            RoofCutawayActive = false;
            _hasStageApproach = false;
            _presentation = null;
            _lastCueProperty = null;
        }

        private string ReadLastPresentationCue()
        {
            if (_presentation == null)
            {
                _presentation = _presentationField.GetValue(_slice);
                if (_presentation == null) return string.Empty;
                _lastCueProperty = _presentation.GetType().GetProperty(
                    "LastCue",
                    BindingFlags.Instance | BindingFlags.Public);
            }

            return _lastCueProperty?.GetValue(_presentation) as string ?? string.Empty;
        }

        private void StepFade(float dt)
        {
            switch (_phase)
            {
                case TransitionPhase.FadingIntoCutscene:
                    _fadeAlpha = MoveAlpha(_fadeAlpha, 0f, dt, m_FadeInSeconds);
                    if (_fadeAlpha <= 0.001f)
                    {
                        _fadeAlpha = 0f;
                        _phase = TransitionPhase.CutsceneVisible;
                    }
                    break;

                case TransitionPhase.FadingOutCutscene:
                    _fadeAlpha = MoveAlpha(_fadeAlpha, 1f, dt, m_FadeOutSeconds);
                    if (_fadeAlpha >= 0.999f)
                    {
                        _fadeAlpha = 1f;
                        _phase = TransitionPhase.HoldingBlackForHandoff;
                    }
                    break;

                case TransitionPhase.FadingIntoGameplay:
                    _fadeAlpha = MoveAlpha(_fadeAlpha, 0f, dt, m_GameplayFadeInSeconds);
                    if (_fadeAlpha <= 0.001f)
                    {
                        _fadeAlpha = 0f;
                        _phase = TransitionPhase.Complete;
                    }
                    break;
            }
        }

        private static float MoveAlpha(float current, float target, float dt, float seconds)
        {
            if (seconds <= 0f) return target;
            return Mathf.MoveTowards(current, target, Mathf.Max(0f, dt) / seconds);
        }

        private void OnGUI()
        {
            if (_slice == null || !_slice.OpeningCutsceneStarted || _fadeAlpha <= 0f) return;
            if (_slice.AutoSurvey || _slice.AutoRecede || _slice.AutoWalk) return;

            int previousDepth = GUI.depth;
            Color previous = GUI.color;
            GUI.depth = -1000;
            GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(_fadeAlpha));
            GUI.DrawTexture(
                new Rect(0f, 0f, Screen.width, Screen.height),
                Texture2D.whiteTexture,
                ScaleMode.StretchToFill);
            GUI.color = previous;
            GUI.depth = previousDepth;
        }
    }
}
