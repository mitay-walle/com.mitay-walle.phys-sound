#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEngine;

namespace PhysSound
{
    internal sealed class PhysSoundMinMaxAttribute : PropertyAttribute
    {
        internal PhysSoundMinMaxAttribute(float minimum, float maximum, string displayName = null)
        {
            Minimum = minimum;
            Maximum = maximum;
            DisplayName = displayName;
        }

        internal PhysSoundMinMaxAttribute(
            string maximumPropertyName,
            float minimum,
            float maximum,
            string displayName = null)
        {
            MaximumPropertyName = maximumPropertyName;
            Minimum = minimum;
            Maximum = maximum;
            DisplayName = displayName;
        }

        internal string DisplayName { get; }
        internal string MaximumPropertyName { get; }
        internal float Minimum { get; }
        internal float Maximum { get; }
    }
}
#endif
