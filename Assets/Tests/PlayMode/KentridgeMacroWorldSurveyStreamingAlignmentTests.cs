using System;
using System.Reflection;
using Game.Kentridge.PlayableSlice;
using MountingForce.WorldGen;
using NUnit.Framework;
using UnityEngine;
using VoxelEngine.Showcase;
using TerrainSampler = VoxelEngine.Terrain.Api.TerrainQuery;

namespace VoxelEngine.Tests.PlayMode
{
    public sealed class KentridgeMacroWorldSurveyStreamingAlignmentTests
    {
        private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        private const uint Seed = 0x4B454E54u;
        private const float DmToMetres = 0.1f;

        [Test]
        public void ElevatedSurveyPinsStreamingDemandToRenderedCameraBeforeSliceStreaming()
        {
            Type driverType = typeof(KentridgePlayableSlice).Assembly.GetType(
                "Game.Kentridge.PlayableSlice.KentridgeMacroWorldEvidenceDriver",
                throwOnError: true);
            DefaultExecutionOrder executionOrder =
                driverType.GetCustomAttribute<DefaultExecutionOrder>();
            MethodInfo resolveCamera = driverType.GetMethod(
                "ResolveSurveyCameraPosition",
                StaticPrivate);
            MethodInfo resolveMotor = driverType.GetMethod(
                "ResolveSurveyMotorPosition",
                StaticPrivate);

            Assert.That(executionOrder, Is.Not.Null,
                "The evidence driver must explicitly run before the playable slice so its streaming demand is not incidental script ordering.");
            Assert.That(executionOrder.order, Is.LessThan(0));
            Assert.That(resolveCamera, Is.Not.Null);
            Assert.That(resolveMotor, Is.Not.Null);

            var cameraDm = new Int2(2030, 3780);
            const float cameraHeightMetres = 70f;
            const float eyeHeightMetres = 1.7f;
            var camera = (Vector3)resolveCamera.Invoke(
                null,
                new object[] { cameraDm, cameraHeightMetres });
            var motor = (Vector3)resolveMotor.Invoke(
                null,
                new object[] { cameraDm, cameraHeightMetres, eyeHeightMetres });
            Vector3 streamedEye = motor + Vector3.up * eyeHeightMetres;

            int cameraGround = TerrainSampler.HeightAt(cameraDm.X, cameraDm.Y, Seed);
            var expectedCamera = new Vector3(
                cameraDm.X * DmToMetres,
                cameraGround * DmToMetres + cameraHeightMetres,
                cameraDm.Y * DmToMetres);

            Assert.That((camera - expectedCamera).sqrMagnitude, Is.LessThan(0.000001f));
            Assert.That((streamedEye - camera).sqrMagnitude, Is.LessThan(0.000001f),
                "CharacterMotor.EyePosition is the ShowcaseWorld streaming authority and must coincide with the elevated survey camera.");
            Assert.That(ShowcaseWorld.RegionAt(streamedEye), Is.EqualTo(ShowcaseWorld.RegionAt(camera)),
                "The renderer and streamed Storage demand must occupy the same 3D region during macro surveys.");
        }
    }
}
