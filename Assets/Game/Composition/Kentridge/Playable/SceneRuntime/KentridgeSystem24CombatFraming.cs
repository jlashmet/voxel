using System;
using Game.Composition.Kentridge.Playable;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// System24-only visual evidence steering. It never writes camera or actor transforms: a dedicated
    /// virtual gamepad supplies ordinary production right-stick input so canonical combat evidence
    /// faces a real spawned encounter combatant.
    /// </summary>
    [DefaultExecutionOrder(-4900)]
    internal sealed class KentridgeSystem24CombatFraming : MonoBehaviour
    {
        private KentridgePlayableSlice _slice;
        private KentridgeForestBanditEncounter _forest;
        private Gamepad _lookGamepad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallWhenRequested()
        {
            if (!KentridgeSystem24VerticalSliceDriver.IsRequested) return;
            KentridgeForestBanditEncounter forest =
                UnityEngine.Object.FindFirstObjectByType<KentridgeForestBanditEncounter>();
            if (forest == null || forest.GetComponent<KentridgeSystem24CombatFraming>() != null) return;
            forest.gameObject.AddComponent<KentridgeSystem24CombatFraming>();
        }

        private void Start()
        {
            if (!KentridgeSystem24VerticalSliceDriver.IsRequested)
            {
                enabled = false;
                return;
            }

            _slice = GetComponent<KentridgePlayableSlice>()
                ?? throw new InvalidOperationException("System24 combat framing requires KentridgePlayableSlice.");
            _forest = GetComponent<KentridgeForestBanditEncounter>()
                ?? throw new InvalidOperationException("System24 combat framing requires the production forest encounter.");
            _lookGamepad = InputSystem.AddDevice<Gamepad>();
        }

        private void Update()
        {
            if (_lookGamepad == null || !_lookGamepad.added || _slice == null || _forest == null) return;
            if (_forest.CombatResolved)
            {
                QueueLook(0f);
                return;
            }

            Vector3 player = _slice.CharacterHost == null
                ? _slice.transform.position
                : _slice.CharacterHost.Position;
            Vector3 toAmbush = _forest.AmbushCenterWorld - player;
            toAmbush.y = 0f;
            float framingRadius = Mathf.Max(20f, _forest.TriggerRadiusMetres * 2.25f);
            if (!_forest.CombatActive && toAmbush.sqrMagnitude > framingRadius * framingRadius)
            {
                QueueLook(0f);
                return;
            }

            Transform target = ClosestBandit(player);
            if (target == null)
            {
                QueueLook(0f);
                return;
            }

            Vector3 forward = Vector3.ProjectOnPlane(_slice.transform.forward, Vector3.up).normalized;
            Vector3 desired = target.position - _slice.transform.position;
            desired.y = 0f;
            if (forward.sqrMagnitude < 0.5f || desired.sqrMagnitude < 0.01f)
            {
                QueueLook(0f);
                return;
            }

            desired.Normalize();
            float angle = Vector3.SignedAngle(forward, desired, Vector3.up);
            float lookX = Mathf.Abs(angle) <= 1.5f ? 0f : Mathf.Clamp(angle / 24f, -1f, 1f);
            QueueLook(lookX);
        }

        private Transform ClosestBandit(Vector3 player)
        {
            Transform closest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < _forest.Bandits.Count; i++)
            {
                GameObject bandit = _forest.Bandits[i];
                if (bandit == null || !bandit.activeInHierarchy) continue;
                Vector3 delta = bandit.transform.position - player;
                delta.y = 0f;
                float distance = delta.sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                closest = bandit.transform;
            }
            return closest;
        }

        private void QueueLook(float x)
        {
            if (_lookGamepad == null || !_lookGamepad.added) return;
            InputSystem.QueueStateEvent(
                _lookGamepad,
                new GamepadState { rightStick = new Vector2(x, 0f) });
        }

        private void OnDisable()
        {
            RemoveDevice();
        }

        private void OnDestroy()
        {
            RemoveDevice();
        }

        private void RemoveDevice()
        {
            if (_lookGamepad != null && _lookGamepad.added)
                InputSystem.RemoveDevice(_lookGamepad);
            _lookGamepad = null;
        }
    }
}
