using System;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Voxel;
using UnityEngine;
using VoxelEngine.Showcase;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Optional built-player evidence driver for Kentridge SceneIssues that explicitly request a
    /// semantic landmark approach. It is dormant in ordinary gameplay and in unrelated captures.
    /// When enabled through issue metadata, it resolves the production public entrance, stages a
    /// bounded distance down that route, walks the real Game-owned character motor toward the
    /// landmark, then holds the normal player camera at eye height for durable screenshot evidence.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    internal sealed class KentridgeLandmarkEvidenceHarness : MonoBehaviour
    {
        private const float HoldDistanceBeyondExteriorMetres = 6f;
        private const float StartDistanceBeyondHoldMetres = 5f;
        private const float ArrivalToleranceMetres = 0.3f;
        private const float ArchFocusHeightMetres = 2.8f;

        private static readonly FieldInfo WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlanField = typeof(KentridgePlayableSlice).GetField(
            "_kentridgePlan", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo MouseLookField = typeof(KentridgePlayableSlice).GetField(
            "_mouseLook", BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private int _roleId;
        private ShowcaseWorld _world;
        private KentridgeCharacterHost _motor;
        private Vector3 _entrance;
        private Vector3 _hold;
        private bool _initialized;
        private bool _arrivalLogged;

        [Serializable]
        private sealed class EvidenceIssueConfig
        {
            public bool evidenceLandmarkApproach;
            public int evidenceLandmarkRole = -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!TryReadIssueConfig(out EvidenceIssueConfig config)
                || !config.evidenceLandmarkApproach
                || config.evidenceLandmarkRole < 0)
                return;

            KentridgePlayableSlice slice = UnityEngine.Object.FindFirstObjectByType<KentridgePlayableSlice>(
                FindObjectsInactive.Include);
            if (slice == null) return;

            if (WorldField == null || MotorField == null || PlanField == null || MouseLookField == null)
            {
                Debug.LogError("ARCH_EVIDENCE could not bind Kentridge playable runtime state");
                return;
            }

            var root = new GameObject("Kentridge Landmark Evidence Harness")
            {
                hideFlags = HideFlags.DontSave
            };
            var harness = root.AddComponent<KentridgeLandmarkEvidenceHarness>();
            harness._slice = slice;
            harness._roleId = config.evidenceLandmarkRole;
            UnityEngine.Object.DontDestroyOnLoad(root);
            Debug.Log($"ARCH_EVIDENCE armed role={harness._roleId}");
        }

        private void LateUpdate()
        {
            if (_slice == null)
            {
                Destroy(gameObject);
                return;
            }

            // SceneIssue automation owns this capture once armed. Generic rotating autowalk/survey
            // must not compete with the deterministic production-entrance approach below.
            _slice.AutoWalk = false;
            _slice.AutoSurvey = false;
            _slice.AutoRecede = false;
            MouseLookField.SetValue(_slice, false);

            if (!_slice.GameplayControlEnabled) return;
            if (!_initialized && !TryInitializeApproach()) return;

            float remaining = HorizontalDistance(_motor.Position, _hold);
            if (remaining > ArrivalToleranceMetres)
            {
                Vector3 delta = _hold - _motor.Position;
                delta.y = 0f;
                Vector3 wish = delta.sqrMagnitude <= 1e-6f ? Vector3.zero : delta.normalized;
                _motor.Step(_world, wish, sprint: false, jumpHeld: false, Time.deltaTime);
            }
            else
            {
                _motor.Velocity = Vector3.zero;
                if (!_arrivalLogged)
                {
                    _arrivalLogged = true;
                    Debug.Log($"ARCH_EVIDENCE arrived role={_roleId} distanceToEntrance="
                            + $"{HorizontalDistance(_motor.Position, _entrance):0.00}m");
                }
            }

            ApplyPlayerHeightArchView();
        }

        private bool TryInitializeApproach()
        {
            _world = WorldField.GetValue(_slice) as ShowcaseWorld;
            _motor = MotorField.GetValue(_slice) as KentridgeCharacterHost;
            SettlementPlan plan = PlanField.GetValue(_slice) as SettlementPlan;
            if (_world == null || _motor == null || plan == null) return false;

            if (!KentridgeGameplaySiteAccessResolver.TryResolve(plan, _roleId, 1, out KentridgeGameplaySiteAccess access))
            {
                Debug.LogError($"ARCH_EVIDENCE could not resolve public access for role={_roleId}");
                enabled = false;
                return false;
            }

            _entrance = ToMetres(access.Entrance);
            Vector3 exterior = ToMetres(access.ExteriorApproach);
            Vector3 outward = exterior - _entrance;
            outward.y = 0f;
            if (outward.sqrMagnitude <= 1e-6f)
            {
                Debug.LogError($"ARCH_EVIDENCE role={_roleId} has no exterior approach direction");
                enabled = false;
                return false;
            }
            outward.Normalize();

            _hold = exterior + outward * HoldDistanceBeyondExteriorMetres;
            Vector3 start = _hold + outward * StartDistanceBeyondHoldMetres;

            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(_entrance));
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(_hold));
            _world.GenerateRegionBlocking(ShowcaseWorld.RegionAt(start));
            _motor.SnapToGround(_world, start);
            _motor.Velocity = Vector3.zero;
            _initialized = true;

            Debug.Log($"ARCH_EVIDENCE approach role={_roleId} start={start} hold={_hold} entrance={_entrance}");
            ApplyPlayerHeightArchView();
            return true;
        }

        private void ApplyPlayerHeightArchView()
        {
            Vector3 camera = _motor.EyePosition;
            Vector3 focus = _entrance + Vector3.up * ArchFocusHeightMetres;
            Vector3 look = focus - camera;
            if (look.sqrMagnitude <= 1e-6f) look = Vector3.forward;
            _slice.transform.position = camera;
            _slice.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
        }

        private static bool TryReadIssueConfig(out EvidenceIssueConfig config)
        {
            config = null;
            string[] args = Environment.GetCommandLineArgs();
            string issuePath = null;
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], "-voxelIssue", StringComparison.Ordinal)) continue;
                issuePath = args[i + 1];
                break;
            }

            if (string.IsNullOrEmpty(issuePath) || !File.Exists(issuePath)) return false;
            try
            {
                config = JsonUtility.FromJson<EvidenceIssueConfig>(File.ReadAllText(issuePath));
                return config != null;
            }
            catch (Exception ex)
            {
                Debug.LogError("ARCH_EVIDENCE could not read SceneIssue metadata: " + ex.Message);
                return false;
            }
        }

        private static Vector3 ToMetres(RealizedWorldPoint point)
        {
            float scale = 0.1f / point.UnitsPerDecimetre;
            return new Vector3(
                point.Position.X * scale,
                point.Position.Y * scale,
                point.Position.Z * scale);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
