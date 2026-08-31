using System;
using System.IO;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Replays an issue-owned waypoint route through the ordinary VoxelShowcase movement path.
    ///
    /// The harness supplies semantic automated headings and walk-speed configuration to
    /// VoxelShowcase. VoxelShowcase still calls CharacterMotor.Step, collision still reads
    /// production voxels, and streaming still follows the player transform; no waypoint teleports
    /// the player and no evidence code reaches into private movement state.
    /// Route files are optional evidence fixtures referenced by SceneIssue metadata, so normal game
    /// launches and captured-pose replays are unchanged.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    internal sealed class ShowcaseWaypointReplayHarness : MonoBehaviour
    {
        private const string SceneIssueArgument = "-voxel-scene-issue";
        private const string ScreenshotDirectoryArgument = "-voxel-screenshot-dir";

        private VoxelShowcase _showcase;
        private RouteSpec _route;
        private string _screenshotDirectory;
        private int _index;
        private float _elapsed;
        private float _holdElapsed;
        private bool _holding;
        private bool _captured;
        private float _completeElapsed;
        private bool _complete;
        private float _ordinaryWalkSpeed;
        private bool _replaySprintApplied;
        private bool _hasVerticalAnchor;
        private float _verticalAnchorY;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            string issuePath = Argument(SceneIssueArgument);
            if (string.IsNullOrEmpty(issuePath) || !File.Exists(issuePath)) return;

            try
            {
                IssueRouteReference issue = JsonUtility.FromJson<IssueRouteReference>(File.ReadAllText(issuePath));
                if (issue == null || string.IsNullOrWhiteSpace(issue.evidenceRoute)) return;

                string issueDirectory = Path.GetDirectoryName(issuePath) ?? string.Empty;
                string routePath = Path.GetFullPath(Path.Combine(issueDirectory, issue.evidenceRoute));
                string expectedRoot = Path.GetFullPath(issueDirectory) + Path.DirectorySeparatorChar;
                if (!routePath.StartsWith(expectedRoot, StringComparison.Ordinal) || !File.Exists(routePath))
                    throw new InvalidOperationException("SceneIssue evidenceRoute must resolve inside its assignment folder.");

                RouteSpec route = JsonUtility.FromJson<RouteSpec>(File.ReadAllText(routePath));
                Validate(route);

                VoxelShowcase showcase = UnityEngine.Object.FindFirstObjectByType<VoxelShowcase>(
                    FindObjectsInactive.Include);
                if (showcase == null)
                    throw new InvalidOperationException("Waypoint replay requires VoxelShowcase in the built scene.");

                string screenshotDirectory = Argument(ScreenshotDirectoryArgument);
                if (string.IsNullOrEmpty(screenshotDirectory))
                    throw new InvalidOperationException("Waypoint replay requires -voxel-screenshot-dir.");
                Directory.CreateDirectory(screenshotDirectory);

                var root = new GameObject("Showcase Waypoint Replay Harness")
                {
                    hideFlags = HideFlags.DontSave
                };
                var replay = root.AddComponent<ShowcaseWaypointReplayHarness>();
                replay._showcase = showcase;
                replay._ordinaryWalkSpeed = showcase.PlayerWalkSpeedMetresPerSecond;
                replay._route = route;
                replay._screenshotDirectory = screenshotDirectory;
                UnityEngine.Object.DontDestroyOnLoad(root);
                Debug.Log($"WAYPOINT_REPLAY armed route={Path.GetFileName(routePath)} waypoints={route.waypoints.Length}");
            }
            catch (Exception error)
            {
                Debug.LogError($"WAYPOINT_REPLAY setup failed: {error}");
                Application.Quit(21);
            }
        }

        private void Update()
        {
            if (_showcase == null || _route == null) return;

            _elapsed += Time.unscaledDeltaTime;
            if (!_complete && _elapsed >= _route.timeoutSeconds)
            {
                StopWalking();
                Debug.LogError($"WAYPOINT_REPLAY timeout at waypoint {_index}/{_route.waypoints.Length} after {_elapsed:0.0}s");
                Application.Quit(23);
                enabled = false;
                return;
            }

            if (_complete)
            {
                StopWalking();
                _completeElapsed += Time.unscaledDeltaTime;
                if (_route.quitOnComplete && _completeElapsed >= 1f)
                {
                    Debug.Log("WAYPOINT_REPLAY quitting after successful capture flush.");
                    Application.Quit(0);
                    enabled = false;
                }
                return;
            }

            Waypoint waypoint = _route.waypoints[_index];
            Vector3 player = _showcase.transform.position;
            Vector2 delta = new Vector2(waypoint.x - player.x, waypoint.z - player.z);
            float arrivalRadius = waypoint.arrivalRadius > 0f
                ? waypoint.arrivalRadius
                : _route.arrivalRadius;
            bool traversalStateMatches = ShowcaseWaypointTraversalContract.Matches(
                _showcase.PlayerFeetPosition.y,
                _showcase.PlayerGrounded,
                waypoint.requireGrounded,
                _hasVerticalAnchor,
                _verticalAnchorY,
                waypoint.expectedYOffset,
                waypoint.yTolerance);

            if (!_holding
                && delta.sqrMagnitude <= arrivalRadius * arrivalRadius
                && traversalStateMatches)
            {
                if (waypoint.anchorVertical)
                {
                    _verticalAnchorY = _showcase.PlayerFeetPosition.y;
                    _hasVerticalAnchor = true;
                    Debug.Log($"WAYPOINT_REPLAY vertical anchor '{waypoint.name}' feetY={_verticalAnchorY:0.00}");
                }

                _holding = true;
                _holdElapsed = 0f;
                _captured = false;
                StopWalking();
                Debug.Log(
                    $"WAYPOINT_REPLAY reached {_index + 1}/{_route.waypoints.Length} '{waypoint.name}' "
                    + $"at {player} feetY={_showcase.PlayerFeetPosition.y:0.00} grounded={_showcase.PlayerGrounded}");
            }

            if (_holding)
            {
                StopWalking();
                ApplyLook(waypoint);
                _holdElapsed += Time.unscaledDeltaTime;
                float holdSeconds = waypoint.holdSeconds >= 0f
                    ? waypoint.holdSeconds
                    : _route.holdSeconds;
                if (_holdElapsed < holdSeconds) return;

                if (!_captured && !string.IsNullOrWhiteSpace(waypoint.capture))
                {
                    string fileName = Path.GetFileName(waypoint.capture);
                    if (!string.Equals(fileName, waypoint.capture, StringComparison.Ordinal))
                    {
                        Debug.LogError($"WAYPOINT_REPLAY invalid capture name '{waypoint.capture}'.");
                        Application.Quit(24);
                        enabled = false;
                        return;
                    }

                    string path = Path.Combine(_screenshotDirectory, fileName);
                    ScreenCapture.CaptureScreenshot(path);
                    _captured = true;
                    Debug.Log($"WAYPOINT_REPLAY capture '{fileName}'");
                }

                _index++;
                _holding = false;
                if (_index >= _route.waypoints.Length)
                {
                    _complete = true;
                    StopWalking();
                    Debug.Log($"WAYPOINT_REPLAY COMPLETE waypoints={_route.waypoints.Length} elapsed={_elapsed:0.0}s");
                }
                return;
            }

            float desiredYaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;
            _showcase.SetAutomatedHeading(desiredYaw);
            ApplyReplaySprint();
            _showcase.AutoWalk = true;
        }

        private void ApplyReplaySprint()
        {
            if (_replaySprintApplied || _showcase == null) return;
            // Evidence replay needs to fit the SceneIssue workflow's 60-second ceiling. Use the
            // production motor's own sprint multiplier, then restore ordinary walk speed whenever
            // the route pauses or exits. Collision, step-up and gravity all remain CharacterMotor.Step.
            _showcase.PlayerWalkSpeedMetresPerSecond =
                _ordinaryWalkSpeed * _showcase.PlayerSprintMultiplier;
            _replaySprintApplied = true;
        }

        private void StopWalking()
        {
            if (_showcase != null) _showcase.AutoWalk = false;
            if (!_replaySprintApplied || _showcase == null) return;
            _showcase.PlayerWalkSpeedMetresPerSecond = _ordinaryWalkSpeed;
            _replaySprintApplied = false;
        }

        private void OnDisable()
        {
            StopWalking();
            _showcase?.ClearAutomatedHeading();
        }

        private void ApplyLook(Waypoint waypoint)
        {
            if (!waypoint.lookAt) return;
            Vector3 player = _showcase.transform.position;
            float dx = waypoint.lookX - player.x;
            float dz = waypoint.lookZ - player.z;
            float yaw = Mathf.Atan2(dx, dz) * Mathf.Rad2Deg;
            _showcase.SetAutomatedHeading(yaw, waypoint.lookPitchDegrees);
        }

        private static void Validate(RouteSpec route)
        {
            if (route == null || route.waypoints == null || route.waypoints.Length == 0)
                throw new InvalidOperationException("Waypoint route must contain at least one waypoint.");
            if (route.timeoutSeconds <= 0f || route.arrivalRadius <= 0f)
                throw new InvalidOperationException("Waypoint route timeoutSeconds and arrivalRadius must be positive.");

            bool hasVerticalAnchor = false;
            for (int i = 0; i < route.waypoints.Length; i++)
            {
                Waypoint waypoint = route.waypoints[i];
                if (waypoint == null || string.IsNullOrWhiteSpace(waypoint.name))
                    throw new InvalidOperationException($"Waypoint {i} has no name.");
                if (waypoint.arrivalRadius == 0f)
                    waypoint.arrivalRadius = -1f;
                if (waypoint.holdSeconds == 0f)
                    waypoint.holdSeconds = -1f;
                if (waypoint.yTolerance < -1f)
                    throw new InvalidOperationException($"Waypoint '{waypoint.name}' has invalid yTolerance.");
                if (waypoint.yTolerance >= 0f && !hasVerticalAnchor)
                    throw new InvalidOperationException(
                        $"Waypoint '{waypoint.name}' requires a vertical anchor earlier in the route.");
                if (waypoint.anchorVertical)
                    hasVerticalAnchor = true;
            }
        }

        private static string Argument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.Ordinal))
                    return args[i + 1];
            return null;
        }

        [Serializable]
        private sealed class IssueRouteReference
        {
            public string evidenceRoute;
        }

        [Serializable]
        private sealed class RouteSpec
        {
            public float timeoutSeconds = 90f;
            public float arrivalRadius = 1.25f;
            public float holdSeconds = 0.75f;
            public bool quitOnComplete = true;
            public Waypoint[] waypoints;
        }

        [Serializable]
        private sealed class Waypoint
        {
            public string name;
            public float x;
            public float z;
            public float arrivalRadius = -1f;
            public float holdSeconds = -1f;
            public string capture;
            public bool lookAt;
            public float lookX;
            public float lookZ;
            public float lookPitchDegrees;
            public bool requireGrounded;
            public bool anchorVertical;
            public float expectedYOffset;
            public float yTolerance = -1f;
        }
    }
}
