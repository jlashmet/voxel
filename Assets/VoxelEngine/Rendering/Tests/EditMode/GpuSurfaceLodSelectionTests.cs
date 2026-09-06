using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.GpuVoxel;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Tests.EditMode
{
    public sealed class GpuSurfaceLodSelectionTests
    {
        private ComputeShader _page, _draw;
        private GpuSurfacePageArena _arena;
        private GpuSurfaceDrawDispatcher _dispatcher;

        [SetUp] public void SetUp()
        {
            Assert.That(SystemInfo.supportsComputeShaders, Is.True);
            _page = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfacePageArena"));
            _draw = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("GpuSurfaceDrawCompact"));
            _arena = new GpuSurfacePageArena(_page, GpuSurfacePageArena.VertexPageSize * 4,
                GpuSurfacePageArena.IndexPageSize * 4, 1024);
            _dispatcher = new GpuSurfaceDrawDispatcher(_draw, _arena);
        }
        [TearDown] public void TearDown()
        {
            if (_dispatcher.ActiveIndirectArgs != null)
                _dispatcher.ActiveIndirectArgs.GetData(new uint[128 * 4]);
            _dispatcher.Dispose(); _arena.Dispose();
            UnityEngine.Object.DestroyImmediate(_page); UnityEngine.Object.DestroyImmediate(_draw);
        }

        private HashSet<int> Select(List<SurfaceLodNodeKey> drawable, List<SurfaceLodNodeKey> complete, int frame,
            List<SurfaceLodNodeKey> owned = null, Plane[] planes = null, float voxelSize = 1f)
        {
            var handles = new List<int>();
            var records = new uint[_arena.HandleCapacity * 8];
            for (int i = 0; i < drawable.Count; i++)
            {
                handles.Add(i);
                records[i * 8] = 1; records[i * 8 + 3] = 3;
                records[i * 8 + 4] = 3; records[i * 8 + 7] = 1;
            }
            _arena.LiveChunkGeometry.SetData(records);
            _dispatcher.PrepareLod(drawable, handles, complete, frame, owned, planes, voxelSize);
            var args = new uint[128 * 4];
            _dispatcher.ActiveIndirectArgs.GetData(args);
            int count = 0;
            for (int i = 0; i < 128; i++) count += (int)args[i * 4 + 1];
            var draws = new uint[_arena.HandleCapacity * 4];
            _dispatcher.ActiveDrawMetadata.GetData(draws);
            var selected = new HashSet<int>();
            for (int i = 0; i < count; i++)
                Assert.That(selected.Add((int)draws[i * 4]), Is.True, "Duplicate GPU-selected handle.");
            return selected;
        }

        [Test] public void PartialThenCompleteThenEditedChildrenPreserveAtomicHandoff()
        {
            var parent = new SurfaceLodNodeKey(4, new int3(-2, 1, -3));
            var drawable = new List<SurfaceLodNodeKey> { parent };
            var complete = new List<SurfaceLodNodeKey>();
            for (int i = 0; i < 7; i++)
            {
                var child = new SurfaceLodNodeKey(2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i));
                drawable.Add(child); complete.Add(child);
            }
            CollectionAssert.AreEquivalent(new[] { 0 }, Select(drawable, complete, 0));
            complete.Add(new SurfaceLodNodeKey(2, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, 7)));
            CollectionAssert.AreEquivalent(new[] { 1, 2, 3, 4, 5, 6, 7 }, Select(drawable, complete, 1));
            complete.RemoveAt(0); // editing a child revokes current completion, retaining its old draw
            CollectionAssert.AreEquivalent(new[] { 0 }, Select(drawable, complete, 2));
            drawable.Clear(); complete.Clear();
            Assert.That(Select(drawable, complete, 3), Is.Empty, "Reused frame buffers must clear old selection.");
        }

        [Test] public void ProofOnlyChildrenKeepPhysicalParent()
        {
            var parent = new SurfaceLodNodeKey(8, new int3(-1, -1, -1));
            var complete = new List<SurfaceLodNodeKey>();
            for (int i = 0; i < 8; i++)
                complete.Add(new SurfaceLodNodeKey(4, SurfaceLodHierarchy.ChildCoordinate(parent.Coordinate, i)));
            CollectionAssert.AreEquivalent(new[] { 0 }, Select(new List<SurfaceLodNodeKey> { parent }, complete, 0));
        }

        [Test] public void GpuFrustumMatchesPaddedBoundsAcrossCameraAndProjectionChanges()
        {
            var go = new GameObject("GPU visibility camera fixture");
            try
            {
                var camera = go.AddComponent<Camera>();
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 300f; camera.aspect = 1.7f;
                var owned = new List<SurfaceLodNodeKey> { new(8, new int3(-1, 0, -1)) };
                for (int i = 0; i < owned.Count; i++)
                    if (owned[i].SourceStep > 1)
                        for (int c = 0; c < 8; c++)
                            owned.Add(new SurfaceLodNodeKey(owned[i].SourceStep / 2,
                                SurfaceLodHierarchy.ChildCoordinate(owned[i].Coordinate, c)));
                var complete = new List<SurfaceLodNodeKey>();
                for (int i = 0; i < owned.Count; i++) if (i % 11 != 0) complete.Add(owned[i]);
                const float voxelSize = 0.1f;
                int offCamera = 0, selectedTotal = 0;
                for (int frame = 0; frame < 8; frame++)
                {
                    camera.transform.position = new Vector3(35 - frame * 8, 18, -65);
                    camera.transform.LookAt(new Vector3(-20 + frame * 4, 18, -20));
                    camera.orthographic = frame % 2 == 0;
                    camera.orthographicSize = 12;
                    camera.fieldOfView = 35 + frame * 3;
                    Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
                    var visible = new List<SurfaceLodNodeKey>();
                    var current = new List<SurfaceLodNodeKey>(complete);
                    foreach (var key in owned)
                    {
                        float edge = 64 * key.SourceStep * voxelSize;
                        var bounds = new Bounds((Vector3)((float3)key.Coordinate * edge + edge * 0.5f),
                            Vector3.one * (edge + 2 * key.SourceStep * voxelSize));
                        if (GeometryUtility.TestPlanesAABB(planes, bounds)) visible.Add(key);
                        else { current.Add(key); offCamera++; }
                    }
                    var reference = new SurfaceLodVisibilitySelector();
                    reference.Rebuild(visible, current);
                    var expected = new HashSet<int>();
                    for (int i = 0; i < owned.Count; i++) if (reference.IsActive(owned[i])) expected.Add(i);
                    var selected = Select(owned, complete, frame, owned, planes, voxelSize);
                    selectedTotal += selected.Count;
                    CollectionAssert.AreEquivalent(expected, selected, $"camera frame {frame}");
                }
                Assert.That(offCamera, Is.GreaterThan(0));
                Assert.That(selectedTotal, Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test] public void FourLevelRandomCompletionMatchesIndependentCpuOwnershipModel()
        {
            var random = new System.Random(917);
            var all = new List<SurfaceLodNodeKey>();
            var root = new SurfaceLodNodeKey(8, new int3(-1, 0, -2));
            all.Add(root);
            for (int i = 0; i < all.Count; i++)
                if (all[i].SourceStep > 1)
                    for (int c = 0; c < 8; c++)
                        all.Add(new SurfaceLodNodeKey(all[i].SourceStep / 2,
                            SurfaceLodHierarchy.ChildCoordinate(all[i].Coordinate, c)));
            var reference = new SurfaceLodVisibilitySelector();
            for (int frame = 0; frame < 20; frame++)
            {
                var drawable = new List<SurfaceLodNodeKey>();
                var complete = new List<SurfaceLodNodeKey>();
                foreach (var key in all)
                {
                    if (random.Next(5) != 0) drawable.Add(key);
                    if (random.Next(12) != 0) complete.Add(key);
                }
                reference.Rebuild(drawable, complete);
                var expected = new HashSet<int>();
                for (int i = 0; i < drawable.Count; i++) if (reference.IsActive(drawable[i])) expected.Add(i);
                CollectionAssert.AreEquivalent(expected, Select(drawable, complete, frame), $"frame {frame}");
            }
        }
    }
}
