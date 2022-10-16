using UnityEngine;

namespace PhysSound
{
    [RequireComponent(typeof(PhysSoundObject)), DisallowMultipleComponent]
    public class PhysSoundObject2DCollisionExit : MonoBehaviour
    {
        private PhysSoundObject _physSoundObject;
#if PHYS_SOUND_2D
        private void OnCollisionExit2D(Collision2D collision)
        {
            _physSoundObject = _physSoundObject ? _physSoundObject : GetComponent<PhysSoundObject>();
            _physSoundObject.OnCollisionExit2DInternal(collision);
        }
#endif
    }
}