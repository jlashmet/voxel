using Unity.Mathematics;
using UnityEngine;
using VoxelEngine.Composition;
using VoxelEngine.Storage.Api;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// A walking character resolved directly against authoritative world data — gravity, ground
    /// contact, step-up, jumping, and wall sliding, with no Unity colliders anywhere.
    ///
    /// Unity colliders are not an option for the voxel world, and semantic trees deliberately have
    /// no per-tree GameObject while healthy/batched. Collision therefore queries the same voxel
    /// storage and semantic tree state that presentation consumes instead of maintaining a second
    /// physics copy of either world.
    ///
    /// This does *not* use the existing brick-space swept-AABB helper, and that is worth explaining:
    /// that helper interprets its AABB in **brick** coordinates, so its finest resolution is
    /// 0.8 m. A character at 10 cm voxels would stop nearly a metre from walls and could not
    /// stand on anything smaller than a brick. Resolution here is per voxel. The engine helper
    /// wants a voxel-space overload before anything ships on it.
    /// </summary>
    public sealed class CharacterMotor
    {
        /// <summary>Metres per voxel.</summary>
        private const float VoxelSize = ShowcaseWorld.VoxelSize;

        public float Height = 1.8f;
        public float Radius = 0.3f;
        public float EyeHeight = 1.65f;

        public float WalkSpeed = 5.5f;
        public float SprintMultiplier = 2.2f;
        public float JumpSpeed = 6.2f;
        public float Gravity = 22f;
        public float FlightRiseSpeed = 7.5f;
        public float FlightAcceleration = 24f;
        public float FlightHoldDelay = 0.35f;

        /// <summary>Largest ledge that can be walked up without jumping: 3 voxels.</summary>
        public float StepHeight = 0.3f;

        /// <summary>Feet position in metres.</summary>
        public Vector3 Position;

        public Vector3 Velocity;
        public bool Grounded { get; private set; }
        public bool AssistedFlight { get; private set; }

        private bool _jumpWasHeld;
        private bool _airJumpAvailable = true;
        private float _jumpHoldSeconds;

        /// <summary>Camera position for this character.</summary>
        public Vector3 EyePosition => Position + Vector3.up * EyeHeight;

        /// <summary>
        /// Advances one step. <paramref name="wishDir"/> is a horizontal unit-ish vector in world
        /// space; vertical motion comes from gravity and jumps only.
        /// </summary>
        public void Step(ShowcaseWorld world, Vector3 wishDir, bool sprint, bool jumpHeld, float dt)
        {
            // Long frames — a region finishing generation, say — must not teleport the character
            // through the floor, so the step is clamped rather than trusted.
            dt = Mathf.Min(dt, 0.05f);

            float speed = WalkSpeed * (sprint ? SprintMultiplier : 1f);
            Velocity.x = wishDir.x * speed;
            Velocity.z = wishDir.z * speed;

            bool jumpPressed = jumpHeld && !_jumpWasHeld;
            if (Grounded)
            {
                _airJumpAvailable = true;
                AssistedFlight = false;
            }

            if (jumpPressed)
            {
                if (Grounded)
                {
                    Velocity.y = JumpSpeed;
                }
                else if (_airJumpAvailable)
                {
                    Velocity.y = JumpSpeed;
                    _airJumpAvailable = false;
                }
            }

            if (jumpHeld)
            {
                _jumpHoldSeconds += dt;
                if (!Grounded && _jumpHoldSeconds >= FlightHoldDelay)
                {
                    AssistedFlight = true;
                    Velocity.y = Mathf.MoveTowards(Velocity.y, FlightRiseSpeed,
                                                   FlightAcceleration * dt);
                }
            }
            else
            {
                _jumpHoldSeconds = 0f;
                AssistedFlight = false;
            }

            Velocity.y -= Gravity * dt;
            Velocity.y = Mathf.Max(Velocity.y, -60f);

            var delta = Velocity * dt;

            MoveHorizontal(world, new Vector3(delta.x, 0f, 0f));
            MoveHorizontal(world, new Vector3(0f, 0f, delta.z));
            MoveVertical(world, delta.y);

            Grounded = IsBlocked(world, FootMin(Position) + new Vector3(0f, -0.02f, 0f),
                                        FootMax(Position, 0.02f));

            if (Grounded && Velocity.y < 0f) Velocity.y = 0f;
            _jumpWasHeld = jumpHeld;
        }

        /// <summary>Drops the character onto the surface below the given position.</summary>
        public void SnapToGround(ShowcaseWorld world, Vector3 near)
        {
            // Ground against the complete capsule footprint. Sampling only the centre column
            // embeds an edge of the player whenever the terrain rises by one voxel beneath its
            // 60 cm width—the exact failure an oblique hillside spawn exposed.
            int minX = Mathf.FloorToInt((near.x - Radius) / VoxelSize);
            int maxX = Mathf.FloorToInt((near.x + Radius - 1e-4f) / VoxelSize);
            int minZ = Mathf.FloorToInt((near.z - Radius) / VoxelSize);
            int maxZ = Mathf.FloorToInt((near.z + Radius - 1e-4f) / VoxelSize);
            int surface = int.MinValue;
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
                surface = Mathf.Max(surface, world.OccupiedSurfaceHeight(x, z));

            Position = new Vector3(near.x, (surface + 2) * VoxelSize, near.z);
            Velocity = Vector3.zero;
            _airJumpAvailable = true;
            _jumpWasHeld = false;
            _jumpHoldSeconds = 0f;
            AssistedFlight = false;
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
        /// True when authoritative voxel or semantic-tree wood overlaps the character box.
        ///
        /// Non-resident voxel regions read as empty, which means the character would fall through a
        /// region that has not finished generating — the driver holds physics until the region
        /// under the character exists rather than letting that happen. Tree collision is branch-only:
        /// foliage remains traversable and a branch stops blocking as soon as damage removes it.
        /// </summary>
        private static bool IsBlocked(ShowcaseWorld world, Vector3 min, Vector3 max)
        {
            int minX = Mathf.FloorToInt(min.x / VoxelSize);
            int minY = Mathf.FloorToInt(min.y / VoxelSize);
            int minZ = Mathf.FloorToInt(min.z / VoxelSize);
            int maxX = Mathf.FloorToInt((max.x - 1e-4f) / VoxelSize);
            int maxY = Mathf.FloorToInt((max.y - 1e-4f) / VoxelSize);
            int maxZ = Mathf.FloorToInt((max.z - 1e-4f) / VoxelSize);

            IVoxelSurfaceQuery surface = world.SurfaceQuery;
            for (int y = minY; y <= maxY; y++)
            for (int z = minZ; z <= maxZ; z++)
            for (int x = minX; x <= maxX; x++)
            {
                if (surface.TryRead(new int3(x, y, z), out VoxelCell cell) &&
                    cell.BaseMaterialId != VoxelGrid.MaterialEmpty)
                    return true;
            }

            return VegetationComposition.TreeDamage.OverlapsWoodAabb(
                new float3(min.x, min.y, min.z),
                new float3(max.x, max.y, max.z));
        }
    }
}
