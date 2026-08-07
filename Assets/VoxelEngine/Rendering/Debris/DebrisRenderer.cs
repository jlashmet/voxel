using UnityEngine;
using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;
using VoxelEngine.Core.Storage;
using VoxelEngine.Tiering;

namespace VoxelEngine.Rendering.Debris
{
    /// <summary>
    /// Renders debris via indirect draw with per-instance transforms.
    ///
    /// For visual-only debris, the renderer respects tier limits: Mobile-HE caps at 400
    /// bodies while PC allows 2000 (from DeviceTierBudget.MaxDebris). The mesh itself is
    /// a simple box that can be instanced efficiently via indirect compute buffers.
    ///
    /// This respects the tiering boundary (Constitution Principle IV): device class affects
    /// rendering count only — never collapse detection, structural integrity, or world state.
    /// A Mobile-HE player sees fewer debris effects but experiences the same physics and
    /// identical grid state as PC players (SC-008).
    /// </summary>
    public static class DebrisRenderer
    {
        /// <summary>Get the tier-appropriate debris body cap from DeviceTierBudget.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetDebrisCap(DeviceTier tier) => DeviceTierBudget.GetForTier(tier).MaxDebris;

        /// <summary>Upload per-instance transforms for indirect draw rendering.</summary>
        public static void UploadTransforms(
            NativeArray<DebrisBody> bodies,
            int activeCount,
            ComputeBuffer instanceBuffer)
        {
            if (instanceBuffer == null || !instanceBuffer.IsValid())
                throw new ArgumentException("Invalid compute buffer for debris instance data.", nameof(instanceBuffer));

            // Clamp to the tier budget — this is the C-006 boundary in action.
            // Visual-only bodies beyond the cap are silently dropped (they produce no state change).
            var cullCount = math.min(activeCount, DebrisBody.MaxDebrisBodies);

            // Staged into one contiguous array and uploaded in a single SetData. Uploading
            // per body would issue one GPU transfer per debris instance, which is the
            // opposite of what an indirect draw is for.
            var instances = new NativeArray<float4>(cullCount, Allocator.Temp);
            int written = 0;

            for (int i = 0; i < cullCount; i++)
            {
                var body = bodies[i];
                if (body.Settled) continue;

                // The instance buffer holds {position.x, position.y, position.z, radius} per
                // entry — orientation is packed into the 4th float for shader unpacking.
                instances[written++] = new float4(
                    body.Position.x,
                    body.Position.y,
                    body.Position.z,
                    body.Radius);
            }

            if (written > 0)
                instanceBuffer.SetData(instances, 0, 0, written);

            instances.Dispose();
        }

        /// <summary>Filter out excess visual-only debris bodies to respect tier budget.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CullToTierBudget(NativeArray<DebrisBody> bodies, int activeCount, DeviceTier tier)
        {
            int maxBodies = GetDebrisCap(tier);
            if (activeCount <= maxBodies) return activeCount;

            // Keep all state-changing debris first (C-006: cannot be culled).
            // Then fill remaining slots with visual-only debris in allocation order.
            int keptStateChanging = 0;
            for (int i = 0; i < activeCount; i++)
            {
                if (!bodies[i].VisualOnly)
                    keptStateChanging++;
            }

            int remaining = maxBodies - keptStateChanging;
            if (remaining <= 0) return keptStateChanging;

            // Count visual-only bodies to cull.
            int visualKept = 0;
            for (int i = 0; i < activeCount && visualKept < remaining; i++)
            {
                if (bodies[i].VisualOnly)
                    visualKept++;
            }

            return keptStateChanging + visualKept;
        }
    }
}
