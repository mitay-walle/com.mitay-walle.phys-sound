using UnityEngine;
using System.Collections.Generic;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object")]
    public class PhysSoundObject : PhysSoundObjectBase
    {
        public List<PhysSoundAudioContainer> AudioContainers = new List<PhysSoundAudioContainer>();
        private Dictionary<int, PhysSoundAudioContainer> _audioContainersDic;

        bool breakOnCollisionStay;
        static Transform mainCamera;
        float distanceToMainCamera;
        bool alertNotInSoundZone;                               // if sound listener not in sound zone, than stop all Collision events
        
        // optimization for OnCollisionStay(), skip after maxStep steps
        byte count;
        float maxStep = 2.0f;

        /// <summary>
        /// Initializes the PhysSoundObject. Use this if you adding a PhysSoundObject component to an object at runtime.
        /// </summary>
        public override void Initialize()
        {
            _r = GetComponent<Rigidbody>();
            _r2D = GetComponent<Rigidbody2D>();

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
                            Debug.LogError("PhysSound Object " + gameObject.name + " has an audio container for an invalid Material Type! Select this object in the hierarchy to update its audio container list.");
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

            if ((1 / Time.unscaledDeltaTime) < 30+(maxStep*2))                     // if there is too much collision, then the minimum FPS will be " < X", because OnCollisionStay slows everything slows down
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

        private void setSlideTargetVolumes(GameObject otherObject, Vector3 relativeVelocity, Vector3 normal, Vector3 contactPoint, bool exit)
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
            }

            //General cases
            //If the other object has a PhysSound material
            if (m)
            {
                PhysSoundAudioContainer aud;

                if (_audioContainersDic.TryGetValue(m.MaterialTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
                else if (!SoundMaterial.HasAudioSet(m.MaterialTypeKey) && SoundMaterial.FallbackTypeKey != -1 && _audioContainersDic.TryGetValue(SoundMaterial.FallbackTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
            }
            //If it doesnt we set volumes based on the fallback setting of our material
            else
            {
                PhysSoundAudioContainer aud;

                if (SoundMaterial.FallbackTypeKey != -1 && _audioContainersDic.TryGetValue(SoundMaterial.FallbackTypeKey, out aud))
                    aud.SetTargetVolumeAndPitch(this.gameObject, otherObject, relativeVelocity, normal, exit);
            }
        }

        #endregion

        #region 3D Collision Messages
        // optimization: c.contacts[0] -> GetContact(0). You should avoid using this as it produces memory garbage. Use GetContact or GetContacts instead (from official documentation)

        Vector3 contactNormal, contactPoint, relativeVelocity;

        void OnCollisionEnter(Collision c)
        {
            if (alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 )
                return;

            contactNormal = c.GetContact(0).normal;
            contactPoint = c.GetContact(0).point;
            relativeVelocity = c.relativeVelocity;

            playImpactSound(c.collider.gameObject, relativeVelocity, contactNormal, contactPoint);

            _setPrevVelocity = true;
        }

        void OnCollisionStay(Collision c)
        {
            if (alertNotInSoundZone)
                return;

            count++;
            if (count >= maxStep)
            {
                count = 0;
                return;
            }

            if (breakOnCollisionStay || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null)
                return;

            if (_setPrevVelocity)
            {
                _prevVelocity = _r.velocity;
                _setPrevVelocity = false;
            }
            
            Vector3 deltaVel = _r.velocity - _prevVelocity;

            if (c.contactCount > 0)
            {
                contactNormal = c.GetContact(0).normal;
                contactPoint = c.GetContact(0).point;
            }

            relativeVelocity = c.relativeVelocity;

            playImpactSound(c.collider.gameObject, deltaVel, contactNormal, contactPoint);
            setSlideTargetVolumes(c.collider.gameObject, relativeVelocity, contactNormal, contactPoint, false);

            _prevVelocity = _r.velocity;
        }

        void OnCollisionExit(Collision c)
        {
            if (alertNotInSoundZone || SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null )
                return;

            setSlideTargetVolumes(c.collider.gameObject, Vector3.zero, Vector3.zero, transform.position, true);
            _setPrevVelocity = true;
        }

        #endregion

        #region 3D Trigger Messages

        void OnTriggerEnter(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || !HitsTriggers)
                return;

            playImpactSound(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position);
        }

        void OnTriggerStay(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null || !HitsTriggers)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, false);
        }

        void OnTriggerExit(Collider c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null || !HitsTriggers)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
        }

        #endregion

        #region 2D Collision Messages

        void OnCollisionEnter2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            playImpactSound(c.collider.gameObject, c.relativeVelocity, c.contacts[0].normal, c.contacts[0].point);

            _setPrevVelocity = true;
        }

        void OnCollisionStay2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null)
                return;

            if (_setPrevVelocity)
            {
                _prevVelocity = _r2D.velocity;
                _setPrevVelocity = false;
            }

            Vector3 deltaVel = _r2D.velocity - (Vector2)_prevVelocity;

            playImpactSound(c.collider.gameObject, deltaVel, c.contacts[0].normal, c.contacts[0].point);
            setSlideTargetVolumes(c.collider.gameObject, c.relativeVelocity, c.contacts[0].normal, c.contacts[0].point, false);

            _prevVelocity = _r2D.velocity;
        }

        void OnCollisionExit2D(Collision2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.collider.gameObject, c.relativeVelocity, Vector3.up, transform.position, true);

            _setPrevVelocity = true;
        }

        #endregion

        #region 2D Trigger Messages

        void OnTriggerEnter2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0)
                return;

            playImpactSound(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position);
        }

        void OnTriggerStay2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, false);
        }

        void OnTriggerExit2D(Collider2D c)
        {
            if (SoundMaterial == null || !this.enabled || SoundMaterial.AudioSets.Count == 0 || _audioContainersDic == null)
                return;

            setSlideTargetVolumes(c.gameObject, TotalKinematicVelocity, Vector3.zero, c.transform.position, true);
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

    [System.Serializable]
    public class PhysSoundAudioContainer
    {
        public int KeyIndex;
        public AudioSource SlideAudio;

        private PhysSoundObject physSoundObject;
        private float _targetVolume;
        private float _baseVol, _basePitch, _basePitchRand;

        private int _lastFrame;
        private bool _lastExit;

        private AudioSource currAudioSource;

        private PhysSoundMaterial soundMaterial
        {
            get { return physSoundObject.SoundMaterial; }
        }

        public PhysSoundAudioContainer(int k)
        {
            KeyIndex = k;
        }

        /// <summary>
        /// Initializes this Audio Container for no audio pooling. Will do nothing if SlideAudio is not assigned.
        /// </summary>
        public void Initialize(PhysSoundObject obj)
        {
            if (SlideAudio == null)
                return;

            physSoundObject = obj;

            _baseVol = SlideAudio.volume;
            _basePitch = SlideAudio.pitch;
            _basePitchRand = _basePitch;

            SlideAudio.clip = soundMaterial.GetAudioSet(KeyIndex).Slide;
            SlideAudio.loop = true;
            SlideAudio.volume = 0;

            currAudioSource = SlideAudio;
        }

        /// <summary>
        /// Initializes this Audio Container for audio pooling.
        /// </summary>
        public void Initialize(PhysSoundObject obj, AudioSource template)
        {
            physSoundObject = obj;
            SlideAudio = template;

            _baseVol = template.volume;
            _basePitch = template.pitch;
            _basePitchRand = _basePitch;
        }

        /// <summary>
        /// Sets the target volume and pitch of the sliding sound effect based on the given object that was hit, velocity, and normal.
        /// </summary>
        public void SetTargetVolumeAndPitch(GameObject parentObject, GameObject otherObject, Vector3 relativeVelocity, Vector3 normal, bool exit, float mod = 1)
        {
            if (SlideAudio == null)
                return;

            float vol = exit || !soundMaterial.CollideWith(otherObject) ? 0 : soundMaterial.GetSlideVolume(relativeVelocity, normal) * _baseVol * mod;

            if (_lastFrame == Time.frameCount)
            {
                if (_lastExit != exit || _targetVolume < vol)
                    _targetVolume = vol;
            }
            else
                _targetVolume = vol;

            if (physSoundObject.PlayClipAtPoint && currAudioSource == null && _targetVolume > 0.001f)
            {
                _basePitchRand = _basePitch * soundMaterial.GetScaleModPitch(parentObject.transform.localScale) + soundMaterial.GetRandomPitch();
                currAudioSource = PhysSoundTempAudioPool.Instance.GetSource(SlideAudio);

                if (currAudioSource)
                {
                    currAudioSource.clip = soundMaterial.GetAudioSet(KeyIndex).Slide;
                    currAudioSource.volume = _targetVolume;
                    currAudioSource.loop = true;
                    currAudioSource.Play();
                }
            }
            else if (currAudioSource && !currAudioSource.isPlaying)
            {
                _basePitchRand = _basePitch * soundMaterial.GetScaleModPitch(parentObject.transform.localScale) + soundMaterial.GetRandomPitch();
                currAudioSource.loop = true;
                currAudioSource.volume = _targetVolume;
                currAudioSource.Play();
            }

            if (currAudioSource)
                currAudioSource.pitch = _basePitchRand + relativeVelocity.magnitude * soundMaterial.SlidePitchMod;

            if (soundMaterial.TimeScalePitch)
                currAudioSource.pitch *= Time.timeScale;

            _lastExit = exit;
            _lastFrame = Time.frameCount;
        }

        /// <summary>
        /// Updates the associated AudioSource to match the target volume and pitch.
        /// </summary>
        public void UpdateVolume()
        {
            if (SlideAudio == null || currAudioSource == null)
                return;

            currAudioSource.transform.position = physSoundObject.transform.position;
            currAudioSource.volume = Mathf.MoveTowards(currAudioSource.volume, _targetVolume, 0.1f);

            if (currAudioSource.volume < 0.001f)
            {
                if (physSoundObject.PlayClipAtPoint)
                {
                    PhysSoundTempAudioPool.Instance.ReleaseSource(currAudioSource);
                    currAudioSource = null;
                }
                else
                {
                    currAudioSource.Stop();
                }
            }
        }

        /// <summary>
        /// Returns true if this Audio Container's key index is the same as the given key index.
        /// </summary>
        public bool CompareKeyIndex(int k)
        {
            return k == KeyIndex;
        }

        /// <summary>
        /// Disables the associated AudioSource.
        /// </summary>
        public void Disable()
        {
            if (SlideAudio)
            {
                SlideAudio.Stop();
                SlideAudio.enabled = false;
            }
        }

        /// <summary>
        /// Enables the associated AudioSource.
        /// </summary>
        public void Enable()
        {
            if (SlideAudio)
            {
                SlideAudio.enabled = true;
            }
        }
    }
}
