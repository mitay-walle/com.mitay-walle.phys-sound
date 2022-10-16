using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject3DCollisionExit : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_3D
        private void OnCollisionExit(Collision collision)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnCollisionExitInternal(collision);
        }
#endif
    }
}