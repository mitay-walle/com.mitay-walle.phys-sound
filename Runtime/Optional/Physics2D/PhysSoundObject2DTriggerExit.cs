using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject2DTriggerExit : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_2D
        private void OnTriggerExit2D(Collider2D other)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnTriggerExit2DInternal(other);
        }
#endif
    }
}