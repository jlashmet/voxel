using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Core.Storage;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A walking character resolved directly against voxels — gravity, ground contact, step-up,
    /// jumping, and wall sliding, with no Unity colliders anywhere.
    ///
    /// Colliders are not an option here: voxels have no GameObject representation, and giving
    /// them one would create a second copy of the world that has to be kept in sync with the
    /// brickmap. Collision reads the same storage the renderer meshes (Constitution
    /// Principle II), so what you stand on is exactly what you see.
    ///
    /// This does *not* use <see cref="Collision.SweptAabb"/>, and that is worth explaining:
    /// that helper interprets its AABB in **brick** coordinates, so its finest resolution is
    /// 0.8 m. A character at 10 cm voxels would stop nearly a metre from walls and could not
    /// stand on anything smaller than a brick. Resolution here is per voxel. The engine helper
    /// wants a voxel-space overload before anything ships on it.
    /// </summary>
    public sealed class CharacterMotor
    {
        /// <summary>Metres per voxel.</summary>
        private const float VoxelSize = VoxelSurfaceRenderer.VoxelSize;

        public float Height = 1.8f;
        public float Radius = 0.3f;
        public float EyeHeight = 1.65f;

        public float WalkSpeed = 5.5f;
        public float SprintMultiplier = 2.2f;
        public float JumpSpeed = 6.2f;
        public float Gravity = 22f;

        /// <summary>Largest ledge that can be walked up without jumping: 3 voxels.</summary>
        public float StepHeight = 0.3f;

        /// <summary>Feet position in metres.</summary>
        public Vector3 Position;

        public Vector3 Velocity;
        public bool Grounded { get; private set; }

        /// <summary>Camera position for this character.</summary>
        public Vector3 EyePosition => Position + Vector3.up * EyeHeight;

        /// <summary>
        /// Advances one step. <paramref name="wishDir"/> is a horizontal unit-ish vector in world
        /// space; vertical motion comes from gravity and jumps only.
        /// </summary>
        public void Step(ShowcaseWorld world, Vector3 wishDir, bool sprint, bool jump, float dt)
        {
            // Long frames — a region finishing generation, say — must not teleport the character
            // through the floor, so the step is clamped rather than trusted.
            dt = Mathf.Min(dt, 0.05f);

            float speed = WalkSpeed * (sprint ? SprintMultiplier : 1f);
            Velocity.x = wishDir.x * speed;
            Velocity.z = wishDir.z * speed;

            if (Grounded && jump) Velocity.y = JumpSpeed;
            Velocity.y -= Gravity * dt;
            Velocity.y = Mathf.Max(Velocity.y, -60f);

            var delta = Velocity * dt;

            MoveHorizontal(world, new Vector3(delta.x, 0f, 0f));
            MoveHorizontal(world, new Vector3(0f, 0f, delta.z));
            MoveVertical(world, delta.y);

            Grounded = IsBlocked(world, FootMin(Position) + new Vector3(0f, -0.02f, 0f),
                                        FootMax(Position, 0.02f));

            if (Grounded && Velocity.y < 0f) Velocity.y = 0f;
        }

        /// <summary>Drops the character onto the surface below the given position.</summary>
        public void SnapToGround(ShowcaseWorld world, Vector3 near)
        {
            int surface = world.SurfaceHeight(Mathf.FloorToInt(near.x / VoxelSize),
                                              Mathf.FloorToInt(near.z / VoxelSize));

            Position = new Vector3(near.x, (surface + 2) * VoxelSize, near.z);
            Velocity = Vector3.zero;
        }

        // -- movement ------------------------------------------------------------

        /// <summary>
        /// Moves horizontally, trying a step up when blocked. Sub-stepping keeps the character
        /// from tunnelling through a voxel at speed, and stopping at the last free sub-step is
        /// what makes contact feel solid rather than sticky.
        /// </summary>
        private void MoveHorizontal(ShowcaseWorld world, Vector3 delta)
        {
            if (delta.sqrMagnitude < 1e-10f) return;

            if (TrySlide(world, delta, out var moved))
            {
                Position += moved;
                return;
            }

            Position += moved;

            // Blocked: try again from a step up, then settle back down onto the ledge.
            if (!Grounded) return;

            var raised = Position + Vector3.up * StepHeight;
            if (IsBlocked(world, FootMin(raised), FootMax(raised, Height))) return;

            var savedPosition = Position;
            Position = raised;

            if (TrySlide(world, delta, out var stepMoved) && stepMoved.sqrMagnitude > 1e-8f)
            {
                Position += stepMoved;
                MoveVertical(world, -StepHeight);
            }
            else
            {
                Position = savedPosition;
            }
        }

        /// <summary>
        /// Walks <paramref name="delta"/> in half-voxel sub-steps. Returns true when the whole
        /// displacement was free; <paramref name="moved"/> always carries the part that was.
        /// </summary>
        private bool TrySlide(ShowcaseWorld world, Vector3 delta, out Vector3 moved)
        {
            float distance = delta.magnitude;
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / (VoxelSize * 0.5f)), 1, 64);
            var increment = delta / steps;

            moved = Vector3.zero;

            for (int i = 0; i < steps; i++)
            {
                var candidate = Position + moved + increment;
                if (IsBlocked(world, FootMin(candidate), FootMax(candidate, Height)))
                    return false;

                moved += increment;
            }

            return true;
        }

        private void MoveVertical(ShowcaseWorld world, float dy)
        {
            if (Mathf.Abs(dy) < 1e-6f) return;

            int steps = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(dy) / (VoxelSize * 0.5f)), 1, 64);
            float increment = dy / steps;

            for (int i = 0; i < steps; i++)
            {
                var candidate = Position + Vector3.up * increment;
                if (IsBlocked(world, FootMin(candidate), FootMax(candidate, Height)))
                {
                    Velocity.y = 0f;
                    return;
                }

                Position = candidate;
            }
        }

        // -- queries -------------------------------------------------------------

        private Vector3 FootMin(Vector3 feet) => new(feet.x - Radius, feet.y, feet.z - Radius);
        private Vector3 FootMax(Vector3 feet, float height) => new(feet.x + Radius, feet.y + height, feet.z + Radius);

        /// <summary>
        /// True when any voxel overlapping the box is solid.
        ///
        /// Non-resident regions read as empty, which means the character would fall through a
        /// region that has not finished generating — the driver holds physics until the region
        /// under the character exists rather than letting that happen.
        /// </summary>
        private static bool IsBlocked(ShowcaseWorld world, Vector3 min, Vector3 max)
        {
            int minX = Mathf.FloorToInt(min.x / VoxelSize);
            int minY = Mathf.FloorToInt(min.y / VoxelSize);
            int minZ = Mathf.FloorToInt(min.z / VoxelSize);
            int maxX = Mathf.FloorToInt((max.x - 1e-4f) / VoxelSize);
            int maxY = Mathf.FloorToInt((max.y - 1e-4f) / VoxelSize);
            int maxZ = Mathf.FloorToInt((max.z - 1e-4f) / VoxelSize);

            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (VoxelAccess.IsSolid(ref world.Table, in world.Pool, new int3(x, y, z)))
                    return true;
            }

            return false;
        }
    }
}
