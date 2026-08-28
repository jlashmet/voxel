using System.Collections.Generic;
using Unity.Mathematics;
using VoxelEngine.Edits.Api;
using VoxelEngine.Storage.Api;
using VoxelEngine.Storage.Runtime;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Bounded runtime state transition for the two authored front-gate leaves. The interaction
    /// remains authoritative in TryOpenCastleFrontGate; this partial preserves the actual closed
    /// materials before that interaction clears them, then rotates those cells around the hinges.
    /// </summary>
    public sealed partial class ShowcaseWorld
    {
        private const float CastleFrontGateAnimationSeconds = 0.9f;
        private const int CastleFrontGateAnimationPoseCount = 9;
        private const float CastleFrontGateMaximumSwingDegrees = 78f;

        private Dictionary<int3, byte> _castleFrontGateClosedPose;
        private Dictionary<int3, byte> _castleFrontGatePose;
        private bool _castleFrontGateAnimationStarted;
        private bool _castleFrontGateAnimating;
        private float _castleFrontGateAnimationElapsed;
        private int _castleFrontGateAnimationPoseIndex;

        public bool CastleFrontGateAnimating => _castleFrontGateAnimating;

        public float CastleFrontGateAnimationProgress => !_castleFrontGateAnimationStarted
            ? 0f
            : (_castleFrontGateAnimating
                ? math.saturate(_castleFrontGateAnimationElapsed / CastleFrontGateAnimationSeconds)
                : 1f);

        /// <summary>
        /// Snapshots the already-authored closed gate. The showcase runtime driver calls this
        /// while the gate is closed, so the animation reuses the real authored timber, iron and
        /// latch cells rather than approximating the art a second time in gameplay code.
        /// </summary>
        public void PrepareCastleFrontGateAnimation()
        {
            if (!_hasCastlePlan || _castleFrontGateOpen || _castleFrontGateClosedPose != null)
                return;

            int3 min = CastleLayout.FrontGateMinimum(in _castlePlan);
            int half = CastleLayout.FrontGateWidth / 2;
            int archTop = CastleLayout.FrontGateHeight - half;
            var pose = new Dictionary<int3, byte>(CastleLayout.FrontGateWidth
                                                  * CastleLayout.FrontGateHeight
                                                  * CastleLayout.FrontGateDepth);

            for (int d = 0; d < CastleLayout.FrontGateDepth; d++)
            for (int w = 0; w < CastleLayout.FrontGateWidth; w++)
            for (int h = 0; h < CastleLayout.FrontGateHeight; h++)
            {
                int dx = w - half;
                if (h > archTop && dx * dx + (h - archTop) * (h - archTop) > half * half)
                    continue;

                int3 voxel = new(min.x + w, min.y + h, min.z + d);
                if (!SurfaceQuery.TryRead(voxel, out VoxelCell cell)
                    || cell.BaseMaterialId == VoxelGrid.MaterialEmpty)
                    continue;
                pose[voxel] = cell.BaseMaterialId;
            }

            // During async castle construction the gate volume can be temporarily empty. Retry on
            // a later frame rather than caching that transient state as the closed gate forever.
            if (pose.Count > CastleLayout.FrontGateWidth * 8)
                _castleFrontGateClosedPose = pose;
        }

        /// <summary>
        /// Advances after the showcase's normal Update. When E has just been accepted the legacy
        /// interaction has already cleared the leaf; pose zero is therefore restored in LateUpdate
        /// before that empty state can be rendered, and subsequent poses swing both leaves inward.
        /// </summary>
        public void StepCastleFrontGateAnimation(float deltaTime)
        {
            if (!_castleFrontGateOpen || _castleFrontGateClosedPose == null)
                return;

            if (!_castleFrontGateAnimationStarted)
            {
                _castleFrontGateAnimationStarted = true;
                _castleFrontGateAnimating = true;
                _castleFrontGateAnimationElapsed = 0f;
                _castleFrontGateAnimationPoseIndex = 0;
                _castleFrontGatePose = BuildCastleFrontGatePose(0);
                ReplaceCastleFrontGatePose(null, _castleFrontGatePose);
                return;
            }

            if (!_castleFrontGateAnimating)
                return;

            _castleFrontGateAnimationElapsed += math.max(0f, deltaTime);
            float progress = math.saturate(_castleFrontGateAnimationElapsed
                                           / CastleFrontGateAnimationSeconds);
            int targetPose = progress >= 1f
                ? CastleFrontGateAnimationPoseCount
                : math.min(CastleFrontGateAnimationPoseCount - 1,
                           (int)math.floor(progress * CastleFrontGateAnimationPoseCount));

            while (_castleFrontGateAnimationPoseIndex < targetPose)
            {
                int nextPoseIndex = _castleFrontGateAnimationPoseIndex + 1;
                Dictionary<int3, byte> nextPose = BuildCastleFrontGatePose(nextPoseIndex);
                ReplaceCastleFrontGatePose(_castleFrontGatePose, nextPose);
                _castleFrontGatePose = nextPose;
                _castleFrontGateAnimationPoseIndex = nextPoseIndex;
            }

            if (_castleFrontGateAnimationPoseIndex < CastleFrontGateAnimationPoseCount)
                return;

            _castleFrontGateAnimationElapsed = CastleFrontGateAnimationSeconds;
            _castleFrontGateAnimating = false;
            _castleFrontGateClosedPose = null;
        }

        private Dictionary<int3, byte> BuildCastleFrontGatePose(int poseIndex)
        {
            if (poseIndex <= 0)
                return new Dictionary<int3, byte>(_castleFrontGateClosedPose);

            int3 min = CastleLayout.FrontGateMinimum(in _castlePlan);
            int width = CastleLayout.FrontGateWidth;
            int half = width / 2;
            float t = math.saturate(poseIndex / (float)CastleFrontGateAnimationPoseCount);
            float angle = math.radians(CastleFrontGateMaximumSwingDegrees * t);
            float cosine = math.cos(angle);
            float sine = math.sin(angle);
            var pose = new Dictionary<int3, byte>(_castleFrontGateClosedPose.Count);

            foreach (KeyValuePair<int3, byte> source in _castleFrontGateClosedPose)
            {
                int localW = source.Key.x - min.x;
                int localDepth = source.Key.z - min.z;
                bool left = localW < half;
                int u = left ? localW : width - 1 - localW;
                int x = left
                    ? min.x + (int)math.round(u * cosine - localDepth * sine)
                    : min.x + width - 1 + (int)math.round(-u * cosine + localDepth * sine);
                int z = min.z + (int)math.round(u * sine + localDepth * cosine);
                pose[new int3(x, source.Key.y, z)] = source.Value;
            }

            return pose;
        }

        private readonly struct CastleGateCellUpdate
        {
            public readonly int3 Position;
            public readonly byte Material;

            public CastleGateCellUpdate(int3 position, byte material)
            {
                Position = position;
                Material = material;
            }
        }

        private void ReplaceCastleFrontGatePose(
            Dictionary<int3, byte> previous,
            Dictionary<int3, byte> next)
        {
            var finalUpdates = new Dictionary<int3, byte>();
            if (previous != null)
            {
                foreach (KeyValuePair<int3, byte> cell in previous)
                    if (!next.ContainsKey(cell.Key))
                        finalUpdates[cell.Key] = VoxelGrid.MaterialEmpty;
            }

            foreach (KeyValuePair<int3, byte> cell in next)
            {
                if (previous != null
                    && previous.TryGetValue(cell.Key, out byte oldMaterial)
                    && oldMaterial == cell.Value)
                    continue;
                finalUpdates[cell.Key] = cell.Value;
            }

            var byBlock = new Dictionary<int3, List<CastleGateCellUpdate>>();
            foreach (KeyValuePair<int3, byte> cell in finalUpdates)
            {
                int3 block = cell.Key >> VoxelReadGrid.BlockEdgeLog2;
                if (!byBlock.TryGetValue(block, out List<CastleGateCellUpdate> updates))
                {
                    updates = new List<CastleGateCellUpdate>();
                    byBlock.Add(block, updates);
                }
                updates.Add(new CastleGateCellUpdate(cell.Key, cell.Value));
            }

            var changedRegions = new HashSet<int3>();
            foreach (KeyValuePair<int3, List<CastleGateCellUpdate>> block in byBlock)
            {
                if (!_mutationStore.TryBeginCellBlock(block.Key, false, out VoxelBlockMutation mutation))
                    continue;

                bool payloadChanged = false;
                List<CastleGateCellUpdate> updates = block.Value;
                for (int i = 0; i < updates.Count; i++)
                {
                    int3 inner = updates[i].Position & VoxelReadGrid.BlockEdgeMask;
                    int voxelIndex = inner.x
                                   | (inner.y << VoxelReadGrid.BlockEdgeLog2)
                                   | (inner.z << (VoxelReadGrid.BlockEdgeLog2 * 2));
                    payloadChanged |= mutation.SetMaterial(voxelIndex, updates[i].Material);
                }

                if (_mutationStore.CompletePartialBlock(ref mutation, payloadChanged))
                    changedRegions.Add(block.Key >> VoxelDimensions.RegionEdgeLog2);
            }

            VoxelChangeKind kind = VoxelChangeKind.Occupancy | VoxelChangeKind.BaseMaterial
                                 | VoxelChangeKind.SurfaceStyle | VoxelChangeKind.Coating;
            foreach (int3 region in changedRegions)
                _changes.PublishRegion(region, kind);
        }
    }
}
