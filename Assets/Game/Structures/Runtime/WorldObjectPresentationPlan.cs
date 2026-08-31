using Game.Structures.Api;
using Unity.Mathematics;

namespace Game.Structures.Runtime
{
    /// <summary>
    /// Runtime presentation intent for object-owned geometry. This never edits surrounding world voxels.
    /// A renderer may realize the pose using a generated mesh, rigid transform, collider, light or particles.
    /// </summary>
    public struct WorldObjectPresentationPlan
    {
        public WorldObjectId Id;
        public WorldObjectKind Kind;
        public DecorationBounds BaselineBounds;
        public int3 TranslationVoxels;
        public int3 RotationDegrees;
        public bool UsesDynamicProxy;
        public bool Visible;
        public bool BlocksNavigation;
        public bool LightActive;
        public bool ParticleActive;
        public int StopIndex;

        public bool IsWellFormed => Id.Value != 0 && Kind != WorldObjectKind.Unknown && BaselineBounds.IsWellFormed;
    }

    public static class WorldObjectPresentationPlanner
    {
        public static WorldObjectPresentationPlan Plan(in WorldObjectResolvedState state)
        {
            WorldObjectDescriptor d = state.Descriptor;
            var plan = new WorldObjectPresentationPlan
            {
                Id = d.Id,
                Kind = d.Kind,
                BaselineBounds = d.Bounds,
                UsesDynamicProxy = RequiresDynamicProxy(d.Kind),
                Visible = !state.IsDestroyed && (state.State & WorldObjectStateFlags.Hidden) == 0,
                BlocksNavigation = !state.IsDestroyed &&
                    (d.Capabilities & WorldObjectCapabilities.BlocksNavigation) != 0,
                LightActive = !state.IsDestroyed &&
                    (d.Capabilities & WorldObjectCapabilities.EmitsLight) != 0 &&
                    (state.State & WorldObjectStateFlags.Active) != 0,
                ParticleActive = !state.IsDestroyed &&
                    (d.Capabilities & WorldObjectCapabilities.EmitsParticles) != 0 &&
                    (state.State & WorldObjectStateFlags.Active) != 0,
                StopIndex = state.RuntimeValue0,
            };

            bool open = state.IsOpen;
            switch (d.Kind)
            {
                case WorldObjectKind.Door:
                case WorldObjectKind.SecretDoor:
                    if (open)
                    {
                        plan.RotationDegrees = new int3(0, 90, 0);
                        plan.BlocksNavigation = false;
                    }
                    break;

                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                    if (open)
                    {
                        int travel = d.Parameter0 > 0 ? d.Parameter0 : d.Bounds.Size.y;
                        plan.TranslationVoxels = new int3(0, math.max(1, travel), 0);
                        plan.BlocksNavigation = false;
                    }
                    break;

                case WorldObjectKind.Drawbridge:
                    if (open)
                    {
                        int angle = d.Parameter0 > 0 ? d.Parameter0 : 90;
                        plan.RotationDegrees = new int3(-angle, 0, 0);
                        plan.BlocksNavigation = false;
                    }
                    break;

                case WorldObjectKind.Elevator:
                {
                    int stopCount = math.max(2, d.Parameter0);
                    int stop = math.clamp(state.RuntimeValue0, 0, stopCount - 1);
                    int travel = math.max(1, d.Bounds.Size.y / stopCount);
                    plan.TranslationVoxels = new int3(0, stop * travel, 0);
                    plan.StopIndex = stop;
                    break;
                }

                case WorldObjectKind.MovingPlatform:
                    if ((state.State & WorldObjectStateFlags.Active) != 0)
                    {
                        int travel = d.Parameter0 != 0 ? d.Parameter0 : math.max(4, d.Bounds.Size.x);
                        plan.TranslationVoxels = math.abs(d.Facing.x) == 1
                            ? new int3(d.Facing.x * travel, 0, 0)
                            : new int3(0, 0, d.Facing.z * travel);
                    }
                    break;

                case WorldObjectKind.RotatingWall:
                    if (open)
                    {
                        plan.RotationDegrees = new int3(0, 90, 0);
                        plan.BlocksNavigation = false;
                    }
                    break;

                case WorldObjectKind.SpikeTrap:
                    if ((state.State & WorldObjectStateFlags.Triggered) != 0)
                        plan.TranslationVoxels = new int3(0, math.max(2, d.Bounds.Size.y / 2), 0);
                    break;

                case WorldObjectKind.FallingBlockTrap:
                    if ((state.State & WorldObjectStateFlags.Triggered) != 0)
                        plan.TranslationVoxels = new int3(0, -math.max(2, d.Bounds.Size.y), 0);
                    break;

                case WorldObjectKind.Crusher:
                    if ((state.State & WorldObjectStateFlags.Triggered) != 0)
                        plan.TranslationVoxels = new int3(0, -math.max(2, d.Bounds.Size.y / 2), 0);
                    break;

                case WorldObjectKind.BreakableWall:
                    plan.Visible = !state.IsDestroyed;
                    plan.BlocksNavigation = !state.IsDestroyed;
                    break;

                case WorldObjectKind.Chest:
                    if (open) plan.RotationDegrees = new int3(-75, 0, 0);
                    break;

                case WorldObjectKind.Lever:
                case WorldObjectKind.Switch:
                case WorldObjectKind.Valve:
                case WorldObjectKind.Winch:
                    if ((state.State & WorldObjectStateFlags.Active) != 0)
                        plan.RotationDegrees = new int3(0, 0, 35);
                    break;

                case WorldObjectKind.Button:
                case WorldObjectKind.PressurePlate:
                    if ((state.State & (WorldObjectStateFlags.Active | WorldObjectStateFlags.Triggered)) != 0)
                        plan.TranslationVoxels = new int3(0, -1, 0);
                    break;
            }

            return plan;
        }

