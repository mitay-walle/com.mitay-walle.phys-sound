using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject2DCollisionStay : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_2D
        private void OnCollisionStay2D(Collision2D collision)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnCollisionStay2DInternal(collision);
        }
#endif
    }
}