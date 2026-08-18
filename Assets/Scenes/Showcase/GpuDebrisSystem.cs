using System;
using System.Runtime.InteropServices;
using Game.Composition.Materials;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Fixed-capacity, visual-only GPU debris simulation. Detached voxel samples are uploaded
    /// once, transformed and drawn entirely on the GPU, then discarded on a short CPU lifetime.
    /// They never re-enter collision or authoritative voxel storage.
    /// </summary>
    public sealed class GpuDebrisSystem : IDisposable
    {
        public const int MaxChunks = 256;
        public const int MaxVoxelsPerChunk = 16;
        public const int RenderInstancesPerChunk = 16;
        public const int MaxSubmissionsPerFrame = 192;

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

        private struct ChunkRecord
        {
            public bool Active;
            public int VoxelCount;
            public float ExpireAt;
        }

        private readonly GpuState[] _states = new GpuState[MaxChunks];
        private readonly GpuInstance[] _instances =
            new GpuInstance[MaxChunks * RenderInstancesPerChunk];
        private readonly ChunkRecord[] _records = new ChunkRecord[MaxChunks];
        private readonly uint[] _drawArguments = new uint[5];
        private readonly ComputeShader _compute;
        private readonly int _integrateKernel;
        private readonly ComputeBuffer _stateBuffer;
        private readonly ComputeBuffer _instanceBuffer;
        private readonly ComputeBuffer _argumentsBuffer;
        private readonly Mesh _cube;
        private readonly Material _material;
        private readonly Bounds _drawBounds = new(Vector3.zero, Vector3.one * 10000f);
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
            _instanceBuffer = new ComputeBuffer(MaxChunks * RenderInstancesPerChunk,
                                                Marshal.SizeOf<GpuInstance>(),
                                                ComputeBufferType.Structured);
            _argumentsBuffer = new ComputeBuffer(1, sizeof(uint) * 5,
                                                 ComputeBufferType.IndirectArguments);
            _cube = BuildCube();
            _material = new Material(shader) { name = "Runtime GPU debris", enableInstancing = true };

            _stateBuffer.SetData(_states);
            _instanceBuffer.SetData(_instances);
            UpdateDrawArguments();
            _compute.SetBuffer(_integrateKernel, "_States", _stateBuffer);
            _material.SetBuffer("_States", _stateBuffer);
            _material.SetBuffer("_Instances", _instanceBuffer);
        }

        public void Step(ShowcaseWorld world, float deltaTime)
        {
            if (_disposed) return;
            if (!Available)
            {
                // Visual debris is deliberately lossy. On a device without compute support the
                // destroyed voxels stay gone and the presentation samples are simply discarded.
                while (world.TryDequeueDetachedChunk(out _)) { }
                return;
            }

            ExpireVisuals(Time.unscaledTime);
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
        }

        private void SubmitPending(ShowcaseWorld world)
        {
            int submitted = 0;
            int minSlot = MaxChunks;
            int maxSlot = -1;

            // Drain the entire event in one frame. Only the first budgeted samples become GPU
            // visuals; the remainder disappear immediately instead of leaking into a queue that
            // emits a fake secondary explosion on every later frame.
            while (world.TryDequeueDetachedChunk(out var chunk))
            {
                if (submitted >= MaxSubmissionsPerFrame) continue;
                int sourceCount = chunk.Voxels.Length;
                if (sourceCount == 0) continue;
                Vector3 pivot = Vector3.zero;
                for (int i = 0; i < sourceCount; i++)
                {
                    pivot += ((Vector3)(float3)chunk.Voxels[i] + Vector3.one * 0.5f)
                           * ShowcaseWorld.VoxelSize;
                }
                pivot /= sourceCount;

                int slot = FindFreeSlot();
                if (slot < 0) continue;

                float collisionRadius = 0.087f;
                int instanceStart = slot * RenderInstancesPerChunk;
                int visibleCount = math.min(RenderInstancesPerChunk, sourceCount);
                float sourceFraction = sourceCount / (float)math.max(1, chunk.Voxels.Length);
                int representedSourceVoxels = math.max(
                    sourceCount,
                    (int)math.ceil(chunk.SourceVoxelCount * sourceFraction));
                float visualScale = math.pow(math.max(1, representedSourceVoxels)
                                             / (float)visibleCount, 1f / 3f);
                int firstVisibleSource = -1;
                for (int i = 0; i < RenderInstancesPerChunk; i++)
                {
                    if (i >= visibleCount)
                    {
                        _instances[instanceStart + i] = new GpuInstance
                        {
                            LocalSlot = new Vector4(0f, 0f, 0f, slot),
                            Colour = Vector4.zero,
                        };
                        continue;
                    }

                    int sourceIndex = FindVisibleSourceIndex(chunk, i, visibleCount, sourceCount);
                    if (firstVisibleSource < 0) firstVisibleSource = sourceIndex;
                    Vector3 centre = ((Vector3)(float3)chunk.Voxels[sourceIndex]
                                   + Vector3.one * 0.5f)
                                   * ShowcaseWorld.VoxelSize;
                    Vector3 local = centre - pivot;
                    collisionRadius = math.max(collisionRadius, local.magnitude + 0.087f);
                    _instances[instanceStart + i] = new GpuInstance
                    {
                        LocalSlot = new Vector4(local.x, local.y, local.z, slot),
                        Colour = MaterialColour(chunk.Materials[sourceIndex],
                                                CoatingAt(chunk, sourceIndex), visualScale),
                    };
                }

                if (firstVisibleSource < 0) continue;
                uint hash = Hash((uint)(chunk.Voxels[firstVisibleSource].x * 73856093)
                               ^ (uint)(chunk.Voxels[firstVisibleSource].y * 19349663)
                               ^ (uint)(chunk.Voxels[firstVisibleSource].z * 83492791));
                float3 radial = (float3)pivot - chunk.ImpactMetres;
                radial.y = math.max(0.15f, radial.y);
                radial = math.normalizesafe(radial, new float3(0f, 1f, 0f));
                float3 direction = math.normalizesafe(chunk.ImpulseDirection);
                float jitterX = Signed(hash);
                float jitterZ = Signed(Hash(hash + 17u));
                float force = math.lerp(2.5f, 7.5f, Unit(Hash(hash + 31u)));
                float materialScale = GameMaterialComposition.DebrisImpulseScale(
                    chunk.Materials[firstVisibleSource]);
                float massScale = math.clamp(math.rsqrt(math.max(1f, representedSourceVoxels / 8f)),
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
                // Structural debris should read as collapse immediately. A strong upward kick
                // made towers hover for half a second before gravity became visible.
                velocity.y = math.lerp(-1.4f, 0.15f, Unit(Hash(hash + 113u)));
                angular *= math.lerp(0.65f, 1f, impulseScale);
                float ground = world.FindLandingCentreY(pivot, collisionRadius);
                float fallDistance = math.max(0f, pivot.y - ground);
                float settleLifetime = math.clamp(math.sqrt(2f * fallDistance / 9.81f) + 0.7f,
                                                  materialScale < 0.7f ? 0.85f : 1.15f, 3.5f);

                _states[slot] = new GpuState
                {
                    PositionAge = new Vector4(pivot.x, pivot.y, pivot.z, 0f),
                    Rotation = new Vector4(0f, 0f, 0f, 1f),
                    VelocitySettled = new Vector4(velocity.x, velocity.y, velocity.z, 0f),
                    AngularGround = new Vector4(angular.x, angular.y, angular.z, ground),
                    ContactActive = new Vector4(0f, 1f, settleLifetime, 0f),
                };
                _records[slot] = new ChunkRecord
                {
                    Active = true,
                    VoxelCount = representedSourceVoxels,
                    ExpireAt = Time.unscaledTime + settleLifetime,
                };
                _highestActiveSlot = math.max(_highestActiveSlot, slot);
                ActiveChunks++;
                ActiveVoxels += representedSourceVoxels;
                minSlot = math.min(minSlot, slot);
                maxSlot = math.max(maxSlot, slot);
                submitted++;
            }

            if (maxSlot < minSlot) return;
            int stateCount = maxSlot - minSlot + 1;
            _stateBuffer.SetData(_states, minSlot, minSlot, stateCount);
            int firstInstance = minSlot * RenderInstancesPerChunk;
            int instanceCount = stateCount * RenderInstancesPerChunk;
            _instanceBuffer.SetData(_instances, firstInstance, firstInstance, instanceCount);
            UpdateDrawArguments();
        }

        private void ExpireVisuals(float now)
        {
            int minSlot = MaxChunks;
            int maxSlot = -1;
            for (int slot = 0; slot < MaxChunks; slot++)
            {
                ChunkRecord record = _records[slot];
                if (!record.Active || record.ExpireAt > now) continue;

                ActiveChunks--;
                ActiveVoxels -= record.VoxelCount;
                _records[slot] = default;
                _states[slot] = default;
                minSlot = math.min(minSlot, slot);
                maxSlot = math.max(maxSlot, slot);
            }

            if (maxSlot >= minSlot)
            {
                _stateBuffer.SetData(_states, minSlot, minSlot, maxSlot - minSlot + 1);
                while (_highestActiveSlot >= 0 && !_records[_highestActiveSlot].Active)
                    _highestActiveSlot--;
                UpdateDrawArguments();
            }
        }

        private void UpdateDrawArguments()
        {
            _drawArguments[0] = _cube.GetIndexCount(0);
            _drawArguments[1] = (uint)((_highestActiveSlot + 1) * RenderInstancesPerChunk);
            _drawArguments[2] = _cube.GetIndexStart(0);
            _drawArguments[3] = (uint)_cube.GetBaseVertex(0);
            _drawArguments[4] = 0;
            _argumentsBuffer.SetData(_drawArguments);
        }

        private int FindFreeSlot()
        {
            for (int i = 0; i < _records.Length; i++)
                if (!_records[i].Active) return i;
            return -1;
        }

        private static int FindVisibleSourceIndex(ShowcaseWorld.DetachedVoxelChunk chunk,
                                                  int ordinal, int visibleCount,
                                                  int sourceCount)
        {
            if (sourceCount <= 1) return 0;
            return math.min(sourceCount - 1,
                            ordinal * sourceCount / math.max(1, visibleCount));
        }

        private static byte CoatingAt(ShowcaseWorld.DetachedVoxelChunk chunk, int index) =>
            chunk.Coatings != null && index < chunk.Coatings.Length
                ? chunk.Coatings[index]
                : Coatings.None;

        private static Vector4 MaterialColour(byte material, byte coating, float scale)
        {
            float4 baseColour = GameMaterialComposition.DebrisColour(material, scale);
            Vector4 colour = new(baseColour.x, baseColour.y, baseColour.z, baseColour.w);

            Vector3 overlay = coating switch
            {
                Coatings.Moss => new Vector3(0.17f, 0.38f, 0.11f),
                Coatings.Snow => new Vector3(0.88f, 0.91f, 0.94f),
                Coatings.Soot => new Vector3(0.08f, 0.07f, 0.06f),
                Coatings.Wet => new Vector3(0.12f, 0.20f, 0.23f),
                _ => new Vector3(colour.x, colour.y, colour.z),
            };
            float blend = coating == Coatings.None ? 0f : coating == Coatings.Wet ? 0.3f : 0.62f;
            colour.x = math.lerp(colour.x, overlay.x, blend);
            colour.y = math.lerp(colour.y, overlay.y, blend);
            colour.z = math.lerp(colour.z, overlay.z, blend);
            return colour;
        }

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
            _argumentsBuffer?.Dispose();
            _instanceBuffer?.Dispose();
            _stateBuffer?.Dispose();
            if (_material != null) UnityEngine.Object.Destroy(_material);
            if (_cube != null) UnityEngine.Object.Destroy(_cube);
        }
    }
}
