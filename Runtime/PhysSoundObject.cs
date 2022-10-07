using UnityEngine;
using System.Collections.Generic;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object")]
    public partial class PhysSoundObject : PhysSoundObjectBase
    {
        public List<PhysSoundAudioContainer> AudioContainers = new List<PhysSoundAudioContainer>();
        private Dictionary<int, PhysSoundAudioContainer> _audioContainersDic;

        bool breakOnCollisionStay;
        static Transform mainCamera;
        float distanceToMainCamera;
        bool alertNotInSoundZone; // if sound listener not in sound zone, than stop all Collision events

        // optimization for OnCollisionStay(), skip after maxStep steps
        byte count;
        float maxStep = 2.0f;

        /// <summary>
        /// Initializes the PhysSoundObject. Use this if you adding a PhysSoundObject component to an object at runtime.
        /// </summary>
        public override void Initialize()
        {
#if PHYS_SOUND_3D
            _r = GetComponent<Rigidbody>();
#endif
#if PHYS_SOUND_3D
            _r2D = GetComponent<Rigidbody2D>();
#endif
            if (AutoCreateSources)
            {
                baseImpactVol = ImpactAudio.volume;
                baseImpactPitch = ImpactAudio.pitch;

                _audioContainersDic = new Dictionary<int, PhysSoundAudioContainer>();
                AudioContainers = new List<PhysSoundAudioContainer>();

                foreach (PhysSoundAudioSet audSet in SoundMaterial.AudioSets)
                {
                    if (audSet.Slide == null)
                        continue;

                    PhysSoundAudioContainer audCont = new PhysSoundAudioContainer(audSet.Key);
                    audCont.SlideAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(ImpactAudio, this.gameObject);

                    audCont.Initialize(this);
                    _audioContainersDic.Add(audCont.KeyIndex, audCont);
                    AudioContainers.Add(audCont);
                }

                ImpactAudio.loop = false;
            }
            else
            {
                if (ImpactAudio)
                {
                    ImpactAudio.loop = false;
                    baseImpactVol = ImpactAudio.volume;
                    baseImpactPitch = ImpactAudio.pitch;
                }

                if (AudioContainers.Count > 0)
                {
                    _audioContainersDic = new Dictionary<int, PhysSoundAudioContainer>();

                    foreach (PhysSoundAudioContainer audCont in AudioContainers)
                    {
                        if (!SoundMaterial.HasAudioSet(audCont.KeyIndex))
                        {
                            Debug.LogError("PhysSound Object " + gameObject.name +
                                           " has an audio container for an invalid Material Type! Select this object in the hierarchy to update its audio container list.");

                            continue;
                        }

                        if (PlayClipAtPoint)
                            audCont.Initialize(this, ImpactAudio);
                        else
                            audCont.Initialize(this);

                        _audioContainersDic.Add(audCont.KeyIndex, audCont);
                    }
                }
            }

            if (PlayClipAtPoint)
                PhysSoundTempAudioPool.Create();
            else if (ImpactAudio != null && !ImpactAudio.isActiveAndEnabled)
                ImpactAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(ImpactAudio, gameObject);

            // @todo - menu in editor for choose camera
            mainCamera = GameObject.Find("Main Camera").transform;
            maxStep = Mathf.Round(Random.Range(2.0f, 4.0f));
            //Debug.Log(maxStep);
        }

        void Update()
        {
            if (SoundMaterial == null)
                return;

            for (int i = 0; i < AudioContainers.Count; i++)
                AudioContainers[i].UpdateVolume();

            if (ImpactAudio && !ImpactAudio.isPlaying)
                ImpactAudio.Stop();

            _kinematicVelocity = (transform.position - _prevPosition) / Time.deltaTime;
            _prevPosition = transform.position;

            _kinematicAngularVelocity = Quaternion.Angle(_prevRotation, transform.rotation) / Time.deltaTime / 45f;
            _prevRotation = transform.rotation;

            if ((1 / Time.unscaledDeltaTime) <
                30 + (maxStep *
                      2)) // if there is too much collision, then the minimum FPS will be " < X", because OnCollisionStay slows everything slows down
                breakOnCollisionStay = true;
            else
                breakOnCollisionStay = false;

            // @todo - distance for each sound source of the Sound Material: impact, slide hard, slide soft
            distanceToMainCamera = Vector3.Distance(mainCamera.position, transform.position);
            if (distanceToMainCamera > GetComponent<AudioSource>().maxDistance)
                alertNotInSoundZone = true;
            else
                alertNotInSoundZone = false;
        }

        /// <summary>
        /// Enables or Disables this script along with its associated AudioSources.
        /// </summary>
        public override void SetEnabled(bool enable)
        {
            if (enable && this.enabled == false)
            {
                for (int i = 0; i < AudioContainers.Count; i++)
                {
                    AudioContainers[i].Enable();
                }

                ImpactAudio.enabled = true;
                this.enabled = true;
            }
            else if (!enable && this.enabled == true)
            {
                if (ImpactAudio)
                {
                    ImpactAudio.Stop();
                    ImpactAudio.enabled = false;
                }

                for (int i = 0; i < AudioContainers.Count; i++)
                {
                    AudioContainers[i].Disable();
                }

                this.enabled = false;
            }
        }

        #region Main Functions

        private void setSlideTargetVolumes(GameObject otherObject, Vector3 relativeVelocity, Vector3 normal,
            Vector3 contactPoint, bool exit)
        {
            //log("Sliding! " + gameObject.name + " against " + otherObject.name + " - Relative Velocity: " + relativeVelocity + ", Normal: " + normal + ", Contact Point: " + contactPoint + ", Exit: " + exit);

            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
            {
                return;
            }

            PhysSoundMaterial m = null;
            PhysSoundBase b = otherObject == null ? null : otherObject.GetComponent<PhysSoundBase>();

            if (b)
            {
#if PHYS_SOUND_TERRAIN
                //Special case for sliding against a terrain
                if (b is PhysSoundTerrain)
                {
                    PhysSoundTerrain terr = b as PhysSoundTerrain;
                    Dictionary<int, PhysSoundComposition> compDic = terr.GetComposition(contactPoint);

                    foreach (PhysSoundAudioContainer c in _audioContainersDic.Values)
                    {
                        PhysSoundComposition comp;
                        float mod = 0;

                        if (compDic.TryGetValue(c.KeyIndex, out comp))
                            mod = comp.GetAverage();

                        c.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit, mod);
                    }

                    return;
                }
                else
                    m = b.GetPhysSoundMaterial(contactPoint);
#endif
            }

            //General cases
            //If the other object has a PhysSound material
            if (m)
            {
                PhysSoundAudioContainer aud;

                if (_audioContainersDic.TryGetValue(m.MaterialTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
                else if (!SoundMaterial.HasAudioSet(m.MaterialTypeKey) && SoundMaterial.FallbackTypeKey != -1 &&
                         _audioContainersDic.TryGetValue(SoundMaterial.FallbackTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
            }
            //If it doesnt we set volumes based on the fallback setting of our material
            else
            {
                PhysSoundAudioContainer aud;

                if (SoundMaterial.FallbackTypeKey != -1 &&
                    _audioContainersDic.TryGetValue(SoundMaterial.FallbackTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
            }
        }

        #endregion


        #region Editor

        public bool HasAudioContainer(int keyIndex)
        {
            foreach (PhysSoundAudioContainer aud in AudioContainers)
            {
                if (aud.CompareKeyIndex(keyIndex))
                    return true;
            }

            return false;
        }

        public void AddAudioContainer(int keyIndex)
        {
            AudioContainers.Add(new PhysSoundAudioContainer(keyIndex));
        }

        public void RemoveAudioContainer(int keyIndex)
        {
            for (int i = 0; i < AudioContainers.Count; i++)
            {
                if (AudioContainers[i].KeyIndex == keyIndex)
                {
                    AudioContainers.RemoveAt(i);
                    return;
                }
            }
        }

        #endregion
    }
}