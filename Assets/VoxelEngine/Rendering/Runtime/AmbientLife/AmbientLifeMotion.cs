using UnityEngine;
using VoxelEngine.AmbientLife.Api;

namespace VoxelEngine.Rendering.Runtime.AmbientLife
{
    /// <summary>
    /// Pure deterministic local presentation motion for reconstructed ambient agents. The networked
    /// state remains the compact AmbientLifeCluster; seed + local time recreate motion on each client.
    /// </summary>
    internal static class AmbientLifeMotion
    {
        public static Vector3 EvaluatePosition(
            AmbientMovementForm form,
            Vector3 basePosition,
            Vector3 clusterCentre,
            float clusterRadius,
            uint agentSeed,
            uint clusterSeed,
            int agentIndex,
            float timeSeconds)
        {
            float radius = Mathf.Max(0.05f, clusterRadius);
            float phase = Hash01(agentSeed ^ 0xB5297A4Du) * Mathf.PI * 2f;
            Vector3 position;

            switch (form)
            {
                case AmbientMovementForm.Flutter:
                    position = Flutter(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.Dart:
                    position = Dart(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.Drift:
                    position = Drift(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.GroundScuttle:
                    position = GroundScuttle(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.Hop:
                    position = Hop(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.Flock:
                    position = Flock(basePosition, radius, phase, clusterSeed, agentIndex, timeSeconds);
                    break;
                case AmbientMovementForm.Orbit:
                    position = Orbit(basePosition, clusterCentre, radius, phase, agentSeed, timeSeconds);
                    break;
                case AmbientMovementForm.HoverSwarm:
                default:
                    position = HoverSwarm(basePosition, radius, phase, agentSeed, timeSeconds);
                    break;
            }

            return ClampHorizontal(position, clusterCentre, radius * 1.04f);
        }

        private static Vector3 HoverSwarm(Vector3 b, float r, float phase, uint seed, float t)
        {
            float a = t * Mathf.Lerp(0.75f, 1.35f, Hash01(seed ^ 0x68BC21EBu)) + phase;
            float amp = r * 0.14f;
            return b + new Vector3(
                Mathf.Sin(a) * amp + Mathf.Sin(a * 0.47f + 1.1f) * amp * 0.35f,
                Mathf.Sin(a * 0.73f + 0.8f) * Mathf.Min(0.24f, r * 0.11f),
                Mathf.Cos(a * 0.81f) * amp + Mathf.Cos(a * 0.39f + 2.0f) * amp * 0.30f);
        }

        private static Vector3 Flutter(Vector3 b, float r, float phase, uint seed, float t)
        {
            float speed = Mathf.Lerp(1.35f, 2.25f, Hash01(seed ^ 0xC2B2AE35u));
            float a = t * speed + phase;
            float amp = r * 0.18f;
            return b + new Vector3(
                Mathf.Sin(a) * amp + Mathf.Sin(a * 2.31f) * amp * 0.20f,
                (0.5f + 0.5f * Mathf.Sin(a * 2.7f + 0.4f)) * Mathf.Min(0.28f, r * 0.13f),
                Mathf.Cos(a * 0.67f + 0.6f) * amp * 0.72f);
        }

        private static Vector3 Dart(Vector3 b, float r, float phase, uint seed, float t)
        {
            float rate = Mathf.Lerp(0.72f, 1.05f, Hash01(seed ^ 0x9E3779B9u));
            float u = Mathf.Max(0f, t) * rate + phase / (Mathf.PI * 2f);
            int segment = Mathf.FloorToInt(u);
            float f = u - segment;
            Vector3 from = DartTarget(seed, segment, r);
            Vector3 to = DartTarget(seed, segment + 1, r);
            float eased = f * f * (3f - 2f * f);
            Vector3 p = b + Vector3.LerpUnclamped(from, to, eased);
            p.y += Mathf.Sin(f * Mathf.PI) * Mathf.Min(0.16f, r * 0.07f);
            return p;
        }

        private static Vector3 Drift(Vector3 b, float r, float phase, uint seed, float t)
        {
            float a = t * Mathf.Lerp(0.22f, 0.42f, Hash01(seed ^ 0x7FEB352Du)) + phase;
            float amp = r * 0.20f;
            return b + new Vector3(
                Mathf.Sin(a) * amp,
                Mathf.Sin(a * 0.53f + 1.2f) * Mathf.Min(0.30f, r * 0.12f),
                Mathf.Cos(a * 0.71f + 0.5f) * amp * 0.78f);
        }

        private static Vector3 GroundScuttle(Vector3 b, float r, float phase, uint seed, float t)
        {
            float a = t * Mathf.Lerp(1.6f, 2.8f, Hash01(seed ^ 0x846CA68Bu)) + phase;
            float amp = r * 0.13f;
            return new Vector3(
                b.x + Mathf.Sin(a) * amp + Mathf.Sin(a * 0.41f) * amp * 0.25f,
                b.y,
                b.z + Mathf.Cos(a * 0.77f + 0.9f) * amp * 0.82f);
        }

        private static Vector3 Hop(Vector3 b, float r, float phase, uint seed, float t)
        {
            float period = Mathf.Lerp(1.45f, 2.25f, Hash01(seed ^ 0x27D4EB2Du));
            float u = Mathf.Max(0f, t + phase * 0.17f) / period;
            int cycle = Mathf.FloorToInt(u);
            float f = u - cycle;
            const float activeFraction = 0.58f;
            Vector3 from = HopTarget(seed, cycle, r);
            Vector3 to = HopTarget(seed, cycle + 1, r);

            if (f >= activeFraction)
                return b + to;

            float p = Mathf.Clamp01(f / activeFraction);
            float eased = p * p * (3f - 2f * p);
            Vector3 pos = b + Vector3.LerpUnclamped(from, to, eased);
            pos.y = b.y + Mathf.Sin(p * Mathf.PI) * Mathf.Lerp(0.20f, 0.42f, Hash01(seed ^ 0xA511E9B3u));
            return pos;
        }

        private static Vector3 Flock(
            Vector3 b, float r, float phase, uint clusterSeed, int agentIndex, float t)
        {
            float sharedPhase = Hash01(clusterSeed ^ 0x63D83595u) * Mathf.PI * 2f;
            float a = t * Mathf.Lerp(0.38f, 0.62f, Hash01(clusterSeed ^ 0xD1B54A35u)) + sharedPhase;
            Vector3 shared = new Vector3(
                Mathf.Sin(a) * r * 0.16f,
                Mathf.Sin(a * 0.61f + 0.7f) * Mathf.Min(0.26f, r * 0.10f),
                Mathf.Cos(a * 0.83f) * r * 0.13f);
            float localPhase = phase + agentIndex * 0.73f;
            Vector3 local = new Vector3(
                Mathf.Sin(t * 1.4f + localPhase) * r * 0.025f,
                Mathf.Sin(t * 1.9f + localPhase) * 0.035f,
                Mathf.Cos(t * 1.2f + localPhase) * r * 0.025f);
            return b + shared + local;
        }

        private static Vector3 Orbit(Vector3 b, Vector3 centre, float r, float phase, uint seed, float t)
        {
            Vector3 radial = b - centre;
            radial.y = 0f;
            float distance = radial.magnitude;
            if (distance < 0.08f)
            {
                distance = r * 0.58f;
                radial = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase)) * distance;
            }

            float angular = t * Mathf.Lerp(0.65f, 1.05f, Hash01(seed ^ 0x6C8E9CF5u));
            float angle = Mathf.Atan2(radial.z, radial.x) + angular;
            Vector3 p = centre + new Vector3(Mathf.Cos(angle) * distance, b.y - centre.y, Mathf.Sin(angle) * distance);
            p.y += Mathf.Sin(angular * 2.1f + phase) * Mathf.Min(0.16f, r * 0.07f);
            return p;
        }

        private static Vector3 DartTarget(uint seed, int segment, float radius)
        {
            uint s = Mix(seed, unchecked((uint)segment));
            float angle = Hash01(s) * Mathf.PI * 2f;
            float radial = Mathf.Lerp(0.04f, radius * 0.28f, Hash01(s ^ 0x85EBCA6Bu));
            float y = (Hash01(s ^ 0xC13FA9A9u) - 0.5f) * Mathf.Min(0.38f, radius * 0.15f);
            return new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
        }

        private static Vector3 HopTarget(uint seed, int cycle, float radius)
        {
            uint s = Mix(seed ^ 0xA511E9B3u, unchecked((uint)cycle));
            float angle = Hash01(s) * Mathf.PI * 2f;
            float radial = Mathf.Lerp(radius * 0.03f, radius * 0.16f, Hash01(s ^ 0x63D83595u));
            return new Vector3(Mathf.Cos(angle) * radial, 0f, Mathf.Sin(angle) * radial);
        }

        private static Vector3 ClampHorizontal(Vector3 position, Vector3 centre, float maxRadius)
        {
            Vector2 delta = new Vector2(position.x - centre.x, position.z - centre.z);
            float magnitude = delta.magnitude;
            if (magnitude <= maxRadius || magnitude < 0.0001f) return position;
            Vector2 clamped = delta * (maxRadius / magnitude);
            position.x = centre.x + clamped.x;
            position.z = centre.z + clamped.y;
            return position;
        }

        private static uint Mix(uint a, uint b)
        {
            uint x = a + b * 0x9E3779B9u + 0x85EBCA6Bu;
            x ^= x >> 16;
            x *= 0x7FEB352Du;
            x ^= x >> 15;
            x *= 0x846CA68Bu;
            x ^= x >> 16;
            return x;
        }

        private static float Hash01(uint seed)
        {
            uint x = Mix(seed == 0u ? 0x9E3779B9u : seed, 0x68BC21EBu);
            return (x & 0x00FFFFFFu) / 16777215f;
        }
    }
}
