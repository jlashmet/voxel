using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.AmbientLife.Api;
using VoxelEngine.Rendering.Runtime.AmbientLife;
using VoxelEngine.Rendering.Runtime.Vegetation;
using VoxelEngine.Vegetation.Api;

namespace VoxelEngine.CI
{
    public sealed class AnimationSemanticQualityTests
    {
        private const float Dt = 1f / 30f;
        private const int Samples = 181;
        private const float Radius = 4f;
        private const uint AgentSeed = 0x1234ABCDu;
        private const uint ClusterSeed = 0xBEEF7711u;
        private static readonly Vector3 Centre = new Vector3(2f, 1f, -3f);
        private static readonly Vector3 Base = Centre + new Vector3(2.1f, 0.8f, 0.7f);

        [Test]
        public void SemanticAnimationQuality_AllMovementFormsAndVegetationPolicies()
        {
            var trajectories = new Dictionary<AmbientMovementForm, Trajectory>();
            var metrics = new StringBuilder("movement,path,avg_speed,median_speed,max_speed,max_step,max_accel,y_range,radial_std,turns,stationary_ratio\n");
            var samples = new StringBuilder("movement,time,x,y,z,speed\n");

            foreach (AmbientMovementForm form in Enum.GetValues(typeof(AmbientMovementForm)))
            {
                Trajectory t = Sample(form);
                trajectories.Add(form, t);
                metrics.AppendLine($"{form},{t.Path:0.0000},{t.AverageSpeed:0.0000},{t.MedianSpeed:0.0000},{t.MaxSpeed:0.0000},{t.MaxStep:0.0000},{t.MaxAcceleration:0.0000},{t.YRange:0.0000},{t.RadialStdDev:0.0000},{t.Turns},{t.StationaryRatio:0.0000}");
                for (int i = 0; i < t.Positions.Length; i++)
                {
                    Vector3 p = t.Positions[i];
                    float speed = i == 0 ? 0f : t.Speeds[i - 1];
                    samples.AppendLine($"{form},{i * Dt:0.000},{p.x:0.0000},{p.y:0.0000},{p.z:0.0000},{speed:0.0000}");
                }
            }

            File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("ambient_trajectory_metrics.csv"), metrics.ToString());
            File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("ambient_trajectory_samples.csv"), samples.ToString());

            foreach (KeyValuePair<AmbientMovementForm, Trajectory> pair in trajectories)
            {
                AmbientMovementForm form = pair.Key;
                Trajectory t = pair.Value;
                Vector3 first = AmbientLifeMotion.EvaluatePosition(form, Base, Centre, Radius, AgentSeed, ClusterSeed, 2, 2.37f);
                Vector3 repeated = AmbientLifeMotion.EvaluatePosition(form, Base, Centre, Radius, AgentSeed, ClusterSeed, 2, 2.37f);
                Assert.That(Vector3.Distance(first, repeated), Is.LessThan(0.000001f), $"{form} is not deterministic.");
                Assert.That(t.MaxStep, Is.LessThan(0.12f), $"{form} has a teleport-sized one-frame step.");
                Assert.That(t.Path, Is.GreaterThan(0.20f), $"{form} barely moves across the six-second sample.");
                Assert.That(t.MaxRadius, Is.LessThanOrEqualTo(Radius * 1.041f), $"{form} escaped its cluster radius.");
                AssertSignature(form, t);
            }

