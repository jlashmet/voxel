using System;
using UnityEngine;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Semantic-free application configuration for opaque texture-array extension slots.
    /// Applications may place one asset named VoxelAdditionalTextureLayers in any Resources
    /// directory; renderer code owns only ordered texture slots, never their game meaning.
    /// </summary>
    [CreateAssetMenu(fileName = ResourceName, menuName = "Voxel Engine/Additional Texture Layers")]
    public sealed class VoxelAdditionalTextureLayersAsset : ScriptableObject
    {
        public const string ResourceName = "VoxelAdditionalTextureLayers";

        [SerializeField] private Texture2D[] m_Albedo = Array.Empty<Texture2D>();
        [SerializeField] private Texture2D[] m_Normals = Array.Empty<Texture2D>();

        public Texture2D[] Albedo => m_Albedo;
        public Texture2D[] Normals => m_Normals;
    }
}
