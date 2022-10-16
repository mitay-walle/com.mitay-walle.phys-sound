using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject2DTriggerStay : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_2D
        private void OnTriggerStay2D(Collider2D other)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnTriggerStay2DInternal(other);
        }
#endif
    }
}