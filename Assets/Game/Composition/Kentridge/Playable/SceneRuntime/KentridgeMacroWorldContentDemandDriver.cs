using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Game.Composition.Kentridge.Playable;
using Game.WorldBuilder.Runtime;
using MountingForce.WorldGen;
using MountingForce.WorldGen.Content.Kentridge;
using UnityEngine;
using VoxelEngine.Showcase;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only demand orchestration for boundary-spanning settlement surveys. The normal
    /// evidence camera is deliberately elevated and fixed, but a settlement can span four region
    /// columns; keeping streaming demand in one camera column can leave the diagonal building
    /// column behind unrelated nearer work. While an acceptance settlement is waiting, this driver
    /// moves the real CharacterMotor demand point to the first unsettled authored building centre.
    /// The production ShowcaseWorld streaming queues, budgets, rasterization, collision, and
    /// rendering remain authoritative; no region is force-generated or granted extra budget.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    internal sealed class KentridgeMacroWorldContentDemandDriver : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const float MaximumSurveyOffsetMetres = 24f;
        private const float MinimumSurveyHeightMetres = 20f;
        private const float DemandHeightMetres = 55f;

        private static readonly FieldInfo s_WorldField = typeof(KentridgePlayableSlice).GetField(
            "_world", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo s_MotorField = typeof(KentridgePlayableSlice).GetField(
            "_motor", BindingFlags.Instance | BindingFlags.NonPublic);

        private KentridgePlayableSlice _slice;
        private ShowcaseWorld _world;
        private KentridgeCharacterHost _motor;
        private SurveySettlement[] _settlements;
        private string _lastDemand;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro World Content Demand");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldContentDemandDriver>();
        }

        private void Update()
        {
            _slice ??= FindFirstObjectByType<KentridgePlayableSlice>();
            if (_slice == null || !_slice.GameplayControlEnabled) return;
            if (s_WorldField == null || s_MotorField == null)
                throw new InvalidOperationException("Macro content-demand driver cannot resolve Kentridge runtime state.");

            _world ??= s_WorldField.GetValue(_slice) as ShowcaseWorld;
            _motor ??= s_MotorField.GetValue(_slice) as KentridgeCharacterHost;
            if (_world == null || _motor == null) return;
            _settlements ??= BuildAcceptanceSettlements();

            Vector3 current = _motor.EyePosition;
            int currentGround = TerrainSampler.HeightAt(
                Mathf.RoundToInt(current.x / DmToMetres),
                Mathf.RoundToInt(current.z / DmToMetres),
                Seed);
            if (current.y - currentGround * DmToMetres < MinimumSurveyHeightMetres) return;

            SurveySettlement settlement = FindSurveySettlement(current);
            if (settlement == null) return;

            for (var i = 0; i < settlement.Buildings.Length; i++)
            {
                Int2 point = settlement.Buildings[i];
                if (IsContentSettled(point)) continue;

                int ground = TerrainSampler.HeightAt(point.X, point.Y, Seed);
                _motor.Position = new Vector3(
                    point.X * DmToMetres,
                    ground * DmToMetres + DemandHeightMetres - _motor.EyeHeight,
                    point.Y * DmToMetres);
                _motor.Velocity = Vector3.zero;

                string demand = settlement.Id + ":" + i;
                if (!string.Equals(_lastDemand, demand, StringComparison.Ordinal))
                {
                    _lastDemand = demand;
                    Debug.Log(
                        $"MACROEVIDENCE streaming-demand target={settlement.Id} building={i} " +
                        $"centreDm=({point.X},{point.Y})");
                }
                return;
            }

            _lastDemand = null;
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

        private SurveySettlement FindSurveySettlement(Vector3 current)
        {
            SurveySettlement best = null;
            float bestDistanceSquared = MaximumSurveyOffsetMetres * MaximumSurveyOffsetMetres;
            for (var i = 0; i < _settlements.Length; i++)
            {
                SurveySettlement candidate = _settlements[i];
                float dx = current.x - candidate.Focus.X * DmToMetres;
                float dz = current.z - candidate.Focus.Y * DmToMetres;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared > bestDistanceSquared) continue;
                best = candidate;
                bestDistanceSquared = distanceSquared;
            }
            return best;
        }

        private static SurveySettlement[] BuildAcceptanceSettlements()
        {
            TopDownWorldLayout layout = MountingForceTopDownWorldDefinition.Build(Seed);
            TopDownWorldPhysicalPlan physical = TopDownWorldPhysicalPlanner.Plan(
                layout,
                KentridgeTopDownWorldPhysicalIntent.Build(),
                KentridgeDefinition.TownCentreDm,
                MountingForceTopDownWorldDefinition.CellSizeDm,
                voxelsPerDecimetre: 1);

            return new[]
            {
                BuildSettlement(physical, MountingForceTopDownWorldDefinition.Moordell),
                BuildSettlement(physical, MountingForceTopDownWorldDefinition.Rossdam),
                BuildSettlement(physical, MountingForceTopDownWorldDefinition.FairyVillage),
                BuildSettlement(physical, MountingForceTopDownWorldDefinition.OrcVillage),
            };
        }

        private static SurveySettlement BuildSettlement(TopDownWorldPhysicalPlan physical, string id)
        {
            if (!physical.TryGetSettlement(id, out TopDownWorldSettlementPlan settlement)
                || settlement.Buildings.Count < 4)
                throw new InvalidOperationException(
                    "Macro content-demand plan is missing acceptance settlement '" + id + "'.");

            var buildings = new Int2[settlement.Buildings.Count];
            long sumX = 0;
            long sumZ = 0;
            for (var i = 0; i < settlement.Buildings.Count; i++)
            {
                buildings[i] = settlement.Buildings[i].CentreDm;
                sumX += buildings[i].X;
                sumZ += buildings[i].Y;
            }

            return new SurveySettlement(
                id,
                new Int2((int)(sumX / buildings.Length), (int)(sumZ / buildings.Length)),
                buildings);
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

        private sealed class SurveySettlement
        {
            public string Id { get; }
            public Int2 Focus { get; }
            public Int2[] Buildings { get; }

            public SurveySettlement(string id, Int2 focus, Int2[] buildings)
            {
                Id = id;
                Focus = focus;
                Buildings = buildings;
            }
        }
    }
}
