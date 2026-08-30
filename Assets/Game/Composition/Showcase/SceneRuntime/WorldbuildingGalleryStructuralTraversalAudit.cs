using UnityEngine;

namespace VoxelEngine.Showcase
{
    /// <summary>
    /// Executes structural traversal evidence in the scene-runtime assembly that owns CharacterMotor.
    /// ShowcaseWorld only prepares authoritative route voxels/endpoints, preserving lower-layer assembly direction.
    /// </summary>
    internal static class WorldbuildingGalleryStructuralTraversalAudit
    {
        private const float CliffLandingRetreatMetres = 3.5f;

        public static ShowcaseWorld.GalleryStructuralTraversalReport AuditWorldbuildingGalleryStructuralTraversal(
            this ShowcaseWorld world,
            int index)
        {
            world.PrepareWorldbuildingGalleryStructuralTraversal(index, out Vector3 start, out Vector3 end);

            int route = index % ShowcaseWorld.WorldbuildingGalleryStructuralTraversalCount;
            if (route < 0) route += ShowcaseWorld.WorldbuildingGalleryStructuralTraversalCount;
            if (route == 2)
            {
                // The authored cliff house begins 2 m before the generic route endpoint. Keep the
                // traversal proof on the clear upper-platform landing rather than targeting its facade.
                end.x -= CliffLandingRetreatMetres;
            }

            var motor = new CharacterMotor { WalkSpeed = 6.5f, StepHeight = 0.35f };
            motor.SnapToGround(world, start);
            float startDistance = HorizontalDistance(motor.Position, end);
            int step;
            for (step = 0; step < 1500; step++)
            {
                Vector3 delta = end - motor.Position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 1.35f)
                    return new ShowcaseWorld.GalleryStructuralTraversalReport(
                        true, step, startDistance, distance, motor.Position);

                Vector3 wish = distance > 1e-5f ? delta / distance : Vector3.zero;
                motor.Step(world, wish, false, false, 1f / 30f);
            }

            float finalDistance = HorizontalDistance(motor.Position, end);
            return new ShowcaseWorld.GalleryStructuralTraversalReport(
                finalDistance <= 1.35f, step, startDistance, finalDistance, motor.Position);
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
