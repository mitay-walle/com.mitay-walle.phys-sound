using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    [CreateAssetMenu(menuName = "Phys Sound/Key")]
    public class PhysSoundKey : ScriptableObject
    {
#if PHYS_SOUND_3D
        public List<PhysicMaterial> PhysicMterials = new List<PhysicMaterial>();
#endif
#if PHYS_SOUND_2D
        public List<PhysicsMaterial2D> PhysicMterials2D = new List<PhysicsMaterial2D>();
#endif
    }
}