            ValidateFlockCoherence();
            ValidateVegetationPolicies();
        }

        private static void AssertSignature(AmbientMovementForm form, Trajectory t)
        {
            switch (form)
            {
                case AmbientMovementForm.HoverSwarm:
                    Assert.That(t.AverageSpeed, Is.InRange(0.25f, 1.30f));
                    Assert.That(t.YRange, Is.GreaterThan(0.20f));
                    break;
                case AmbientMovementForm.Flutter:
                    Assert.That(t.AverageSpeed, Is.GreaterThan(0.60f));
                    Assert.That(t.YRange, Is.GreaterThan(0.16f));
                    Assert.That(t.Turns, Is.GreaterThanOrEqualTo(4), "Flutter lacks repeated direction changes.");
                    break;
                case AmbientMovementForm.Dart:
                    Assert.That(t.MaxSpeed, Is.GreaterThan(t.MedianSpeed * 2f), "Dart lacks a fast/slow burst signature.");
                    Assert.That(t.Turns, Is.GreaterThanOrEqualTo(5));
                    Assert.That(t.MaxStep, Is.LessThan(0.08f));
                    break;
                case AmbientMovementForm.Drift:
                    Assert.That(t.MaxSpeed, Is.LessThan(0.50f));
                    Assert.That(t.MaxAcceleration, Is.LessThan(0.25f));
                    Assert.That(t.YRange, Is.GreaterThan(0.08f));
                    break;
                case AmbientMovementForm.GroundScuttle:
                    Assert.That(t.YRange, Is.LessThan(0.001f), "GroundScuttle left the ground plane.");
                    Assert.That(t.Path, Is.GreaterThan(0.80f));
                    break;
                case AmbientMovementForm.Hop:
                    ValidateHop(t);
                    break;
                case AmbientMovementForm.Flock:
                    Assert.That(t.MaxAcceleration, Is.LessThan(0.90f));
                    break;
                case AmbientMovementForm.Orbit:
                    Assert.That(t.RadialStdDev, Is.LessThan(0.025f));
                    Assert.That(t.Path, Is.GreaterThan(5f));
                    Assert.That(t.YRange, Is.GreaterThan(0.10f));
                    break;
            }
        }

        private static void ValidateHop(Trajectory t)
        {
            int grounded = 0, airborne = 0, apexes = 0;
            for (int i = 0; i < t.Positions.Length; i++)
            {
                float height = t.Positions[i].y - Base.y;
                Assert.That(height, Is.GreaterThanOrEqualTo(-0.001f), "Hop penetrated below ground.");
                if (Mathf.Abs(height) < 0.003f) grounded++;
                if (height > 0.08f) airborne++;
            }
            for (int i = 2; i < t.Positions.Length; i++)
            {
                float previousVy = (t.Positions[i - 1].y - t.Positions[i - 2].y) / Dt;
                float currentVy = (t.Positions[i].y - t.Positions[i - 1].y) / Dt;
                if (previousVy > 0.02f && currentVy <= 0.02f) apexes++;
            }
            Assert.That(grounded / (float)t.Positions.Length, Is.GreaterThan(0.20f), "Hop has no readable grounded dwell.");
            Assert.That(airborne / (float)t.Positions.Length, Is.GreaterThan(0.20f));
            Assert.That(apexes, Is.GreaterThanOrEqualTo(2));
            Assert.That(t.YRange, Is.GreaterThan(0.18f));
        }

        private static Trajectory Sample(AmbientMovementForm form)
        {
            var positions = new Vector3[Samples];
            for (int i = 0; i < Samples; i++)
                positions[i] = AmbientLifeMotion.EvaluatePosition(form, Base, Centre, Radius, AgentSeed, ClusterSeed, 2, i * Dt);
            return new Trajectory(positions, Centre);
        }

        private static void ValidateFlockCoherence()
        {
            const int agentCount = 8;
            const int sampleCount = 91;
            var basePositions = new Vector3[agentCount];
            var seeds = new uint[agentCount];
            var firstRelative = new Vector3[agentCount];
            Vector3 firstCentroid = Vector3.zero;
            float maxCentroidTravel = 0f, maxDistortion = 0f, minPairwise = float.MaxValue;

            for (int i = 0; i < agentCount; i++)
            {
                float angle = i * Mathf.PI * 2f / agentCount;
                basePositions[i] = new Vector3(Mathf.Cos(angle) * 2.2f, 1.8f, Mathf.Sin(angle) * 2.2f);
                seeds[i] = unchecked(AgentSeed + (uint)i * 0x9E3779B9u);
            }

            for (int sample = 0; sample < sampleCount; sample++)
            {
                float time = sample * 6f / (sampleCount - 1);
                var positions = new Vector3[agentCount];
                Vector3 centroid = Vector3.zero;
                for (int i = 0; i < agentCount; i++)
                {
                    positions[i] = AmbientLifeMotion.EvaluatePosition(AmbientMovementForm.Flock, basePositions[i], Vector3.up, Radius, seeds[i], ClusterSeed, i, time);
                    centroid += positions[i];
                }
                centroid /= agentCount;
                if (sample == 0)
                {
                    firstCentroid = centroid;
                    for (int i = 0; i < agentCount; i++) firstRelative[i] = positions[i] - centroid;
                }
                maxCentroidTravel = Mathf.Max(maxCentroidTravel, Vector3.Distance(centroid, firstCentroid));
                float distortion = 0f;
                for (int i = 0; i < agentCount; i++)
                {
                    distortion += Vector3.Distance(positions[i] - centroid, firstRelative[i]);
                    for (int j = i + 1; j < agentCount; j++) minPairwise = Mathf.Min(minPairwise, Vector3.Distance(positions[i], positions[j]));
                }
                maxDistortion = Mathf.Max(maxDistortion, distortion / agentCount);
            }

            File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("flock_coherence.txt"), $"max_centroid_travel={maxCentroidTravel:0.0000}\nmax_relative_distortion={maxDistortion:0.0000}\nmin_pairwise_distance={minPairwise:0.0000}\n");
            Assert.That(maxCentroidTravel, Is.GreaterThan(0.65f));
            Assert.That(maxDistortion, Is.LessThan(0.35f));
            Assert.That(minPairwise, Is.GreaterThan(0.85f));
        }

        private static void ValidateVegetationPolicies()
        {
            var csv = new StringBuilder("kind,growth_form,shader_class,wind_strength,emission_strength,animation_policy\n");
            var seen = new bool[4];
            for (int i = 0; i < VegetationCatalogue.Count; i++)
            {
                VegetationKind kind = VegetationCatalogue.KindAt(i);
                VegetationProfile profile = VegetationCatalogue.Get(kind);
                VegetationRenderStyle style = ProceduralVegetationMaterials.StyleFor(kind);
                seen[(int)style.ShaderClass] = true;
                string policy;
                if (style.ShaderClass == VegetationShaderClass.Foliage || style.ShaderClass == VegetationShaderClass.Vine)
                {
                    policy = "wind-geometry";
                    Assert.That(style.WindStrength, Is.GreaterThan(0.04f), $"{kind} has no effective wind motion.");
                }
                else policy = style.EmissionStrength > 0f ? "emission-only" : "static";
                if ((profile.Traits & VegetationTraits.Luminous) != 0) Assert.That(style.EmissionStrength, Is.GreaterThan(0f));
                csv.AppendLine($"{kind},{profile.GrowthForm},{style.ShaderClass},{style.WindStrength:0.000},{style.EmissionStrength:0.000},{policy}");
            }
            File.WriteAllText(VegetationLifeRenderingVisualTests.ArtifactPath("vegetation_animation_policy.csv"), csv.ToString());
            for (int i = 0; i < seen.Length; i++) Assert.That(seen[i], Is.True, $"No catalogue coverage for {(VegetationShaderClass)i}.");
        }

        private sealed class Trajectory
        {
            public readonly Vector3[] Positions;
            public readonly float[] Speeds;
            public readonly float Path, AverageSpeed, MedianSpeed, MaxSpeed, MaxStep, MaxAcceleration, YRange, RadialStdDev, StationaryRatio, MaxRadius;
            public readonly int Turns;

            public Trajectory(Vector3[] positions, Vector3 centre)
            {
                Positions = positions;
                Speeds = new float[positions.Length - 1];
                var velocities = new Vector3[Speeds.Length];
                float path = 0f, maxStep = 0f, maxSpeed = 0f, maxAcceleration = 0f;
                float minY = float.MaxValue, maxY = float.MinValue, radialSum = 0f, radialSq = 0f, maxRadius = 0f;
                int turns = 0, stationary = 0;
                for (int i = 0; i < positions.Length; i++)
                {
                    minY = Mathf.Min(minY, positions[i].y); maxY = Mathf.Max(maxY, positions[i].y);
                    float radial = Vector2.Distance(new Vector2(positions[i].x, positions[i].z), new Vector2(centre.x, centre.z));
                    radialSum += radial; radialSq += radial * radial; maxRadius = Mathf.Max(maxRadius, radial);
                    if (i == 0) continue;
                    float step = Vector3.Distance(positions[i], positions[i - 1]); path += step; maxStep = Mathf.Max(maxStep, step);
                    velocities[i - 1] = (positions[i] - positions[i - 1]) / Dt; Speeds[i - 1] = velocities[i - 1].magnitude;
                    maxSpeed = Mathf.Max(maxSpeed, Speeds[i - 1]); if (Speeds[i - 1] < 0.03f) stationary++;
                }
                for (int i = 1; i < velocities.Length; i++)
                {
                    maxAcceleration = Mathf.Max(maxAcceleration, ((velocities[i] - velocities[i - 1]) / Dt).magnitude);
                    if (velocities[i].sqrMagnitude > 0.0001f && velocities[i - 1].sqrMagnitude > 0.0001f && Vector3.Dot(velocities[i].normalized, velocities[i - 1].normalized) < 0.98f) turns++;
                }
                float[] sorted = (float[])Speeds.Clone(); Array.Sort(sorted);
                float meanRadial = radialSum / positions.Length;
                Path = path; AverageSpeed = path / 6f; MedianSpeed = sorted[sorted.Length / 2]; MaxSpeed = maxSpeed; MaxStep = maxStep; MaxAcceleration = maxAcceleration;
                YRange = maxY - minY; RadialStdDev = Mathf.Sqrt(Mathf.Max(0f, radialSq / positions.Length - meanRadial * meanRadial)); Turns = turns;
                StationaryRatio = stationary / (float)Speeds.Length; MaxRadius = maxRadius;
            }
        }
    }
}
