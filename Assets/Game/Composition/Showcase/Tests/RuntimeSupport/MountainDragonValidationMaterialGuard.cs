using System;
using UnityEngine;

namespace VoxelEngine.Showcase.Tests.RuntimeSupport
{
    /// <summary>
    /// Ensures the focused Mountain Dragon standalone validation scene uses an
    /// explicitly packaged URP-compatible marker shader instead of Unity's
    /// built-in primitive default material, which renders as the magenta error
    /// shader in the standalone URP player.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MountainDragonValidationSceneDriver))]
    public sealed class MountainDragonValidationMaterialGuard : MonoBehaviour
    {
        public const string ExpectedShaderName = "Hidden/VoxelEngine/MountainDragonValidationMarker";
        private const string ShaderResourceName = "MountainDragonValidationMarker";
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Material _markerMaterial;
        private bool _applied;
        private bool _shaderSupported;
        private int _rendererCount;
        private string _detail = "NOT APPLIED";

        public bool Applied => _applied;
        public bool ShaderSupported => _shaderSupported;
        public int RendererCount => _rendererCount;
        public string Detail => _detail;

        private void LateUpdate()
        {
            if (_applied) return;

            MountainDragonValidationSceneDriver driver = GetComponent<MountainDragonValidationSceneDriver>();
            if (driver == null || !driver.Complete) return;

            try
            {
                ApplyMarkerMaterial();
            }
            catch (Exception exception)
            {
                _detail = exception.GetType().Name + ": " + exception.Message;
                Debug.LogError("[MountainDragonValidation] MARKER MATERIAL FAIL: " + _detail);
                enabled = false;
            }
        }

        private void ApplyMarkerMaterial()
        {
            Shader shader = Resources.Load<Shader>(ShaderResourceName);
            if (shader == null)
                throw new InvalidOperationException($"Required validation shader resource '{ShaderResourceName}' was not packaged.");
            if (!string.Equals(shader.name, ExpectedShaderName, StringComparison.Ordinal))
                throw new InvalidOperationException($"Validation shader resource resolved as '{shader.name}', expected '{ExpectedShaderName}'.");
            if (!shader.isSupported)
                throw new InvalidOperationException($"Validation shader '{shader.name}' is unsupported by the active render pipeline/player.");

            _markerMaterial = new Material(shader)
            {
                name = "Mountain Dragon Validation Marker Material",
                hideFlags = HideFlags.DontSave
            };

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("Focused Mountain Dragon validation produced no marker renderers.");

            var propertyBlock = new MaterialPropertyBlock();
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                Color authoredColor = ReadAuthoredColor(renderer.sharedMaterial);
                renderer.sharedMaterial = _markerMaterial;
                renderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor(BaseColorId, authoredColor);
                renderer.SetPropertyBlock(propertyBlock);
                propertyBlock.Clear();
            }

            _rendererCount = renderers.Length;
            _shaderSupported = true;
            _applied = true;
            _detail = $"PASS: applied packaged shader '{shader.name}' to {_rendererCount} marker renderers";
            Debug.Log("[MountainDragonValidation] " + _detail);
        }

        private static Color ReadAuthoredColor(Material material)
        {
            if (material == null) return Color.white;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private void OnDestroy()
        {
            if (_markerMaterial != null)
                Destroy(_markerMaterial);
        }
    }
}
