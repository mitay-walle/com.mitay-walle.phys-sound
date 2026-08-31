#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D && PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
using UnityEngine;

namespace PhysSound
{
    internal struct PhysSoundContactData2D
    {
        internal Collider2D FirstCollider;
        internal Collider2D SecondCollider;
        internal Vector3 Position;
        internal Vector3 Normal;
        internal Vector3 RelativeVelocity;
        internal float Impulse;
    }
}
#endif
