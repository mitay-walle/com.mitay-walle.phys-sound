#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEngine;

namespace PhysSound
{
    internal sealed class PhysSoundTwoPointCurveAttribute : PropertyAttribute
    {
        internal PhysSoundTwoPointCurveAttribute(float minimum, float maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        internal float Minimum { get; }
        internal float Maximum { get; }
    }
}
#endif
