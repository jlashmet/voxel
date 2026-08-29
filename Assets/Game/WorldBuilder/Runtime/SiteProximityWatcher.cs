using System;
using Game.WorldBuilder.Api;

namespace Game.WorldBuilder.Runtime
{
    /// <summary>
    /// Configurable horizontal proximity authored in world-voxel coordinates. World-space distance
    /// belongs here, before Story: downstream systems receive only the stable semantic site id.
    /// </summary>
    public readonly struct SiteProximityTriggerSpec
    {
        public SiteRef Site { get; }
        public int CentreX { get; }
        public int CentreZ { get; }
        public int Radius { get; }
        public bool OneShot { get; }

        public SiteProximityTriggerSpec(
            SiteRef site,
            int centreX,
            int centreZ,
            int radius,
            bool oneShot = true)
        {
            if (radius <= 0) throw new ArgumentOutOfRangeException(nameof(radius));
            Site = site;
            CentreX = centreX;
            CentreZ = centreZ;
            Radius = radius;
            OneShot = oneShot;
        }
    }

    /// <summary>
    /// Edge-triggered reusable proximity evaluator. It is deterministic, allocation-free after
    /// construction, and O(configured triggers) per update; one-shot triggers stop participating
    /// after they fire.
    /// </summary>
    public sealed class SiteProximityWatcher
    {
        private readonly SiteProximityTriggerSpec[] _triggers;
        private readonly bool[] _inside;
        private readonly bool[] _fired;

        public SiteProximityWatcher(SiteProximityTriggerSpec[] triggers)
        {
            if (triggers == null) throw new ArgumentNullException(nameof(triggers));
            _triggers = (SiteProximityTriggerSpec[])triggers.Clone();
            _inside = new bool[_triggers.Length];
            _fired = new bool[_triggers.Length];
        }

        public int FiredCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < _fired.Length; i++) if (_fired[i]) count++;
                return count;
            }
        }

        public int Update(int worldX, int worldZ, Func<SiteRef, int> onEntered)
        {
            if (onEntered == null) throw new ArgumentNullException(nameof(onEntered));
            int matched = 0;

            for (int i = 0; i < _triggers.Length; i++)
            {
                SiteProximityTriggerSpec trigger = _triggers[i];
                if (trigger.OneShot && _fired[i]) continue;

                long dx = (long)worldX - trigger.CentreX;
                long dz = (long)worldZ - trigger.CentreZ;
                long radius = trigger.Radius;
                bool isInside = dx * dx + dz * dz <= radius * radius;
                bool entered = isInside && !_inside[i];
                _inside[i] = isInside;
                if (!entered) continue;

                matched += onEntered(trigger.Site);
                _fired[i] = true;
            }

            return matched;
        }
    }
}
