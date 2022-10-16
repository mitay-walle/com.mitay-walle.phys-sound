using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject3DCollisionStay : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_3D
        private void OnCollisionStay(Collision collision)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnCollisionStayInternal(collision);
        }
#endif
    }
}