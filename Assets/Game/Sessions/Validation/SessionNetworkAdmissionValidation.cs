using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Game.Sessions.Validation
{
    /// <summary>Non-visual admission-boundary proof; the GUI is instrumentation, not game art.</summary>
    public sealed class SessionNetworkAdmissionValidation : MonoBehaviour
    {
        private SessionNetworkAdmissionProbe _probe;
        private Stopwatch _clock;
        private string _failure;

        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying) return;
            _failure = null;
            try
            {
                if (GetComponent<Camera>() == null) gameObject.AddComponent<Camera>();
                _probe = new SessionNetworkAdmissionProbe();
                _probe.Milestone += Report;
                _clock = Stopwatch.StartNew();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private void Update()
        {
            if (_probe == null || _probe.Complete || _failure != null) return;
            try
            {
                _probe.Step();
                if (!_probe.Complete && _clock.ElapsedMilliseconds > 5000)
                    throw new InvalidOperationException("Monotonic admission deadline expired in " + _probe.PhaseDescription);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Report(string line) => Debug.Log(line);

        private void Fail(Exception exception)
        {
            _failure = exception.Message;
            Debug.LogError("SESSION_NETWORK_ADMISSION failure: " + exception);
            DisposeProbe();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, 1120, 200), GUI.skin.box);
            GUILayout.Label("SESSIONS / NET — ADMISSION BOUNDARY VALIDATION");
            GUILayout.Label(_failure ?? _probe?.PhaseDescription ?? "Starting");
            GUILayout.Label("Real PartySession, canonical Net authority and two UTP clients.");
            GUILayout.Label("Rejection and retry preserve live identity; replacement follows real disconnect cleanup.");
            GUILayout.Label("Focused boundary evidence, not provider or separate-process gameplay acceptance.");
            GUILayout.EndArea();
        }

        private void OnDisable() => DisposeProbe();

        private void DisposeProbe()
        {
            _clock?.Stop();
            SessionNetworkAdmissionProbe probe = _probe;
            _probe = null;
            if (probe == null) return;
            probe.Milestone -= Report;
            probe.Dispose();
        }
    }
}
