#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEngine;

namespace PhysSound
{
    internal sealed class PhysSoundMinMaxAttribute : PropertyAttribute
    {
        internal PhysSoundMinMaxAttribute(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        internal PhysSoundMinMaxAttribute(string maximumPropertyName, float minimum, float maximum)
        {
            MaximumPropertyName = maximumPropertyName;
            Minimum = minimum;
            Maximum = maximum;
        }

        internal string MaximumPropertyName { get; }
        internal float Minimum { get; }
        internal float Maximum { get; }
    }
}
#endif
