using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace VoxelEngine.Rendering.Runtime
{
    /// <summary>
    /// Temporary standalone-player diagnostic for Unity's indexed-indirect command contract on Metal.
    /// In the Editor it is inert; the ArchLookdev standalone built by the focused test becomes the probe.
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

            // Use the scene camera that the existing real-player harness has already proven reaches
            // the presented framebuffer. A separate runtime-created URP camera made the first probe
            // non-discriminating because it was not the presented camera.
            _camera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (_camera == null)
            {
                Debug.LogError("INDIRECT_PROBE no presented camera found");
                enabled = false;
                return;
            }

            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // Four triangles laid out left-to-right in clip space. The top row executes all four
            // commands in one RenderPrimitivesIndexedIndirect call. The bottom row executes the
            // exact same command records individually via commandCount=1/startCommand=N.
            var positions = new[]
            {
                new Vector3(-0.72f, -0.22f, 0f), new Vector3(-0.42f, -0.22f, 0f), new Vector3(-0.57f,  0.22f, 0f),
                new Vector3(-0.22f, -0.22f, 0f), new Vector3( 0.08f, -0.22f, 0f), new Vector3(-0.07f,  0.22f, 0f),
                new Vector3( 0.28f, -0.22f, 0f), new Vector3( 0.58f, -0.22f, 0f), new Vector3( 0.43f,  0.22f, 0f),
                new Vector3( 0.68f, -0.22f, 0f), new Vector3( 0.98f, -0.22f, 0f), new Vector3( 0.83f,  0.22f, 0f),
            };
            _positions = new GraphicsBuffer(GraphicsBuffer.Target.Structured, positions.Length, sizeof(float) * 3);
            _positions.SetData(positions);

            // Segment 0 addresses vertices 0..2. Segment 1 contains absolute 3..5. Segment 2
            // returns to local 0..2 so the final command can combine non-zero start and base.
            uint[] indices = { 0u, 1u, 2u, 3u, 4u, 5u, 0u, 1u, 2u };
            _indices = new GraphicsBuffer(GraphicsBuffer.Target.Index, indices.Length, sizeof(uint));
            _indices.SetData(indices);

            var commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[CommandCount];
            commandData[0] = Command(0u, 0u); // control
            commandData[1] = Command(3u, 0u); // startIndex only
            commandData[2] = Command(0u, 6u); // baseVertexIndex only
            commandData[3] = Command(6u, 9u); // both non-zero
            _commands = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments,
                CommandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            _commands.SetData(commandData);

            _multiProps = new MaterialPropertyBlock();
            _multiProps.SetBuffer("_Positions", _positions);
            _multiProps.SetFloat("_YOffset", 0.48f);
            _singleProps = new MaterialPropertyBlock();
            _singleProps.SetBuffer("_Positions", _positions);
            _singleProps.SetFloat("_YOffset", -0.48f);

            Debug.Log("INDIRECT_PROBE ready device=" + SystemInfo.graphicsDeviceType
                + " camera=" + _camera.name
                + " top=commandCount4 bottom=individual startCommand0..3 "
                + "args=[(0,0),(3,0),(0,6),(6,9)] expected colors=red,green,blue,yellow");
        }

        private static GraphicsBuffer.IndirectDrawIndexedArgs Command(uint startIndex, uint baseVertex)
        {
            return new GraphicsBuffer.IndirectDrawIndexedArgs
            {
                indexCountPerInstance = 3u,
                instanceCount = 1u,
                startIndex = startIndex,
                baseVertexIndex = baseVertex,
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
