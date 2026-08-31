#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using UnityEngine;

namespace PhysSound
{
    internal sealed class PhysSoundLabelAttribute : PropertyAttribute
    {
        internal PhysSoundLabelAttribute(string displayName)
        {
            DisplayName = displayName;
        }

        internal string DisplayName { get; }
    }
}
#endif
