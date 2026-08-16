using System.Collections.Generic;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;

namespace VoxelEngine.Rendering.Runtime.AmbientLife
{
    /// <summary>
    /// Reconstructs lightweight local visual agents from deterministic clusters. Static agent
    /// identity/placement is cached once; movement matrices are derived from seed + local time each
    /// draw. Clusters remain the authoritative/network-friendly representation.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProceduralAmbientLifeBatchRenderer : MonoBehaviour
    {
        private const int MaxInstancesPerDraw = 1023;
        private const float GoldenAngleRadians = 2.39996323f;

        private readonly Dictionary<AmbientLifeKind, List<AgentVisual>> _batches =
            new Dictionary<AmbientLifeKind, List<AgentVisual>>();
        private readonly Matrix4x4[] _scratch = new Matrix4x4[MaxInstancesPerDraw];
        private MaterialPropertyBlock _properties;
        private int _agentCount;

        private static Mesh s_BillboardQuad;

        public int AgentCount => _agentCount;

        public void SetClusters(IReadOnlyList<AmbientLifeCluster> clusters)
        {
            Clear();
            if (clusters == null) return;

            for (int clusterIndex = 0; clusterIndex < clusters.Count; clusterIndex++)
            {
                AmbientLifeCluster cluster = clusters[clusterIndex];
                if (cluster.Count == 0) continue;

                if (!_batches.TryGetValue(cluster.Kind, out List<AgentVisual> agents))
                {
                    agents = new List<AgentVisual>();
                    _batches.Add(cluster.Kind, agents);
                }

                AmbientLifeRenderStyle style = ProceduralAmbientLifeMaterials.StyleFor(cluster.Kind);
                AmbientLifeProfile profile = AmbientLifeCatalogue.Get(cluster.Kind);
                bool flying = (profile.Traits & AmbientLifeTraits.Flying) != 0;
                GetShapeScale(style.Shape, out float shapeWidth, out float shapeHeight);

                float radius = Mathf.Max(0.05f, cluster.RadiusMetres);
                float clusterRotation = Random01(cluster.Seed ^ 0xC13FA9A9u) * Mathf.PI * 2f;
                Vector3 clusterCentre = new Vector3(
                    cluster.PositionMetres.x,
                    cluster.PositionMetres.y,
                    cluster.PositionMetres.z);

                for (int i = 0; i < cluster.Count; i++)
                {
                    uint seed = Hash(cluster.Seed, (uint)i + 1u, (uint)clusterIndex + 17u);
                    float sequence = (i + 0.5f) / Mathf.Max(1f, cluster.Count);
                    float angleJitter = (Random01(seed ^ 0x8DA6B343u) - 0.5f) * 0.22f;
                    float angle = clusterRotation + i * GoldenAngleRadians + angleJitter;
                    float radialJitter = Mathf.Lerp(0.96f, 1.04f, Random01(seed ^ 0xA511E9B3u));
                    float radialFraction = Mathf.Lerp(0.50f, 0.98f, Mathf.Sqrt(sequence));
                    float radial = Mathf.Min(radius, radius * radialFraction * radialJitter);

                    float height = 0.035f;
                    if (flying)
                    {
                        float verticalRange = Mathf.Min(2.1f, Mathf.Max(0.35f, radius * 0.34f));
                        float lane = (i + 0.5f) / Mathf.Max(1f, cluster.Count);
                        float heightJitter = (Random01(seed ^ 0x63D83595u) - 0.5f) * 0.10f;
                        height = 0.28f + verticalRange * Mathf.Clamp01(0.08f + lane * 0.84f + heightJitter);
                    }

                    Vector3 basePosition = clusterCentre + new Vector3(
                        Mathf.Cos(angle) * radial,
                        height,
                        Mathf.Sin(angle) * radial);

                    float sizeVariation = Mathf.Lerp(0.86f, 1.12f, Random01(seed ^ 0xB5297A4Du));
                    float widthVariation = Mathf.Lerp(0.94f, 1.06f, Random01(seed ^ 0x6C8E9CF5u));
                    float heightVariation = Mathf.Lerp(0.94f, 1.06f, Random01(seed ^ 0xD1B54A35u));
                    float size = style.SizeMetres * sizeVariation;

                    agents.Add(new AgentVisual(
                        basePosition,
                        clusterCentre,
                        radius,
                        seed,
                        cluster.Seed,
                        i,
                        profile.Movement,
                        new Vector3(
                            size * shapeWidth * widthVariation,
                            size * shapeHeight * heightVariation,
                            1f)));
                    _agentCount++;
                }
            }
        }

        public void Clear()
        {
            foreach (KeyValuePair<AmbientLifeKind, List<AgentVisual>> pair in _batches)
                pair.Value.Clear();
            _agentCount = 0;
        }

        private void LateUpdate()
        {
            DrawNow();
        }

        public void DrawNow()
        {
            DrawAtTime(Time.time);
        }

        /// <summary>
        /// Deterministic draw entry point used by visual tests and capture tooling. The timestamp
        /// drives both reconstructed locomotion and shader-side flutter/emission so a capture is
        /// fully reproducible.
        /// </summary>
        public void DrawAtTime(float timeSeconds)
        {
            if (_agentCount == 0) return;
            Material material = ProceduralAmbientLifeMaterials.Shared;
            if (material == null) return;
            if (_properties == null) _properties = new MaterialPropertyBlock();

            ProceduralAmbientLifeMaterials.ApplyLighting();
            Mesh mesh = BillboardQuad;

            foreach (KeyValuePair<AmbientLifeKind, List<AgentVisual>> pair in _batches)
            {
                List<AgentVisual> agents = pair.Value;
                if (agents.Count == 0) continue;

                _properties.Clear();
                ProceduralAmbientLifeMaterials.Configure(_properties, pair.Key);
                _properties.SetFloat("_AnimationTime", timeSeconds);
                for (int start = 0; start < agents.Count; start += MaxInstancesPerDraw)
                {
                    int count = Mathf.Min(MaxInstancesPerDraw, agents.Count - start);
                    for (int i = 0; i < count; i++)
                    {
                        AgentVisual agent = agents[start + i];
                        Vector3 position = Evaluate(agent, timeSeconds);
                        _scratch[i] = Matrix4x4.TRS(position, Quaternion.identity, agent.Scale);
                    }

                    Graphics.DrawMeshInstanced(mesh, 0, material, _scratch, count, _properties);
                }
            }
        }

        internal int CopyAgentPositionsAtTime(float timeSeconds, List<Vector3> output)
        {
            output.Clear();
            foreach (KeyValuePair<AmbientLifeKind, List<AgentVisual>> pair in _batches)
            {
                List<AgentVisual> agents = pair.Value;
                for (int i = 0; i < agents.Count; i++)
                    output.Add(Evaluate(agents[i], timeSeconds));
            }
            return output.Count;
        }

        private static Vector3 Evaluate(in AgentVisual agent, float timeSeconds)
        {
            return AmbientLifeMotion.EvaluatePosition(
                agent.Movement,
                agent.BasePosition,
                agent.ClusterCentre,
                agent.ClusterRadius,
                agent.Seed,
                agent.ClusterSeed,
                agent.AgentIndex,
                timeSeconds);
        }

        private readonly struct AgentVisual
        {
            public readonly Vector3 BasePosition;
            public readonly Vector3 ClusterCentre;
            public readonly float ClusterRadius;
            public readonly uint Seed;
            public readonly uint ClusterSeed;
            public readonly int AgentIndex;
            public readonly AmbientMovementForm Movement;
            public readonly Vector3 Scale;

            public AgentVisual(
                Vector3 basePosition,
                Vector3 clusterCentre,
                float clusterRadius,
                uint seed,
                uint clusterSeed,
                int agentIndex,
                AmbientMovementForm movement,
                Vector3 scale)
            {
                BasePosition = basePosition;
                ClusterCentre = clusterCentre;
                ClusterRadius = clusterRadius;
                Seed = seed;
                ClusterSeed = clusterSeed;
                AgentIndex = agentIndex;
                Movement = movement;
                Scale = scale;
            }
        }

        private static void GetShapeScale(AmbientVisualShape shape, out float width, out float height)
        {
            switch (shape)
            {
                case AmbientVisualShape.Butterfly: width = 1.42f; height = 0.90f; return;
                case AmbientVisualShape.CompactInsect: width = 1.28f; height = 0.76f; return;
                case AmbientVisualShape.Dragonfly: width = 1.55f; height = 1.02f; return;
                case AmbientVisualShape.GroundInsect: width = 0.82f; height = 1.08f; return;
                case AmbientVisualShape.Frog: width = 1.30f; height = 0.82f; return;
                case AmbientVisualShape.BirdOrBat: width = 1.55f; height = 0.78f; return;
                case AmbientVisualShape.Spore: width = 0.72f; height = 0.72f; return;
                case AmbientVisualShape.Wisp: width = 0.72f; height = 1.30f; return;
                case AmbientVisualShape.Emberfly: width = 1.18f; height = 0.88f; return;
                case AmbientVisualShape.Mote:
                default: width = 0.72f; height = 0.72f; return;
            }
        }

        private static Mesh BillboardQuad
        {
            get
            {
                if (s_BillboardQuad != null) return s_BillboardQuad;
                Mesh mesh = new Mesh
                {
                    name = "Ambient Life Billboard Quad",
                    hideFlags = HideFlags.DontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
                    },
                    uv = new[]
                    {
                        new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
                    },
                    triangles = new[] { 0,1,2, 0,2,3 },
                };
                mesh.RecalculateBounds();
                s_BillboardQuad = mesh;
                return s_BillboardQuad;
            }
        }

        private static uint Hash(uint a, uint b, uint c)
        {
            uint x = a ^ 0x9E3779B9u;
            x += b * 0x85EBCA6Bu;
            x ^= x >> 16;
            x += c * 0xC2B2AE35u;
            x ^= x >> 13;
            x *= 0x27D4EB2Du;
            x ^= x >> 15;
            return x;
        }

        private static float Random01(uint seed)
        {
            return (Hash(seed, 0x68BC21EBu, 0x02E5BE93u) & 0x00FFFFFFu) / 16777215f;
        }
    }
}
