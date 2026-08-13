using System.Collections.Generic;
using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>Deterministic terrain-only look-development scene for the sunlit landscape.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed partial class TerrainLookdev : MonoBehaviour
    {
        private const int Seed = 0x51A17;
        private const float XMin = -22f;
        private const float XMax = 22f;
        private const float ZMin = -9f;
        private const float ZMax = 55f;

        private Transform _root;
        private readonly List<Material> _materials = new();
        private readonly List<Mesh> _meshes = new();
        private Mesh _roundedBlock;
        private Mesh _blob;
        private Mesh _flower;
        private Material _grass;
        private Material _grassDark;
        private Material _moss;
        private Material[] _stone;
        private Material _path;
        private Material _flowerWhite;
        private Material _flowerYellow;
        private Material _flowerPink;
        private Material _flowerBlue;

        public Camera SceneCamera => GetComponent<Camera>();

        private void OnEnable()
        {
            if (Application.isPlaying && _root == null) Rebuild();
        }

        [ContextMenu("Rebuild Terrain Lookdev")]
        public void Rebuild()
        {
            if (_root != null) return;
            ConfigureEnvironment();
            CreateSharedAssets();
            GameObject rootObject = new("Terrain Visual Root");
            _root = rootObject.transform;
            _root.SetParent(transform.parent, false);
            BuildGround();
            BuildPath();
            BuildRockFields();
            BuildMossCarpet();
            BuildWildflowers();
        }
    }
}
