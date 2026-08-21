using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Temporary standalone-player diagnostic for Unity's indexed-indirect command contract on Metal.
    /// This version proves the workaround representation: arena-global stored indices with baseVertex=0.
    /// </summary>
    internal sealed class IndexedIndirectContractProbe : MonoBehaviour
    {
        private const string ProbeArgument = "-voxel-indexed-indirect-contract";
        private const int CommandCount = 4;

        private Camera _camera;
        private Material _material;
        private GraphicsBuffer _positions;
        private GraphicsBuffer _indices;
        private GraphicsBuffer _commands;
        private MaterialPropertyBlock _multiProps;
        private MaterialPropertyBlock _singleProps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            bool explicitProbe = false;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == ProbeArgument)
                {
                    explicitProbe = true;
                    break;
                }
            }

            bool focusedStandalone = !Application.isEditor
                && SceneManager.GetActiveScene().name == "ArchLookdev";
            if (!explicitProbe && !focusedStandalone) return;

            var host = new GameObject("IndexedIndirectContractProbe");
            DontDestroyOnLoad(host);
            host.AddComponent<IndexedIndirectContractProbe>();
        }

        private void Awake()
        {
            Shader shader = Resources.Load<Shader>("IndexedIndirectContract");
            if (shader == null || !shader.isSupported)
            {
                Debug.LogError($"INDIRECT_PROBE shader unavailable supported={shader != null && shader.isSupported}");
                enabled = false;
                return;
            }

            _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (_camera == null)
            {
                Debug.LogError("INDIRECT_PROBE no presented camera found");
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // Four distinct triangles laid out left-to-right in clip space. The top row submits
            // all four commands in one API call; the bottom row submits the same records one at a
            // time. Every stored index is already global to the shared position buffer and every
            // indirect command deliberately uses baseVertexIndex=0.
            var positions = new[]
            {
                new Vector3(-0.72f, -0.22f, 0f), new Vector3(-0.42f, -0.22f, 0f), new Vector3(-0.57f,  0.22f, 0f),
                new Vector3(-0.22f, -0.22f, 0f), new Vector3( 0.08f, -0.22f, 0f), new Vector3(-0.07f,  0.22f, 0f),
                new Vector3( 0.28f, -0.22f, 0f), new Vector3( 0.58f, -0.22f, 0f), new Vector3( 0.43f,  0.22f, 0f),
                new Vector3( 0.68f, -0.22f, 0f), new Vector3( 0.98f, -0.22f, 0f), new Vector3( 0.83f,  0.22f, 0f),
            };
            _positions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, positions.Length, sizeof(float) * 3);
            _positions.SetData(positions);

            uint[] indices =
            {
                0u, 1u, 2u,
                3u, 4u, 5u,
                6u, 7u, 8u,
                9u, 10u, 11u,
            };
            _indices = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Length, sizeof(uint));
            _indices.SetData(indices);

            var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[CommandCount];
            commandData[0] = Command(0u);
            commandData[1] = Command(3u);
            commandData[2] = Command(6u);
            commandData[3] = Command(9u);
            _commands = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                CommandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commands.SetData(commandData);

            _multiProps = new MaterialPropertyBlock();
            _multiProps.SetBuffer("_Positions", _positions);
            _multiProps.SetFloat("_YOffset", 0.48f);
            _singleProps = new MaterialPropertyBlock();
            _singleProps.SetBuffer("_Positions", _positions);
            _singleProps.SetFloat("_YOffset", -0.48f);

            Debug.Log("INDIRECT_PROBE_GLOBAL ready device=" + SystemInfo.graphicsDeviceType
                + " camera=" + _camera.name
                + " top=commandCount4 bottom=individual startCommand0..3"
                + " startIndices=[0,3,6,9] baseVertices=[0,0,0,0]"
                + " storedIndices=global expected colors=red,green,blue,yellow");
        }

        private static GraphicsBuffer.IndirectDrawIndexedArgs Command(uint startIndex)
        {
            return new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = 3u,
                instanceCount = 1u,
                startIndex = startIndex,
                baseVertexIndex = 0u,
                startInstance = 0u,
            };
        }

        private void Update()
        {
            if (_camera == null || _material == null || _commands == null) return;

            var multi = new RenderParams(_material)
            {
                camera = _camera,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
                matProps = _multiProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderPrimitivesIndexedIndirect(multi, MeshTopology.Triangles,
                _indices, _commands, CommandCount, 0);

            var single = new RenderParams(_material)
            {
                camera = _camera,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 1000f),
                matProps = _singleProps,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            for (int command = 0; command < CommandCount; command++)
            {
                Graphics.RenderPrimitivesIndexedIndirect(single, MeshTopology.Triangles,
                    _indices, _commands, 1, command);
            }
        }

        private void OnDestroy()
        {
            _positions?.Dispose();
            _indices?.Dispose();
            _commands?.Dispose();
            if (_material != null) Destroy(_material);
        }
    }
}
