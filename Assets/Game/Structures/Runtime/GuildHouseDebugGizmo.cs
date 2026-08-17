using Unity.Mathematics;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Structures.Runtime
{
    /// <summary>Scene-view overlay for guild shell, room roles/depth and concealed-access plans.</summary>
    public sealed class GuildHouseDebugGizmo : MonoBehaviour
    {
        [SerializeField] private GuildHouseKind _guild = GuildHouseKind.Wizards;
        [SerializeField] private DecorationRegionTheme _region = DecorationRegionTheme.Moordell;
        [SerializeField] private uint _seed = 1u;
        [SerializeField] private uint _structureId = 7001u;
        [SerializeField] private int _width = 150;
        [SerializeField] private int _depth = 140;
        [SerializeField] private int _rooms = 0;
        [SerializeField, Min(0.001f)] private float _voxelWorldSize = 0.1f;
        [SerializeField] private bool _showLabels = true;
        [SerializeField] private bool _showSecretAccess = true;

        private void OnDrawGizmosSelected()
        {
            float s = Mathf.Max(0.001f, _voxelWorldSize);
            Vector3 world = transform.position;
            int3 origin = new int3(Mathf.RoundToInt(world.x / s), Mathf.RoundToInt(world.y / s), Mathf.RoundToInt(world.z / s));
            GuildHousePrototype prototype = GuildHousePrototypeComposition.Build(
                _guild, _region, _seed, _structureId, origin, Mathf.Max(64,_width), Mathf.Max(64,_depth), _rooms);
            if (!prototype.IsWellFormed) return;

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Color oldColor = Gizmos.color;
            Gizmos.matrix = Matrix4x4.Scale(Vector3.one * s);

            GuildHouseSpatialPlan plan = prototype.SpatialPlan;
            Gizmos.color = Color.white;
            Vector3 shellCenter = new Vector3(plan.Origin.x + plan.Width * .5f,
                plan.Origin.y + plan.FloorCount * plan.FloorHeight * .5f,
                plan.Origin.z + plan.Depth * .5f);
            Gizmos.DrawWireCube(shellCenter, new Vector3(plan.Width, plan.FloorCount * plan.FloorHeight, plan.Depth));

            for (int i = 0; i < prototype.Rooms.Length; i++)
            {
                GuildHouseSpatialRoom room = prototype.Rooms[i].SpatialRoom;
                Gizmos.color = DepthColor(room.Node.Depth);
                DrawBounds(room.Min, room.Size);
#if UNITY_EDITOR
                if (_showLabels)
                    Handles.Label((Vector3)(new Vector3(room.Min.x + room.Size.x*.5f, room.Min.y + room.Size.y*.5f, room.Min.z + room.Size.z*.5f) * s),
                        $"{_guild}\n{room.Node.Room.Role} depth {room.Node.Depth}" + (room.Node.HiddenAccess ? "\nHIDDEN" : string.Empty));
#endif
            }

            if (_showSecretAccess)
            {
                GuildHouseSecretPortal[] portals = GuildHouseSecretAccessPlanner.Plan(in plan);
                Gizmos.color = Color.magenta;
                for (int i = 0; i < portals.Length; i++)
                {
                    DrawBounds(portals[i].Min, portals[i].Size);
                    Vector3 c = new Vector3(portals[i].Min.x + portals[i].Size.x*.5f,
                        portals[i].Min.y + portals[i].Size.y*.5f,
                        portals[i].Min.z + portals[i].Size.z*.5f);
                    Vector3 f = new Vector3(portals[i].Facing.x, 0, portals[i].Facing.z);
                    Gizmos.DrawLine(c, c + f * 8f);
                }
            }

            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }

        private static void DrawBounds(int3 min, int3 size)
        {
            Gizmos.DrawWireCube(new Vector3(min.x + size.x*.5f, min.y + size.y*.5f, min.z + size.z*.5f),
                new Vector3(size.x,size.y,size.z));
        }

        private static Color DepthColor(byte depth)
        {
            switch (depth)
            {
                case 0: return new Color(.2f,1f,.3f,.9f);
                case 1: return new Color(.3f,.8f,1f,.9f);
                case 2: return new Color(1f,.8f,.2f,.9f);
                case 3: return new Color(1f,.4f,.2f,.9f);
                default: return new Color(.8f,.2f,1f,.9f);
            }
        }
    }
}
