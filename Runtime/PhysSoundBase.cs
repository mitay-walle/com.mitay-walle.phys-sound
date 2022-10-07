using UnityEngine;

namespace PhysSound
{
    public abstract class PhysSoundBase : MonoBehaviour
    {
        public abstract PhysSoundMaterial GetPhysSoundMaterial(Vector3 contactPoint);
    }
}
