#!/usr/bin/env python3
"""Apply T3.1/T3.2 active-hierarchy transition masks and reusable seam geometry."""
from pathlib import Path

CACHE = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/CpuTransvoxelChunkCache.cs")
VERTEX = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/SmoothSurfaceVertex.cs")
SCHED = Path("Assets/VoxelEngine/Rendering/Runtime/SurfaceExtraction/VoxelSurfaceScheduler.cs")
SHADER = Path("Assets/VoxelEngine/Rendering/Runtime/Shaders/SmoothSurface.shader")


def replace_once(text: str, old: str, new: str, label: str) -> str:
    if old not in text:
        raise RuntimeError(f"could not find expected source shape for {label}")
    return text.replace(old, new, 1)


def main() -> None:
    vertex = VERTEX.read_text()
    vertex = replace_once(
        vertex,
        """        public uint Active;
        public const int Stride = 32;""",
        """        /// <summary>
        /// Bits 8..15 carry ambient-occlusion strength. Bits 24..31 carry a reusable
        /// Transvoxel transition-face tag: zero for ordinary geometry, face+1 for seams.
        /// The active six-bit face mask is supplied per draw so LOD changes do not remesh.
        /// </summary>
        public uint Active;
        public const int TransitionTagShift = 24;
        public const uint TransitionTagMask = 0xFF000000u;
        public const int Stride = 32;""",
        "vertex transition tag contract",
    )
    VERTEX.write_text(vertex)

    cache = CACHE.read_text()
    cache = replace_once(
        cache,
        '        private static readonly int s_SurfaceVertexBase = Shader.PropertyToID("_SurfaceVertexBase");',
        '        private static readonly int s_SurfaceVertexBase = Shader.PropertyToID("_SurfaceVertexBase");\n        private static readonly int s_SurfaceTransitionMask = Shader.PropertyToID("_SurfaceTransitionMask");',
        "transition-mask shader property",
    )
    cache = replace_once(
        cache,
        """            public int IndexCount;
            public int LastUsedFrame;""",
        """            public int IndexCount;
            public int LastUsedFrame;
            public byte TransitionMask;""",
        "entry transition mask",
    )
    cache = replace_once(
        cache,
        """                IndexCount = 0;
                LastUsedFrame = 0;
                GpuBytes = 0;""",
        """                IndexCount = 0;
                LastUsedFrame = 0;
                TransitionMask = 0;
                GpuBytes = 0;""",
        "entry transition reset",
    )
    cache = replace_once(
        cache,
        """                properties.SetInt(s_SurfaceVertexBase, _liveLease.VertexStart);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);""",
        """                properties.SetInt(s_SurfaceVertexBase, _liveLease.VertexStart);
                properties.SetInt(s_SurfaceIndexBase, _liveLease.IndexStart);
                properties.SetInt(s_SurfaceTransitionMask, TransitionMask);""",
        "draw transition mask",
    )
    cache = replace_once(
        cache,
        """                IndexCount = 0;
                GpuBytes = 0;""",
        """                IndexCount = 0;
                TransitionMask = 0;
                GpuBytes = 0;""",
        "dispose transition reset",
    )

    cache = replace_once(
        cache,
        """        internal void MarkHierarchyActive(int3 coordinate)
        {
            if (_known.Contains(coordinate)) _hierarchyActive.Add(coordinate);
        }

        /// <summary>True when a known node is in its legacy distance shell and camera frustum.""",
        """        internal void MarkHierarchyActive(int3 coordinate)
        {
            if (_known.Contains(coordinate)) _hierarchyActive.Add(coordinate);
        }

        internal void SetHierarchyTransitionMask(int3 coordinate, byte mask)
        {
            if (_entries.TryGetValue(coordinate, out Entry entry) && entry.Ready)
                entry.TransitionMask = (byte)(mask & 0x3Fu);
        }

        /// <summary>True when a known node is in its legacy distance shell and camera frustum.""",
        "cache transition-mask setter",
    )

    cache = replace_once(
        cache,
        "StepTransitionFaces(source, in palette, camera, voxelSize,\n                                             deadline)",
        "StepTransitionFaces(source, in palette, voxelSize, deadline)",
        "phase-4 transition call",
    )
    cache = replace_once(
        cache,
        """        private bool StepTransitionFaces(IRegionReadSource source,
                                         in MaterialPaletteView palette,
                                         Camera camera, float voxelSize,
                                         double deadlineSeconds)""",
        """        private bool StepTransitionFaces(IRegionReadSource source,
                                         in MaterialPaletteView palette,
                                         float voxelSize,
                                         double deadlineSeconds)""",
        "transition function signature",
    )
    cache = replace_once(
        cache,
        "            if (MinViewDistanceMetres <= 0f || camera == null) return true;",
        "            if (SourceStep <= SurfaceLodHierarchy.FinestSourceStep) return true;",
        "transition finest-LOD guard",
    )
    cache = replace_once(
        cache,
        "            Vector3 cameraPosition = camera.transform.position;\n",
        "",
        "transition camera dependency",
    )
    cache = replace_once(
        cache,
        """                if (!FaceNeedsTransition(_build.Coordinate, face, voxelSize,
                                         cameraPosition))
                {
                    _build.Cursor++;
                    continue;
                }
""",
        "",
        "fixed-shell transition-face filter",
    )
    cache = replace_once(
        cache,
        """                _transitionJobScheduled = false;
                _transitionResultPending = true;""",
        """                _transitionJobScheduled = false;
                TagTransitionVertices(_transitionFace);
                _transitionResultPending = true;""",
        "tag completed transition face",
    )

    snapshot_marker = """        /// <summary>
        /// Snapshots a transition face at the finer neighbour's sample spacing without"""
    if snapshot_marker not in cache:
        raise RuntimeError("could not find transition snapshot insertion marker")
    tag_method = """        private void TagTransitionVertices(int face)
        {
            if ((uint)face >= SurfaceLodTransitionMask.FaceCount)
                throw new ArgumentOutOfRangeException(nameof(face), face,
                    "Transition face must be in [0,5].");
            uint tag = (uint)(face + 1) << SmoothSurfaceVertex.TransitionTagShift;
            for (int i = 0; i < _transitionVertices.Length; i++)
            {
                SmoothSurfaceVertex vertex = _transitionVertices[i];
                vertex.Active = (vertex.Active & ~SmoothSurfaceVertex.TransitionTagMask) | tag;
                _transitionVertices[i] = vertex;
            }
        }

"""
    cache = cache.replace(snapshot_marker, tag_method + snapshot_marker, 1)
    CACHE.write_text(cache)

    sched = SCHED.read_text()
    sched = replace_once(
        sched,
        """                        SurfaceLodNodeKey active = _activeLodScratch[i];
                        RequestAndSync(active, SurfaceBuildPriority.PreserveActiveCoverage);
                        WorkerFor(active).CollectActiveCoordinate(
                            active.Coordinate, _visibilityFrustumPlanes, voxelSize, frame);""",
        """                        SurfaceLodNodeKey active = _activeLodScratch[i];
                        RequestAndSync(active, SurfaceBuildPriority.PreserveActiveCoverage);
                        CpuTransvoxelChunkCache worker = WorkerFor(active);
                        worker.SetHierarchyTransitionMask(active.Coordinate,
                            SurfaceLodTransitionMask.Compute(active, _activeLodCoverage));
                        worker.CollectActiveCoordinate(
                            active.Coordinate, _visibilityFrustumPlanes, voxelSize, frame);""",
        "active draw transition mask",
    )
    SCHED.write_text(sched)

    shader = SHADER.read_text()
    shader = replace_once(
        shader,
        """            uint _SurfaceIndexBase;
            uint _SurfaceVertexBase;
            float4 _BaseColor;""",
        """            uint _SurfaceIndexBase;
            uint _SurfaceVertexBase;
            uint _SurfaceTransitionMask;
            float4 _BaseColor;""",
        "shader transition mask uniform",
    )
    old_vert = """                Varyings output;
                output.positionCS = TransformWorldToHClip(vertex.position);
                output.positionWS = vertex.position;
                output.normalNS = normalize(vertex.normal);
                output.material = vertex.material;
                output.occlusion = ((vertex.active >> 8) & 0xFFu) * (1.0 / 255.0);
                return output;"""
    new_vert = """                Varyings output;
                output.positionWS = vertex.position;
                output.normalNS = normalize(vertex.normal);
                output.material = vertex.material;
                output.occlusion = ((vertex.active >> 8) & 0xFFu) * (1.0 / 255.0);

                uint transitionTag = (vertex.active >> 24) & 0xFFu;
                if (transitionTag > 0u)
                {
                    uint faceBit = 1u << (transitionTag - 1u);
                    if ((_SurfaceTransitionMask & faceBit) == 0u)
                    {
                        // Every vertex in one reusable transition triangle carries the same face
                        // tag. Collapsing disabled vertices to one offscreen point makes the
                        // triangle degenerate without changing buffers or issuing another draw.
                        output.positionCS = float4(2.0, 2.0, 2.0, 1.0);
                        return output;
                    }
                }

                output.positionCS = TransformWorldToHClip(vertex.position);
                return output;"""
    shader = replace_once(shader, old_vert, new_vert, "shader reusable transition suppression")
    SHADER.write_text(shader)


if __name__ == "__main__":
    main()
