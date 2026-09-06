using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace VoxelEngine.Net.Validation
{
    /// <summary>Focused non-visual transport validation; GUI output is instrumentation, not game art.</summary>
    public sealed class SessionAdmissionTransportValidation : MonoBehaviour
    {
        private SessionAdmissionTransportProbe _probe;
        private Stopwatch _clock;
        private string _failure;

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            _failure = null;
            try
            {
                // Capture setup only. The feature under test has no world/geometry presentation.
                if (GetComponent<Camera>() == null) gameObject.AddComponent<Camera>();
                _probe = new SessionAdmissionTransportProbe();
                _probe.Milestone += Report;
                _clock = Stopwatch.StartNew();
            }
            catch (Exception ex) { Fail(ex); }
        }

        private void Update()
        {
            if (_probe == null || _probe.Complete || _failure != null) return;
            try
            {
                _probe.Step();
                if (!_probe.Complete && _clock.ElapsedMilliseconds > 6000)
                    throw new InvalidOperationException("Monotonic transport deadline expired in " + _probe.PhaseDescription);
            }
            catch (Exception ex) { Fail(ex); }
        }

        private static void Report(string line) => Debug.Log(line);

        private void Fail(Exception ex)
        {
            _failure = ex.Message;
            Debug.LogError("SESSION_ADMISSION_TRANSPORT failure: " + ex);
            DisposeProbe();
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(24, 24, 960, 180), GUI.skin.box);
            GUILayout.Label("SESSION ADMISSION — PRODUCTION TRANSPORT VALIDATION");
            GUILayout.Label(_failure ?? _probe?.PhaseDescription ?? "Starting");
            GUILayout.Label("Two real Net clients, bounded EVENT packets, isolated replies and a replacement connection.");
            GUILayout.Label("Transport-only evidence. No membership, GameplayReady or full multiplayer acceptance is claimed.");
            GUILayout.EndArea();
        }

        private void OnDisable() => DisposeProbe();

        private void DisposeProbe()
        {
            _clock?.Stop();
            if (_probe == null) return;
            _probe.Milestone -= Report;
            _probe.Dispose();
            _probe = null;
        }
    }
}
