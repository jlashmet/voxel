using System;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Core.Storage;
using VoxelEngine.Rendering;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Fixed-capacity GPU debris simulation. Detached voxel data is uploaded once, transformed
    /// and drawn entirely on the GPU, then read back only after bodies report that they settled.
    /// </summary>
    public sealed class GpuDebrisSystem : IDisposable
    {
        public const int MaxChunks = 1024;
        public const int VoxelsPerChunk = 64;
        private const int MaxSubmissionsPerFrame = 96;
        private const int MaxSettlesPerReadback = 48;
        private const float ReadbackInterval = 0.2f;

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuState
        {
            public Vector4 PositionAge;
            public Vector4 Rotation;
            public Vector4 VelocitySettled;
            public Vector4 AngularGround;
            public Vector4 ContactActive;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuInstance
        {
            public Vector4 LocalSlot;
            public Vector4 Colour;
        }

        private sealed class ChunkRecord
        {
            public ShowcaseWorld.DetachedVoxelChunk Chunk;
            public Vector3 OriginalPivot;
        }

        private readonly GpuState[] _states = new GpuState[MaxChunks];
        private readonly GpuInstance[] _instances = new GpuInstance[MaxChunks * VoxelsPerChunk];
        private readonly ChunkRecord[] _records = new ChunkRecord[MaxChunks];
        private readonly ComputeShader _compute;
        private readonly int _integrateKernel;
        private readonly ComputeBuffer _stateBuffer;
        private readonly ComputeBuffer _instanceBuffer;
        private readonly ComputeBuffer _argumentsBuffer;
        private readonly Mesh _cube;
        private readonly Material _material;
        private readonly Bounds _drawBounds = new(Vector3.zero, Vector3.one * 10000f);
        private AsyncGPUReadbackRequest _readback;
        private bool _readbackPending;
        private float _nextReadback;
        private bool _disposed;
        private int _highestActiveSlot = -1;

        public bool Available { get; }
        public int ActiveChunks { get; private set; }
        public int ActiveVoxels { get; private set; }

        public GpuDebrisSystem()
        {
            _compute = Resources.Load<ComputeShader>("GpuDebris");
            Shader shader = Resources.Load<Shader>("GpuDebris");
            Available = SystemInfo.supportsComputeShaders && _compute != null && shader != null;
            if (!Available) return;

            _integrateKernel = _compute.FindKernel("Integrate");
            _stateBuffer = new ComputeBuffer(MaxChunks, Marshal.SizeOf<GpuState>(),
                                             ComputeBufferType.Structured);
            _instanceBuffer = new ComputeBuffer(MaxChunks * VoxelsPerChunk,
                                                Marshal.SizeOf<GpuInstance>(),
                                                ComputeBufferType.Structured);
            _argumentsBuffer = new ComputeBuffer(1, sizeof(uint) * 5,
                                                 ComputeBufferType.IndirectArguments);
            _cube = BuildCube();
            _material = new Material(shader) { name = "Runtime GPU debris", enableInstancing = true };

            _stateBuffer.SetData(_states);
            _instanceBuffer.SetData(_instances);
            _argumentsBuffer.SetData(new uint[]
            {
                _cube.GetIndexCount(0), 0,
                _cube.GetIndexStart(0), (uint)_cube.GetBaseVertex(0), 0,
            });
            _compute.SetBuffer(_integrateKernel, "_States", _stateBuffer);
            _material.SetBuffer("_States", _stateBuffer);
            _material.SetBuffer("_Instances", _instanceBuffer);
        }

        public void Step(ShowcaseWorld world, float deltaTime)
        {
            if (_disposed) return;
            if (!Available)
            {
                while (world.TryDequeueDetachedChunk(out var rejected))
                    world.RestoreDetachedChunk(rejected);
                return;
            }

            PollReadback(world);
            SubmitPending(world);

            if (ActiveChunks > 0 && deltaTime > 0f)
            {
                _compute.SetFloat("_DeltaTime", deltaTime);
                _compute.SetInt("_StateCount", MaxChunks);
                _compute.Dispatch(_integrateKernel, (MaxChunks + 63) / 64, 1, 1);

                Graphics.DrawMeshInstancedIndirect(
                    _cube, 0, _material, _drawBounds, _argumentsBuffer, 0, null,
                    ShadowCastingMode.Off, false, 0, null, LightProbeUsage.Off);
            }

            if (!_readbackPending && ActiveChunks > 0 && Time.unscaledTime >= _nextReadback)
            {
                _readback = AsyncGPUReadback.Request(_stateBuffer);
                _readbackPending = true;
                _nextReadback = Time.unscaledTime + ReadbackInterval;
            }
        }

        private void SubmitPending(ShowcaseWorld world)
        {
            int submitted = 0;
            int minSlot = MaxChunks;
            int maxSlot = -1;

            while (submitted < MaxSubmissionsPerFrame
                   && world.TryDequeueDetachedChunk(out var chunk))
            {
                int slot = FindFreeSlot();
                if (slot < 0)
                {
                    world.RestoreDetachedChunk(chunk);
                    break;
                }

                Vector3 pivot = Vector3.zero;
                for (int i = 0; i < chunk.Voxels.Length; i++)
                    pivot += ((Vector3)(float3)chunk.Voxels[i] + Vector3.one * 0.5f)
                           * VoxelSurfaceRenderer.VoxelSize;
                pivot /= math.max(1, chunk.Voxels.Length);

                float collisionRadius = 0.087f;
                int instanceStart = slot * VoxelsPerChunk;
                for (int i = 0; i < VoxelsPerChunk; i++)
                {
                    if (i >= chunk.Voxels.Length)
                    {
                        _instances[instanceStart + i] = new GpuInstance
                        {
                            LocalSlot = new Vector4(0f, 0f, 0f, slot),
                            Colour = Vector4.zero,
                        };
                        continue;
                    }

                    Vector3 centre = ((Vector3)(float3)chunk.Voxels[i] + Vector3.one * 0.5f)
                                   * VoxelSurfaceRenderer.VoxelSize;
                    Vector3 local = centre - pivot;
                    collisionRadius = math.max(collisionRadius, local.magnitude + 0.087f);
                    _instances[instanceStart + i] = new GpuInstance
                    {
                        LocalSlot = new Vector4(local.x, local.y, local.z, slot),
                        Colour = MaterialColour(chunk.Materials[i]),
                    };
                }

                uint hash = Hash((uint)(chunk.Voxels[0].x * 73856093)
                               ^ (uint)(chunk.Voxels[0].y * 19349663)
                               ^ (uint)(chunk.Voxels[0].z * 83492791));
                float3 radial = (float3)pivot - chunk.ImpactMetres;
                radial.y = math.max(0.15f, radial.y);
                radial = math.normalizesafe(radial, new float3(0f, 1f, 0f));
                float3 direction = math.normalizesafe(chunk.ImpulseDirection);
                float jitterX = Signed(hash);
                float jitterZ = Signed(Hash(hash + 17u));
                float force = math.lerp(2.5f, 7.5f, Unit(Hash(hash + 31u)));
                float materialScale = chunk.Materials[0] switch
                {
                    ShowcaseWorld.MatWood => 0.58f,
                    9 => 0.50f,  // cloth
                    10 => 0.38f, // foliage/grass
                    14 => 0.45f, // moss
                    _ => 1f,
                };
                float massScale = math.clamp(math.rsqrt(math.max(1f, chunk.Voxels.Length / 8f)),
                                             0.45f, 1f);
                float impulseScale = materialScale * massScale;
                float3 velocity = radial * force + direction * 5.5f
                                + new float3(jitterX * 2.2f,
                                             math.lerp(2.5f, 6.5f, Unit(Hash(hash + 47u))),
                                             jitterZ * 2.2f);
                float3 angular = new float3(Signed(Hash(hash + 61u)),
                                            Signed(Hash(hash + 79u)),
                                            Signed(Hash(hash + 97u))) * 7f;
                velocity *= impulseScale;
                angular *= math.lerp(0.65f, 1f, impulseScale);
                float ground = world.FindLandingCentreY(pivot, collisionRadius);

                _states[slot] = new GpuState
                {
                    PositionAge = new Vector4(pivot.x, pivot.y, pivot.z, 0f),
                    Rotation = new Vector4(0f, 0f, 0f, 1f),
                    VelocitySettled = new Vector4(velocity.x, velocity.y, velocity.z, 0f),
                    AngularGround = new Vector4(angular.x, angular.y, angular.z, ground),
                    ContactActive = new Vector4(0f, 1f, 0f, 0f),
                };
                _records[slot] = new ChunkRecord { Chunk = chunk, OriginalPivot = pivot };
                _highestActiveSlot = math.max(_highestActiveSlot, slot);
                ActiveChunks++;
                ActiveVoxels += chunk.Voxels.Length;
                minSlot = math.min(minSlot, slot);
                maxSlot = math.max(maxSlot, slot);
                submitted++;
            }

            if (maxSlot < minSlot) return;
            int stateCount = maxSlot - minSlot + 1;
            _stateBuffer.SetData(_states, minSlot, minSlot, stateCount);
            int firstInstance = minSlot * VoxelsPerChunk;
            int instanceCount = stateCount * VoxelsPerChunk;
            _instanceBuffer.SetData(_instances, firstInstance, firstInstance, instanceCount);
            UpdateDrawArguments();
        }

        private void PollReadback(ShowcaseWorld world)
        {
            if (!_readbackPending || !_readback.done) return;
            _readbackPending = false;
            if (_readback.hasError) return;

            var states = _readback.GetData<GpuState>();
            int settled = 0;
            int minSlot = MaxChunks;
            int maxSlot = -1;
            for (int slot = 0; slot < MaxChunks && settled < MaxSettlesPerReadback; slot++)
            {
                var record = _records[slot];
                if (record == null || states[slot].VelocitySettled.w < 0.5f) continue;

                GpuState state = states[slot];
                var position = new Vector3(state.PositionAge.x, state.PositionAge.y, state.PositionAge.z);
                var rotation = new Quaternion(state.Rotation.x, state.Rotation.y,
                                              state.Rotation.z, state.Rotation.w).normalized;
                world.SettleDetachedChunk(record.Chunk, position, rotation, record.OriginalPivot);

                ActiveChunks--;
                ActiveVoxels -= record.Chunk.Voxels.Length;
                _records[slot] = null;
                _states[slot] = default;
                minSlot = math.min(minSlot, slot);
                maxSlot = math.max(maxSlot, slot);
                settled++;
            }

            if (maxSlot >= minSlot)
            {
                _stateBuffer.SetData(_states, minSlot, minSlot, maxSlot - minSlot + 1);
                while (_highestActiveSlot >= 0 && _records[_highestActiveSlot] == null)
                    _highestActiveSlot--;
                UpdateDrawArguments();
            }
        }

        private void UpdateDrawArguments()
        {
            _argumentsBuffer.SetData(new uint[]
            {
                _cube.GetIndexCount(0),
                (uint)((_highestActiveSlot + 1) * VoxelsPerChunk),
                _cube.GetIndexStart(0), (uint)_cube.GetBaseVertex(0), 0,
            });
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _records.Length; i++)
                if (_records[i] == null) return i;
            return -1;
        }

        private static Vector4 MaterialColour(byte material) => material switch
        {
            ShowcaseWorld.MatWood => new Vector4(0.43f, 0.25f, 0.12f, 1f),
            ShowcaseWorld.MatSand => new Vector4(0.72f, 0.64f, 0.42f, 1f),
            ShowcaseWorld.MatGlass => new Vector4(0.52f, 0.78f, 0.88f, 1f),
            7 => new Vector4(0.20f, 0.24f, 0.30f, 1f),
            8 => new Vector4(0.42f, 0.18f, 0.12f, 1f),
            10 => new Vector4(0.25f, 0.46f, 0.15f, 1f),
            13 => new Vector4(0.32f, 0.22f, 0.13f, 1f),
            14 => new Vector4(0.22f, 0.38f, 0.18f, 1f),
            _ => new Vector4(0.48f, 0.50f, 0.54f, 1f),
        };

        private static uint Hash(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }

        private static float Unit(uint value) => (value & 0x00FFFFFFu) / 16777215f;
        private static float Signed(uint value) => Unit(value) * 2f - 1f;

        private static Mesh BuildCube()
        {
            var mesh = new Mesh { name = "Runtime debris cube" };
            Vector3[] vertices =
            {
                new(-.5f,-.5f,-.5f), new(.5f,-.5f,-.5f), new(.5f,.5f,-.5f), new(-.5f,.5f,-.5f),
                new(.5f,-.5f,.5f), new(-.5f,-.5f,.5f), new(-.5f,.5f,.5f), new(.5f,.5f,.5f),
                new(-.5f,-.5f,.5f), new(-.5f,-.5f,-.5f), new(-.5f,.5f,-.5f), new(-.5f,.5f,.5f),
                new(.5f,-.5f,-.5f), new(.5f,-.5f,.5f), new(.5f,.5f,.5f), new(.5f,.5f,-.5f),
                new(-.5f,.5f,-.5f), new(.5f,.5f,-.5f), new(.5f,.5f,.5f), new(-.5f,.5f,.5f),
                new(-.5f,-.5f,.5f), new(.5f,-.5f,.5f), new(.5f,-.5f,-.5f), new(-.5f,-.5f,-.5f),
            };
            int[] triangles =
            {
                0,2,1, 0,3,2, 4,6,5, 4,7,6, 8,10,9, 8,11,10,
                12,14,13, 12,15,14, 16,18,17, 16,19,18, 20,22,21, 20,23,22,
            };
            Vector3[] normals = new Vector3[24];
            for (int i = 0; i < 4; i++) normals[i] = Vector3.back;
            for (int i = 4; i < 8; i++) normals[i] = Vector3.forward;
            for (int i = 8; i < 12; i++) normals[i] = Vector3.left;
            for (int i = 12; i < 16; i++) normals[i] = Vector3.right;
            for (int i = 16; i < 20; i++) normals[i] = Vector3.up;
            for (int i = 20; i < 24; i++) normals[i] = Vector3.down;
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.normals = normals;
            mesh.RecalculateBounds();
            return mesh;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_readbackPending) _readback.WaitForCompletion();
            _argumentsBuffer?.Dispose();
            _instanceBuffer?.Dispose();
            _stateBuffer?.Dispose();
            if (_material != null) UnityEngine.Object.Destroy(_material);
            if (_cube != null) UnityEngine.Object.Destroy(_cube);
        }
    }
}