        public static bool RequiresDynamicProxy(WorldObjectKind kind)
        {
            switch (kind)
            {
                case WorldObjectKind.Door:
                case WorldObjectKind.Gate:
                case WorldObjectKind.Portcullis:
                case WorldObjectKind.Drawbridge:
                case WorldObjectKind.Elevator:
                case WorldObjectKind.MovingPlatform:
                case WorldObjectKind.Lever:
                case WorldObjectKind.Switch:
                case WorldObjectKind.Button:
                case WorldObjectKind.PressurePlate:
                case WorldObjectKind.PullChain:
                case WorldObjectKind.Winch:
                case WorldObjectKind.Valve:
                case WorldObjectKind.Chest:
                case WorldObjectKind.Torch:
                case WorldObjectKind.Lantern:
                case WorldObjectKind.Brazier:
                case WorldObjectKind.Fireplace:
                case WorldObjectKind.Trap:
                case WorldObjectKind.SpikeTrap:
                case WorldObjectKind.DartTrap:
                case WorldObjectKind.FallingBlockTrap:
                case WorldObjectKind.Crusher:
                case WorldObjectKind.SecretDoor:
                case WorldObjectKind.RotatingWall:
                case WorldObjectKind.BreakableWall:
                case WorldObjectKind.Generator:
                case WorldObjectKind.FuseBox:
                case WorldObjectKind.MineCart:
                case WorldObjectKind.Cart:
                case WorldObjectKind.Teleporter:
                    return true;
                default:
                    return false;
            }
        }

        public static WorldObjectPresentationPlan[] PlanAll(WorldObjectDescriptor[] objects, WorldObjectStateStore state)
        {
            if (objects == null) return new WorldObjectPresentationPlan[0];
            var plans = new WorldObjectPresentationPlan[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                WorldObjectResolvedState resolved = WorldObjectStateResolver.Resolve(in objects[i], state);
                plans[i] = Plan(in resolved);
            }
            return plans;
        }
    }
}
