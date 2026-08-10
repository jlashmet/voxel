using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace VoxelEngine.Rendering.SurfaceExtraction
{
    /// <summary>A GPU-authored Surface Nets vertex. One slot exists for every density cell.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct SmoothSurfaceVertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public uint Material;
        public uint Active;

        public const int Stride = 32;
    }

    /// <summary>
    /// Extracts a continuous indexed surface from a dense scalar lattice entirely on the GPU.
    ///
    /// This is the isolated kernel behind the sparse-brick renderer migration. Vertex placement
    /// follows Surface Nets (the robust, inexpensive member of the dual-contouring family): every
    /// sign-changing cell averages its edge intersections, then sign-changing lattice edges join
    /// four neighbouring cell vertices into quads. No output position is snapped to a voxel face.
    /// </summary>
    public sealed class GpuSurfaceExtractor : IDisposable
    {
        private ComputeBuffer _density;
        private ComputeBuffer _materials;
        private ComputeBuffer _vertices;
        private ComputeBuffer _indices;
        private ComputeBuffer _args;
        private ComputeBuffer _dummyInt;
        private ComputeBuffer _dummyUInt;
        private int _sampleCapacity;
        private int _cellCapacity;
        private int _indexCapacity;
        private readonly GpuSurfaceArena _arena;
        private readonly int _slot = -1;

        /// <summary>Standalone: this extractor owns its own buffers. Used by isolated tests.</summary>
        public GpuSurfaceExtractor() { }

        /// <summary>
        /// Arena-backed: geometry lives in a shared preallocated buffer at <paramref name="slot"/>.
        /// This is the resident path, where per-chunk allocation churn is the thing to avoid.
        /// </summary>
        public GpuSurfaceExtractor(GpuSurfaceArena arena, int slot)
        {
            _arena = arena ?? throw new ArgumentNullException(nameof(arena));
            if (slot < 0 || slot >= arena.SlotCount)
                throw new ArgumentOutOfRangeException(nameof(slot));
            _slot = slot;
            _indexCapacity = arena.IndicesPerChunk;
            _cellCapacity = arena.CellsPerChunk;
        }

        private bool ArenaBacked => _arena != null;

        public ComputeBuffer VertexBuffer => ArenaBacked ? _arena.Vertices : _vertices;
        public ComputeBuffer IndexBuffer => ArenaBacked ? _arena.Indices : _indices;
        public ComputeBuffer ArgsBuffer => ArenaBacked ? _arena.Args : _args;
        public int VertexBase => ArenaBacked ? _arena.VertexBase(_slot) : 0;
        public int IndexBase => ArenaBacked ? _arena.IndexBase(_slot) : 0;
        public int ArgsBase => ArenaBacked ? _arena.ArgsBase(_slot) : 0;
        public int ArgsByteOffset => ArenaBacked ? _arena.ArgsByteOffset(_slot) : 0;
        public int CellCount { get; private set; }
        public int MaxIndexCount => _indexCapacity;

        private static readonly int s_SurfaceVertices = Shader.PropertyToID("_SurfaceVertices");
        private static readonly int s_SurfaceIndices = Shader.PropertyToID("_SurfaceIndices");
        private static readonly int s_SurfaceDensity = Shader.PropertyToID("g_SurfaceDensity");
        private static readonly int s_SurfaceMaterials = Shader.PropertyToID("g_SurfaceMaterials");
        private static readonly int s_OutputVertices = Shader.PropertyToID("g_SurfaceVertices");
        private static readonly int s_OutputIndices = Shader.PropertyToID("g_SurfaceIndices");
        private static readonly int s_OutputArgs = Shader.PropertyToID("g_SurfaceArgs");
        private static readonly int s_IndexCapacity = Shader.PropertyToID("g_SurfaceIndexCapacity");
        private static readonly int s_GridSize = Shader.PropertyToID("g_SurfaceGridSize");
        private static readonly int s_Origin = Shader.PropertyToID("g_SurfaceOrigin");
        private static readonly int s_Spacing = Shader.PropertyToID("g_SurfaceSpacing");
        private static readonly int s_IsoLevel = Shader.PropertyToID("g_SurfaceIsoLevel");
        private static readonly int s_Source = Shader.PropertyToID("g_SurfaceSource");
        private static readonly int s_SourceStep = Shader.PropertyToID("g_SurfaceSourceStep");
        private static readonly int s_VoxelOrigin = Shader.PropertyToID("g_SurfaceVoxelOrigin");
        private static readonly int s_WindowOrigin = Shader.PropertyToID("g_WindowOrigin");
        private static readonly int s_WindowX = Shader.PropertyToID("g_WindowX");
        private static readonly int s_WindowY = Shader.PropertyToID("g_WindowY");
        private static readonly int s_WindowZ = Shader.PropertyToID("g_WindowZ");
        private static readonly int s_RegionWindow = Shader.PropertyToID("g_RegionWindow");
        private static readonly int s_BrickRefs = Shader.PropertyToID("g_BrickRefs");
        private static readonly int s_BrickVoxels = Shader.PropertyToID("g_BrickVoxels");
        private static readonly int s_BrickDensity = Shader.PropertyToID("g_BrickDensity");
        private static readonly int s_VertexBase = Shader.PropertyToID("g_SurfaceVertexBase");
        private static readonly int s_IndexBase = Shader.PropertyToID("g_SurfaceIndexBase");
        private static readonly int s_ArgsBase = Shader.PropertyToID("g_SurfaceArgsBase");
        private static readonly int s_TerrainSeed = Shader.PropertyToID("g_TerrainSeed");
        private static readonly int s_FarBaseHeight = Shader.PropertyToID("g_FarBaseHeight");
        private static readonly int s_FarEnabled = Shader.PropertyToID("g_FarEnabled");
        private static readonly int s_SurfaceIndexBaseMaterial =
            Shader.PropertyToID("_SurfaceIndexBase");

        public void Extract(ComputeShader shader, float[] density, uint[] materials,
                            int3 gridSize, Vector3 origin, float spacing, float isoLevel = 0.5f)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (gridSize.x < 2 || gridSize.y < 2 || gridSize.z < 2)
                throw new ArgumentOutOfRangeException(nameof(gridSize), "Each lattice axis needs two samples.");

            int sampleCount = checked(gridSize.x * gridSize.y * gridSize.z);
            if (density == null || density.Length != sampleCount)
                throw new ArgumentException($"Density needs exactly {sampleCount} samples.", nameof(density));
            if (materials == null || materials.Length != sampleCount)
                throw new ArgumentException($"Materials need exactly {sampleCount} samples.", nameof(materials));
            if (!(spacing > 0f)) throw new ArgumentOutOfRangeException(nameof(spacing));

            int3 cells = gridSize - 1;
            CellCount = checked(cells.x * cells.y * cells.z);

            // A lattice edge emits one quad (six indices). There are fewer than three edges per
            // sample in the positive directions, so this is a strict topology bound.
            int maxIndices = checked(sampleCount * 18);
            EnsureOutputCapacity(CellCount, maxIndices);
            EnsureDenseInputCapacity(sampleCount);

            _density.SetData(density);
            _materials.SetData(materials);
            _args.SetData(new uint[] { 0u, 1u, 0u, 0u });

            int vertexKernel = shader.FindKernel("CSBuildSurfaceVertices");
            int triangleKernel = shader.FindKernel("CSBuildSurfaceTriangles");
            BindCommon(shader, vertexKernel, gridSize, origin, spacing, isoLevel);
            BindCommon(shader, triangleKernel, gridSize, origin, spacing, isoLevel);
            BindDummySparse(shader, vertexKernel);
            BindDummySparse(shader, triangleKernel);
            shader.SetInt("g_SurfaceSource", 0);
            shader.SetInt("g_SurfaceSourceStep", 1);
            shader.SetInt("g_SurfaceVertexBase", 0);
            shader.SetInt("g_SurfaceIndexBase", 0);
            shader.SetInt("g_SurfaceArgsBase", 0);

            shader.SetBuffer(vertexKernel, "g_SurfaceVertices", _vertices);
            shader.SetBuffer(triangleKernel, "g_SurfaceVertices", _vertices);
            shader.SetBuffer(triangleKernel, "g_SurfaceIndices", _indices);
            BindOutputArgs(shader, triangleKernel);

            shader.Dispatch(vertexKernel, DivideRoundUp(cells.x, 4), DivideRoundUp(cells.y, 4),
                            DivideRoundUp(cells.z, 4));
            shader.Dispatch(triangleKernel, DivideRoundUp(gridSize.x, 4),
                            DivideRoundUp(gridSize.y, 4), DivideRoundUp(gridSize.z, 4));
            FinalizeArgs(shader);
        }

        /// <summary>
        /// Extracts directly from the live sparse GPU mirror. Density and materials stay on GPU;
        /// only the chunk coordinate and bounded dispatch enter from CPU.
        /// </summary>
        public void ExtractSparse(ComputeShader shader, VoxelGpuBuffers source, int3 gridSize,
                                  int3 worldVoxelOrigin, float voxelSize, float isoLevel = 0.5f,
                                  int maxIndexCount = 0)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (source == null || !source.IsCreated)
                throw new ArgumentException("The sparse GPU source must be created.", nameof(source));
            if (gridSize.x < 2 || gridSize.y < 2 || gridSize.z < 2)
                throw new ArgumentOutOfRangeException(nameof(gridSize));
            if (!(voxelSize > 0f)) throw new ArgumentOutOfRangeException(nameof(voxelSize));

            int sampleCount = checked(gridSize.x * gridSize.y * gridSize.z);
            int3 cells = gridSize - 1;
            CellCount = checked(cells.x * cells.y * cells.z);
            int strictMaxIndices = checked(sampleCount * 18);
            int indexCapacity = maxIndexCount > 0
                              ? math.min(maxIndexCount, strictMaxIndices) : strictMaxIndices;
            EnsureOutputCapacity(CellCount, indexCapacity);

            // Metal requires every statically referenced resource to be bound even when the
            // runtime source branch does not read it.
            EnsureDenseInputCapacity(1);
            _args.SetData(new uint[] { 0u, 1u, 0u, 0u });

            int vertexKernel = shader.FindKernel("CSBuildSurfaceVertices");
            int triangleKernel = shader.FindKernel("CSBuildSurfaceTriangles");
            Vector3 originMetres = (new Vector3(worldVoxelOrigin.x, worldVoxelOrigin.y,
                                                worldVoxelOrigin.z) + Vector3.one * 0.5f)
                                 * voxelSize;
            BindCommon(shader, vertexKernel, gridSize, originMetres, voxelSize, isoLevel);
            BindCommon(shader, triangleKernel, gridSize, originMetres, voxelSize, isoLevel);
            shader.SetInt("g_SurfaceSource", 1);
            shader.SetInt("g_SurfaceSourceStep", 1);
            shader.SetInt("g_SurfaceVertexBase", VertexBase);
            shader.SetInt("g_SurfaceIndexBase", IndexBase);
            shader.SetInt("g_SurfaceArgsBase", ArgsBase);
            shader.SetInts("g_SurfaceVoxelOrigin", worldVoxelOrigin.x, worldVoxelOrigin.y,
                           worldVoxelOrigin.z);
            int3 windowOrigin = source.WindowOrigin;
            shader.SetInts("g_WindowOrigin", windowOrigin.x, windowOrigin.y, windowOrigin.z);
            shader.SetInt("g_WindowX", VoxelGpuBuffers.WindowX);
            shader.SetInt("g_WindowY", VoxelGpuBuffers.WindowY);
            shader.SetInt("g_WindowZ", VoxelGpuBuffers.WindowZ);

            BindSparse(shader, vertexKernel, source);
            BindSparse(shader, triangleKernel, source);
            shader.SetBuffer(vertexKernel, "g_SurfaceVertices", _vertices);
            shader.SetBuffer(triangleKernel, "g_SurfaceVertices", _vertices);
            shader.SetBuffer(triangleKernel, "g_SurfaceIndices", _indices);
            BindOutputArgs(shader, triangleKernel);

            shader.Dispatch(vertexKernel, DivideRoundUp(cells.x, 4), DivideRoundUp(cells.y, 4),
                            DivideRoundUp(cells.z, 4));
            shader.Dispatch(triangleKernel, DivideRoundUp(gridSize.x, 4),
                            DivideRoundUp(gridSize.y, 4), DivideRoundUp(gridSize.z, 4));
            FinalizeArgs(shader);
        }

        /// <summary>
        /// Records sparse extraction into an existing render-graph command stream. This is the
        /// production path: density generation and meshing remain ordered on the GPU, with no
        /// CPU readback and no immediate dispatch that could race the preceding density pass.
        /// </summary>
        public void RecordExtractSparse(CommandBuffer commandBuffer, ComputeShader shader,
                                        VoxelGpuBuffers source, int3 gridSize,
                                        int3 worldVoxelOrigin, float voxelSize,
                                        float isoLevel = 0.5f, int maxIndexCount = 0,
                                        int sourceStep = 1)
        {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (source == null || !source.IsCreated)
                throw new ArgumentException("The sparse GPU source must be created.", nameof(source));
            if (gridSize.x < 2 || gridSize.y < 2 || gridSize.z < 2)
                throw new ArgumentOutOfRangeException(nameof(gridSize));
            if (!(voxelSize > 0f)) throw new ArgumentOutOfRangeException(nameof(voxelSize));
            if (sourceStep < 1) throw new ArgumentOutOfRangeException(nameof(sourceStep));

            int sampleCount = checked(gridSize.x * gridSize.y * gridSize.z);
            int3 cells = gridSize - 1;
            CellCount = checked(cells.x * cells.y * cells.z);
            int strictMaxIndices = checked(sampleCount * 18);
            int indexCapacity = maxIndexCount > 0
                              ? math.min(maxIndexCount, strictMaxIndices) : strictMaxIndices;
            indexCapacity = math.max(6, indexCapacity / 6 * 6);
            EnsureOutputCapacity(CellCount, indexCapacity);
            EnsureDenseInputCapacity(1);

            int clearKernel = shader.FindKernel("CSClearSurfaceArgs");
            int vertexKernel = shader.FindKernel("CSBuildSurfaceVertices");
            int triangleKernel = shader.FindKernel("CSBuildSurfaceTriangles");
            int finalizeKernel = shader.FindKernel("CSFinalizeSurfaceArgs");

            Vector3 originMetres = (new Vector3(worldVoxelOrigin.x, worldVoxelOrigin.y,
                                                worldVoxelOrigin.z) + Vector3.one * 0.5f)
                                 * voxelSize;
            RecordCommon(commandBuffer, shader, gridSize, originMetres,
                         voxelSize * sourceStep, isoLevel, worldVoxelOrigin, source, sourceStep);

            RecordInputBindings(commandBuffer, shader, vertexKernel, source);
            RecordInputBindings(commandBuffer, shader, triangleKernel, source);
            ComputeBuffer vertexTarget = VertexBuffer;
            ComputeBuffer indexTarget = IndexBuffer;
            ComputeBuffer argsTarget = ArgsBuffer;
            commandBuffer.SetComputeBufferParam(shader, vertexKernel, s_OutputVertices, vertexTarget);
            commandBuffer.SetComputeBufferParam(shader, triangleKernel, s_OutputVertices, vertexTarget);
            commandBuffer.SetComputeBufferParam(shader, triangleKernel, s_OutputIndices, indexTarget);
            commandBuffer.SetComputeBufferParam(shader, clearKernel, s_OutputArgs, argsTarget);
            commandBuffer.SetComputeBufferParam(shader, triangleKernel, s_OutputArgs, argsTarget);
            commandBuffer.SetComputeBufferParam(shader, finalizeKernel, s_OutputArgs, argsTarget);
            commandBuffer.SetComputeIntParam(shader, s_IndexCapacity, _indexCapacity);
            commandBuffer.SetComputeIntParam(shader, s_VertexBase, VertexBase);
            commandBuffer.SetComputeIntParam(shader, s_IndexBase, IndexBase);
            commandBuffer.SetComputeIntParam(shader, s_ArgsBase, ArgsBase);

            commandBuffer.DispatchCompute(shader, clearKernel, 1, 1, 1);
            commandBuffer.DispatchCompute(shader, vertexKernel,
                DivideRoundUp(cells.x, 4), DivideRoundUp(cells.y, 4), DivideRoundUp(cells.z, 4));
            commandBuffer.DispatchCompute(shader, triangleKernel,
                DivideRoundUp(gridSize.x, 4), DivideRoundUp(gridSize.y, 4),
                DivideRoundUp(gridSize.z, 4));
            commandBuffer.DispatchCompute(shader, finalizeKernel, 1, 1, 1);
        }

        /// <summary>Enqueues a procedural indexed draw without copying mesh data back to CPU.</summary>
        public void Draw(CommandBuffer commandBuffer, Material material,
                         MaterialPropertyBlock properties = null, int shaderPass = 0)
        {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (ArgsBuffer == null) return;

            properties ??= new MaterialPropertyBlock();
            properties.SetBuffer(s_SurfaceVertices, VertexBuffer);
            properties.SetBuffer(s_SurfaceIndices, IndexBuffer);
            properties.SetInt(s_SurfaceIndexBaseMaterial, IndexBase);
            commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, shaderPass,
                MeshTopology.Triangles, ArgsBuffer, ArgsByteOffset, properties);
        }

        /// <summary>Render-graph raster equivalent of <see cref="Draw(CommandBuffer,Material,MaterialPropertyBlock,int)"/>.</summary>
        public void Draw(RasterCommandBuffer commandBuffer, Material material,
                         MaterialPropertyBlock properties = null, int shaderPass = 0)
        {
            if (commandBuffer == null) throw new ArgumentNullException(nameof(commandBuffer));
            if (material == null) throw new ArgumentNullException(nameof(material));
            if (ArgsBuffer == null) return;

            properties ??= new MaterialPropertyBlock();
            properties.SetBuffer(s_SurfaceVertices, VertexBuffer);
            properties.SetBuffer(s_SurfaceIndices, IndexBuffer);
            properties.SetInt(s_SurfaceIndexBaseMaterial, IndexBase);
            commandBuffer.DrawProceduralIndirect(Matrix4x4.identity, material, shaderPass,
                MeshTopology.Triangles, ArgsBuffer, ArgsByteOffset, properties);
        }

        public int ReadIndexCount()
        {
            ComputeBuffer args = ArgsBuffer;
            if (args == null) return 0;
            var values = new uint[GpuSurfaceArena.ArgsStride];
            args.GetData(values, 0, ArgsBase, GpuSurfaceArena.ArgsStride);
            return checked((int)values[0]);
        }

        private void BindCommon(ComputeShader shader, int kernel, int3 gridSize, Vector3 origin,
                                float spacing, float isoLevel)
        {
            shader.SetInts("g_SurfaceGridSize", gridSize.x, gridSize.y, gridSize.z);
            shader.SetVector("g_SurfaceOrigin", origin);
            shader.SetFloat("g_SurfaceSpacing", spacing);
            shader.SetFloat("g_SurfaceIsoLevel", isoLevel);
            shader.SetInt("g_SurfaceSource", 0);
            BindFarField(shader);
            shader.SetBuffer(kernel, "g_SurfaceDensity", _density);
            shader.SetBuffer(kernel, "g_SurfaceMaterials", _materials);
        }

        // The sparse source continues into the implicit far field where no region is resident;
        // the dense fixture has no world, so isolated extraction keeps it disabled.
        private static void BindFarField(ComputeShader shader)
        {
            shader.SetInt(s_TerrainSeed, unchecked((int)VoxelRenderBridge.TerrainSeed));
            shader.SetInt(s_FarBaseHeight, (int)VoxelRenderBridge.FarBaseHeight);
            shader.SetInt(s_FarEnabled, VoxelRenderBridge.FarFieldEnabled ? 1 : 0);
        }

        private void RecordCommon(CommandBuffer commandBuffer, ComputeShader shader,
                                  int3 gridSize, Vector3 origin, float spacing, float isoLevel,
                                  int3 voxelOrigin, VoxelGpuBuffers source, int sourceStep)
        {
            commandBuffer.SetComputeIntParams(shader, s_GridSize,
                gridSize.x, gridSize.y, gridSize.z);
            commandBuffer.SetComputeVectorParam(shader, s_Origin, origin);
            commandBuffer.SetComputeFloatParam(shader, s_Spacing, spacing);
            commandBuffer.SetComputeFloatParam(shader, s_IsoLevel, isoLevel);
            commandBuffer.SetComputeIntParam(shader, s_Source, 1);
            commandBuffer.SetComputeIntParam(shader, s_SourceStep, sourceStep);
            commandBuffer.SetComputeIntParam(shader, s_TerrainSeed,
                unchecked((int)VoxelRenderBridge.TerrainSeed));
            commandBuffer.SetComputeIntParam(shader, s_FarBaseHeight,
                (int)VoxelRenderBridge.FarBaseHeight);
            commandBuffer.SetComputeIntParam(shader, s_FarEnabled,
                VoxelRenderBridge.FarFieldEnabled ? 1 : 0);
            commandBuffer.SetComputeIntParams(shader, s_VoxelOrigin,
                voxelOrigin.x, voxelOrigin.y, voxelOrigin.z);
            int3 windowOrigin = source.WindowOrigin;
            commandBuffer.SetComputeIntParams(shader, s_WindowOrigin,
                windowOrigin.x, windowOrigin.y, windowOrigin.z);
            commandBuffer.SetComputeIntParam(shader, s_WindowX, VoxelGpuBuffers.WindowX);
            commandBuffer.SetComputeIntParam(shader, s_WindowY, VoxelGpuBuffers.WindowY);
            commandBuffer.SetComputeIntParam(shader, s_WindowZ, VoxelGpuBuffers.WindowZ);
        }

        private void RecordInputBindings(CommandBuffer commandBuffer, ComputeShader shader,
                                         int kernel, VoxelGpuBuffers source)
        {
            commandBuffer.SetComputeBufferParam(shader, kernel, s_SurfaceDensity, _density);
            commandBuffer.SetComputeBufferParam(shader, kernel, s_SurfaceMaterials, _materials);
            commandBuffer.SetComputeBufferParam(shader, kernel, s_RegionWindow,
                                                source.WindowBuffer);
            commandBuffer.SetComputeBufferParam(shader, kernel, s_BrickRefs,
                                                source.BrickRefBuffer);
            commandBuffer.SetComputeBufferParam(shader, kernel, s_BrickVoxels,
                                                source.VoxelBuffer);
            commandBuffer.SetComputeBufferParam(shader, kernel, s_BrickDensity,
                                                source.DensityBuffer);
        }

        private void BindSparse(ComputeShader shader, int kernel, VoxelGpuBuffers source)
        {
            shader.SetBuffer(kernel, "g_RegionWindow", source.WindowBuffer);
            shader.SetBuffer(kernel, "g_BrickRefs", source.BrickRefBuffer);
            shader.SetBuffer(kernel, "g_BrickVoxels", source.VoxelBuffer);
            shader.SetBuffer(kernel, "g_BrickDensity", source.DensityBuffer);
        }

        private void BindDummySparse(ComputeShader shader, int kernel)
        {
            if (_dummyInt == null)
            {
                _dummyInt = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Structured);
                _dummyInt.SetData(new[] { -1 });
            }
            if (_dummyUInt == null)
            {
                _dummyUInt = new ComputeBuffer(1, sizeof(uint), ComputeBufferType.Structured);
                _dummyUInt.SetData(new[] { 0u });
            }

            shader.SetBuffer(kernel, "g_RegionWindow", _dummyInt);
            shader.SetBuffer(kernel, "g_BrickRefs", _dummyInt);
            shader.SetBuffer(kernel, "g_BrickVoxels", _dummyUInt);
            shader.SetBuffer(kernel, "g_BrickDensity", _dummyUInt);
        }

        private void BindOutputArgs(ComputeShader shader, int triangleKernel)
        {
            shader.SetBuffer(triangleKernel, "g_SurfaceArgs", _args);
            shader.SetInt("g_SurfaceIndexCapacity", _indexCapacity);
        }

        private void FinalizeArgs(ComputeShader shader)
        {
            int kernel = shader.FindKernel("CSFinalizeSurfaceArgs");
            shader.SetBuffer(kernel, "g_SurfaceArgs", _args);
            shader.SetInt("g_SurfaceIndexCapacity", _indexCapacity);
            shader.Dispatch(kernel, 1, 1, 1);
        }

        private void EnsureDenseInputCapacity(int samples)
        {
            if (_sampleCapacity != samples)
            {
                Release(ref _density);
                Release(ref _materials);
                _density = new ComputeBuffer(samples, sizeof(float), ComputeBufferType.Structured);
                _materials = new ComputeBuffer(samples, sizeof(uint), ComputeBufferType.Structured);
                _sampleCapacity = samples;
            }
        }

        private void EnsureOutputCapacity(int cells, int indices)
        {
            if (ArenaBacked)
            {
                if (cells > _arena.CellsPerChunk)
                    throw new InvalidOperationException(
                        $"Chunk needs {cells} cells but the arena reserves {_arena.CellsPerChunk}.");
                return;
            }

            if (_cellCapacity != cells)
            {
                Release(ref _vertices);
                _vertices = new ComputeBuffer(cells, SmoothSurfaceVertex.Stride,
                                              ComputeBufferType.Structured);
                _cellCapacity = cells;
            }

            if (_indexCapacity != indices)
            {
                Release(ref _indices);
                _indices = new ComputeBuffer(indices, sizeof(uint), ComputeBufferType.Structured);
                _indexCapacity = indices;
            }

            if (_args == null)
                _args = new ComputeBuffer(4, sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        private static int DivideRoundUp(int value, int divisor) => (value + divisor - 1) / divisor;

        private static void Release(ref ComputeBuffer buffer)
        {
            if (buffer == null) return;
            buffer.Release();
            buffer = null;
        }

        public void Dispose()
        {
            // Arena buffers outlive any one chunk; only the slot goes back.
            if (ArenaBacked) _arena.Release(_slot);
            Release(ref _density);
            Release(ref _materials);
            Release(ref _vertices);
            Release(ref _indices);
            Release(ref _args);
            Release(ref _dummyInt);
            Release(ref _dummyUInt);
            _sampleCapacity = 0;
            _cellCapacity = 0;
            _indexCapacity = 0;
            CellCount = 0;
        }
    }
}
