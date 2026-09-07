using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using VoxelEngine.Rendering.Runtime.SurfaceExtraction;

namespace VoxelEngine.Rendering.Runtime.GpuVoxel
{
    /// <summary>Bounded transport of readiness and spatial relationships, never draw selection.</summary>
    internal sealed class GpuSurfaceLodInputs
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Node
        {
            internal int Parent, Step, Handle;
            internal uint Flags; // drawable=1, current-view-complete=2
            internal int C0, C1, C2, C3, C4, C5, C6, C7;
            internal Vector4 BoundsVoxel; // xyz centre, w padded half edge
        }

        private readonly Dictionary<SurfaceLodNodeKey, int> _indices = new();
        private readonly List<SurfaceLodNodeKey> _keys = new();
        private readonly List<SurfaceLodNodeKey> _previousDrawable = new();
        private readonly List<SurfaceLodNodeKey> _previousComplete = new();
        private readonly List<int> _previousHandles = new();
        private readonly List<SurfaceLodNodeKey> _previousOwned = new();
        internal readonly Node[] Nodes;
        internal int Count => _keys.Count;
        internal SurfaceLodNodeKey KeyAt(int index) => _keys[index];
        internal uint Version { get; private set; }
        internal uint TopologyBuildCount { get; private set; }

        internal GpuSurfaceLodInputs(int capacity) { Nodes = new Node[capacity]; }

        internal void Update(IReadOnlyList<SurfaceLodNodeKey> drawable, IReadOnlyList<int> handles,
                             IReadOnlyList<SurfaceLodNodeKey> complete, IReadOnlyList<SurfaceLodNodeKey> owned = null)
        {
            owned ??= Array.Empty<SurfaceLodNodeKey>();
            if (drawable.Count != handles.Count) throw new ArgumentException("LOD handle/key mismatch.");
            if (Version != 0 && Matches(drawable, _previousDrawable)
                && Matches(complete, _previousComplete) && Matches(handles, _previousHandles)
                && Matches(owned, _previousOwned)) return;
            bool rebuildTopology = Version == 0 || owned.Count == 0
                || !Matches(owned, _previousOwned) || !ContainsAll(drawable) || !ContainsAll(complete);
            if (rebuildTopology)
            {
                _indices.Clear();
                _keys.Clear();
                TopologyBuildCount++;
            }
            else
            {
                // Membership is unchanged. Readiness/handle updates do not change any edge,
                // so keep parent/child indices and clear only last frame's presentation state.
                for (int i = 0; i < Count; i++)
                {
                    Node n = Nodes[i]; n.Flags &= 4u; n.Handle = -1; Nodes[i] = n;
                }
            }
            for (int i = 0; i < drawable.Count; i++)
            {
                int index = AddAncestors(drawable[i]);
                Node n = Nodes[index]; n.Flags |= 1u; n.Handle = handles[i]; Nodes[index] = n;
            }
            for (int i = 0; i < complete.Count; i++)
            {
                int index = AddAncestors(complete[i]);
                Node n = Nodes[index]; n.Flags |= 2u; Nodes[index] = n;
            }
            if (rebuildTopology)
            for (int i = 0; i < owned.Count; i++)
            {
                int index = AddAncestors(owned[i]);
                Node n = Nodes[index]; n.Flags |= 4u; Nodes[index] = n;
            }
            if (rebuildTopology)
            for (int i = 0; i < Count; i++)
            {
                SurfaceLodNodeKey key = _keys[i];
                Node n = Nodes[i];
                if (key.SourceStep < 8)
                    n.Parent = _indices[new SurfaceLodNodeKey(key.SourceStep * 2,
                        SurfaceLodHierarchy.ParentCoordinate(key.Coordinate))];
                if (key.SourceStep > 1)
                {
                    n.C0 = Child(key, 0); n.C1 = Child(key, 1);
                    n.C2 = Child(key, 2); n.C3 = Child(key, 3);
                    n.C4 = Child(key, 4); n.C5 = Child(key, 5);
                    n.C6 = Child(key, 6); n.C7 = Child(key, 7);
                }
                Nodes[i] = n;
            }
            Copy(drawable, _previousDrawable); Copy(complete, _previousComplete);
            Copy(handles, _previousHandles); Copy(owned, _previousOwned);
            Version++;
            if (Version == 0) Version = 1;
        }

        private bool ContainsAll(IReadOnlyList<SurfaceLodNodeKey> keys)
        {
            for (int i = 0; i < keys.Count; i++)
                if (!_indices.ContainsKey(keys[i])) return false;
            return true;
        }

        private int AddAncestors(SurfaceLodNodeKey key)
        {
            if (_indices.TryGetValue(key, out int existing)) return existing;
            if (Count == Nodes.Length) throw new InvalidOperationException("GPU LOD input capacity exhausted.");
            int index = Count;
            _indices.Add(key, index); _keys.Add(key);
            Nodes[index] = new Node { Parent = -1, Step = key.SourceStep, Handle = -1,
                C0 = -1, C1 = -1, C2 = -1, C3 = -1, C4 = -1, C5 = -1, C6 = -1, C7 = -1,
                BoundsVoxel = new Vector4((key.Coordinate.x + 0.5f) * 64 * key.SourceStep,
                    (key.Coordinate.y + 0.5f) * 64 * key.SourceStep,
                    (key.Coordinate.z + 0.5f) * 64 * key.SourceStep, 33 * key.SourceStep) };
            if (key.SourceStep < 8) AddAncestors(new SurfaceLodNodeKey(key.SourceStep * 2,
                SurfaceLodHierarchy.ParentCoordinate(key.Coordinate)));
            return index;
        }

        private int Child(SurfaceLodNodeKey key, int ordinal) =>
            _indices.TryGetValue(new SurfaceLodNodeKey(key.SourceStep / 2,
                SurfaceLodHierarchy.ChildCoordinate(key.Coordinate, ordinal)), out int index) ? index : -1;

        private static bool Matches<T>(IReadOnlyList<T> a, List<T> b) where T : IEquatable<T>
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++) if (!a[i].Equals(b[i])) return false;
            return true;
        }
        private static void Copy<T>(IReadOnlyList<T> a, List<T> b)
        {
            b.Clear(); for (int i = 0; i < a.Count; i++) b.Add(a[i]);
        }
    }
}
