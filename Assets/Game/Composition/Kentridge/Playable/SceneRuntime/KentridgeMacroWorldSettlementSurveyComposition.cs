using System;
using System.IO;
using UnityEngine;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace Game.Kentridge.PlayableSlice
{
    /// <summary>
    /// Validation-only composition for readable macro settlement evidence. The evidence driver
    /// deliberately keeps streaming demand at each semantic settlement focus; this component only
    /// lowers the already-selected survey camera from the coarse source-step-2 view into the exact
    /// near ring before the frame is rendered. Production world generation, residency, renderer
    /// policy, budgets, and normal gameplay cameras are unchanged.
    /// </summary>
    internal sealed class KentridgeMacroWorldSettlementSurveyComposition : MonoBehaviour
    {
        private const string ValidationProfile = "kentridge-macro-world";
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;
        private const float DriverSettlementSurveyHeightMetres = 70f;
        private const float ReadableSettlementSurveyHeightMetres = 45f;
        private const float HeightToleranceMetres = 1.5f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallForAssignedProfile()
        {
            if (!TryReadValidationProfile(out string profile)
                || !string.Equals(profile, ValidationProfile, StringComparison.Ordinal))
                return;

            var host = new GameObject("Kentridge Macro Settlement Survey Composition");
            host.hideFlags = HideFlags.DontSave;
            host.AddComponent<KentridgeMacroWorldSettlementSurveyComposition>();
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera == null) return;

            Vector3 position = camera.transform.position;
            int xDm = Mathf.RoundToInt(position.x / DmToMetres);
            int zDm = Mathf.RoundToInt(position.z / DmToMetres);
            float terrainMetres = TerrainSampler.HeightAt(xDm, zDm, Seed) * DmToMetres;
            float configuredHeight = position.y - terrainMetres;
            if (Mathf.Abs(configuredHeight - DriverSettlementSurveyHeightMetres) > HeightToleranceMetres)
                return;

            position.y = terrainMetres + ReadableSettlementSurveyHeightMetres;
            camera.transform.position = position;
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
    }
}
