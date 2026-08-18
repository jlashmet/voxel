using Game.Structures.Api;
using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Scene-view look-dev overlay for the first procedural decoration vertical slice. Add this
    /// component to a GameObject at the castle centre (or supply an explicit voxel centre) to see
    /// the semantic room, placement sockets, exclusions, resolved prop bounds, facings, and anchor
    /// relationships produced by the exact same adapter/resolver used by castle authoring.
    /// </summary>
    public sealed class CastleBedroomDecorationDebugGizmo : MonoBehaviour
    {
        [SerializeField] private uint _seed = 1u;
        [SerializeField] private bool _useTransformAsCastleCentre = true;
        [SerializeField] private Vector3Int _castleCentreVoxel;
        [SerializeField, Min(0.001f)] private float _voxelWorldSize = 0.1f;

        [Header("Decoration Debug Layers")]
        [SerializeField] private bool _showSpace = true;
        [SerializeField] private bool _showSockets = true;
        [SerializeField] private bool _showSocketFacing = true;
        [SerializeField] private bool _showExclusions = true;
        [SerializeField] private bool _showPlacements = true;
        [SerializeField] private bool _showAnchorLinks = true;
        [SerializeField] private bool _showLabels = true;

        private void OnDrawGizmosSelected()
        {
            float voxelSize = Mathf.Max(0.001f, _voxelWorldSize);
            int3 centre = ResolveCastleCentre(voxelSize);
            CastlePlan plan = CastlePlanner.Plan(centre, _seed);

            if (!CastleBedroomDecorationAdapter.TryResolve(
                    in plan,
                    out DecorationSpace space,
                    out _,
                    out DecorationExclusion[] exclusions,
                    out DecorationPlacement[] placements))
                return;

            DecorationSocket[] sockets = RectangularDecorationSpaceAnalyzer.ExtractSockets(in space);
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.Scale(Vector3.one * voxelSize);

            if (_showSpace)
            {
                Gizmos.color = new Color(0.2f, 0.85f, 1f, 0.9f);
                DrawBounds(in space.Bounds);
            }

            if (_showSockets)
                DrawSockets(sockets);

            if (_showExclusions)
                DrawExclusions(exclusions);

            if (_showPlacements)
                DrawPlacements(placements);

            if (_showAnchorLinks)
                DrawAnchorLinks(placements);

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }

        private int3 ResolveCastleCentre(float voxelSize)
        {
            if (!_useTransformAsCastleCentre)
                return new int3(_castleCentreVoxel.x, _castleCentreVoxel.y, _castleCentreVoxel.z);

            Vector3 world = transform.position;
            return new int3(
                Mathf.RoundToInt(world.x / voxelSize),
                Mathf.RoundToInt(world.y / voxelSize),
                Mathf.RoundToInt(world.z / voxelSize));
        }

        private void DrawSockets(DecorationSocket[] sockets)
        {
            if (sockets == null)
                return;

            for (int i = 0; i < sockets.Length; i++)
            {
                DecorationSocket socket = sockets[i];
                if (!socket.IsWellFormed)
                    continue;

                Gizmos.color = SocketColor(socket.Kind);
                DrawBounds(in socket.Bounds);

                if (_showSocketFacing)
                {
                    Vector3 centre = BoundsCenter(in socket.Bounds);
                    Vector3 facing = new Vector3(socket.Facing.x, socket.Facing.y, socket.Facing.z);
                    Gizmos.DrawLine(centre, centre + facing * 6f);
                }

#if UNITY_EDITOR
                if (_showLabels)
                    DrawLabel(BoundsCenter(in socket.Bounds), $"socket {socket.SocketId} {socket.Kind}");
#endif
            }
        }

        private void DrawExclusions(DecorationExclusion[] exclusions)
        {
            if (exclusions == null)
                return;

            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.95f);
            for (int i = 0; i < exclusions.Length; i++)
            {
                if (!exclusions[i].IsWellFormed)
                    continue;

                DrawBounds(in exclusions[i].Bounds);
#if UNITY_EDITOR
                if (_showLabels)
                    DrawLabel(BoundsCenter(in exclusions[i].Bounds), $"exclude {exclusions[i].Kind}");
#endif
            }
        }

        private void DrawPlacements(DecorationPlacement[] placements)
        {
            if (placements == null)
                return;

            Gizmos.color = new Color(0.25f, 1f, 0.35f, 0.95f);
            for (int i = 0; i < placements.Length; i++)
            {
                if (!placements[i].IsWellFormed)
                    continue;

                DrawBounds(in placements[i].Bounds);
#if UNITY_EDITOR
                if (_showLabels)
                    DrawLabel(
                        BoundsCenter(in placements[i].Bounds),
                        $"{placements[i].Family} slot {placements[i].SlotId}\n{placements[i].Id}");
#endif
            }
        }

        private static void DrawAnchorLinks(DecorationPlacement[] placements)
        {
            if (placements == null)
                return;

            Gizmos.color = new Color(1f, 0.85f, 0.15f, 1f);
            for (int i = 0; i < placements.Length; i++)
            {
                uint anchorSlotId = placements[i].AnchorSlotId;
                if (anchorSlotId == 0)
                    continue;

                int anchorIndex = FindPlacement(placements, anchorSlotId);
                if (anchorIndex < 0)
                    continue;

                Gizmos.DrawLine(
                    BoundsCenter(in placements[anchorIndex].Bounds),
                    BoundsCenter(in placements[i].Bounds));
            }
        }

        private static int FindPlacement(DecorationPlacement[] placements, uint slotId)
        {
            for (int i = 0; i < placements.Length; i++)
                if (placements[i].SlotId == slotId)
                    return i;
            return -1;
        }

        private static Color SocketColor(DecorationSocketKind kind)
        {
            switch (kind)
            {
                case DecorationSocketKind.Floor:
                    return new Color(0.2f, 0.65f, 1f, 0.8f);
                case DecorationSocketKind.Wall:
                    return new Color(0.75f, 0.35f, 1f, 0.8f);
                case DecorationSocketKind.Corner:
                    return new Color(1f, 0.45f, 0.8f, 0.8f);
                case DecorationSocketKind.Ceiling:
                    return new Color(0.2f, 1f, 0.95f, 0.8f);
                default:
                    return Color.white;
            }
        }

        private static void DrawBounds(in DecorationBounds bounds)
        {
            Gizmos.DrawWireCube(BoundsCenter(in bounds), BoundsSize(in bounds));
        }

        private static Vector3 BoundsCenter(in DecorationBounds bounds)
        {
            return new Vector3(
                (bounds.Min.x + bounds.MaxExclusive.x) * 0.5f,
                (bounds.Min.y + bounds.MaxExclusive.y) * 0.5f,
                (bounds.Min.z + bounds.MaxExclusive.z) * 0.5f);
        }

        private static Vector3 BoundsSize(in DecorationBounds bounds)
        {
            int3 size = bounds.Size;
            return new Vector3(size.x, size.y, size.z);
        }

#if UNITY_EDITOR
        private void DrawLabel(Vector3 voxelPosition, string text)
        {
            Matrix4x4 matrix = Gizmos.matrix;
            Vector3 worldPosition = matrix.MultiplyPoint3x4(voxelPosition);
            Handles.Label(worldPosition, text);
        }
#endif
    }
}
