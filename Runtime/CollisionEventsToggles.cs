using System;

namespace PhysSound
{
    [Serializable]
    public class CollisionEventsToggles
    {
        public bool PlayOnCollisionEnter;
        public bool PlayOnCollisionStay;
        public bool SlideOnCollisionStay;
        public bool SlideOnCollisionExit;
        public bool PlayOnTriggerEnter;
        public bool SlideOnTriggerStay;
        public bool SlideOnTriggerExit;

        public CollisionEventsToggles()
        {
            PlayOnCollisionEnter = true;
            PlayOnCollisionStay = false;
            SlideOnCollisionStay = true;
            SlideOnCollisionExit = true;
            PlayOnTriggerEnter = true;
            SlideOnTriggerStay = true;
            SlideOnTriggerExit = true;
        }
    }
}