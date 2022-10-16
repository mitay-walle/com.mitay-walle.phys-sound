using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject3DTriggerStay : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_3D
        private void OnTriggerStay(Collider other)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnTriggerStayInternal(other);
        }
#endif
    }
}