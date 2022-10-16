using UnityEngine;
using System.Collections.Generic;
using PhysSound.Optional.Terrains;

namespace PhysSound
{
    [AddComponentMenu("PhysSound/PhysSound Object")]
    public partial class PhysSoundObject : PhysSoundObjectBase
    {
        private static Transform _mainCamera;

        public List<PhysSoundAudioContainer> AudioContainers = new List<PhysSoundAudioContainer>();
        protected Dictionary<PhysSoundKey, PhysSoundAudioContainer> _audioContainersMap;

        protected bool _alertNotInSoundZone; // if sound listener not in sound zone, than stop all Collision events
        protected bool _breakOnCollisionStay;
        protected float _distanceToMainCamera;

        // optimization for OnCollisionStay(), skip after maxStep steps
        protected float _maxStep = 2;
        protected byte _count;

        /// <summary>
        /// Initializes the PhysSoundObject. Use this if you adding a PhysSoundObject component to an object at runtime.
        /// </summary>
        override protected void Initialize()
        {
#if PHYS_SOUND_3D
            _r = GetComponent<Rigidbody>();
            if (_r)
            {
                ValidateComponent<PhysSoundObject3DTriggerExit>(_collisionEvents.SlideOnTriggerExit);
                ValidateComponent<PhysSoundObject3DTriggerStay>(_collisionEvents.SlideOnTriggerStay);
                ValidateComponent<PhysSoundObject3DCollisionExit>(_collisionEvents.SlideOnCollisionExit);
                ValidateComponent<PhysSoundObject3DCollisionStay>(_collisionEvents.PlayOnCollisionStay ||
                                                                  _collisionEvents.SlideOnCollisionStay);
            }

#endif
#if PHYS_SOUND_2D
            _r2D = GetComponent<Rigidbody2D>();
            if (_r2D)
            {
                ValidateComponent<PhysSoundObject2DTriggerExit>(_collisionEvents.SlideOnTriggerExit);
                ValidateComponent<PhysSoundObject2DTriggerStay>(_collisionEvents.SlideOnTriggerStay);
                ValidateComponent<PhysSoundObject2DCollisionExit>(_collisionEvents.SlideOnCollisionExit);
                ValidateComponent<PhysSoundObject2DCollisionStay>(_collisionEvents.PlayOnCollisionStay ||
                                                                  _collisionEvents.SlideOnCollisionStay);
            }

#endif
            if (_autoCreateSources)
            {
                _baseImpactVol = _impactAudio.volume;
                _baseImpactPitch = _impactAudio.pitch;

                _audioContainersMap = new Dictionary<PhysSoundKey, PhysSoundAudioContainer>();
                AudioContainers = new List<PhysSoundAudioContainer>();

                foreach (PhysSoundAudioSet audSet in SoundMaterial.AudioSets)
                {
                    if (audSet.Slide == null)
                        continue;

                    PhysSoundAudioContainer audCont = new PhysSoundAudioContainer(audSet.Key);
                    audCont.SlideAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(_impactAudio, gameObject);

                    audCont.Initialize(this);
                    _audioContainersMap.Add(audCont.Key, audCont);
                    AudioContainers.Add(audCont);
                }

                _impactAudio.loop = false;
            }
            else
            {
                if (_impactAudio)
                {
                    _impactAudio.loop = false;
                    _baseImpactVol = _impactAudio.volume;
                    _baseImpactPitch = _impactAudio.pitch;
                }

                if (AudioContainers.Count > 0)
                {
                    _audioContainersMap = new Dictionary<PhysSoundKey, PhysSoundAudioContainer>();

                    foreach (PhysSoundAudioContainer audCont in AudioContainers)
                    {
                        if (!SoundMaterial.HasAudioSet(audCont.Key))
                        {
                            Debug.LogError(
                                $"[ {GetType().Name} ] '{name}' has invalid PhysSoundKey '{audCont.Key?.name}'",
                                this);

                            continue;
                        }

                        if (PlayClipAtPoint)
                            audCont.Initialize(this, _impactAudio);
                        else
                            audCont.Initialize(this);

                        _audioContainersMap.Add(audCont.Key, audCont);
                    }
                }
            }

            if (PlayClipAtPoint)
                PhysSoundTempAudioPool.Create();
            else if (_impactAudio != null && !_impactAudio.isActiveAndEnabled)
                _impactAudio = PhysSoundTempAudioPool.GetAudioSourceCopy(_impactAudio, gameObject);

            _mainCamera = _mainCamera ? _mainCamera : GetCameraTransform();
            _maxStep = Mathf.Round(Random.Range(2f, 4f));
        }

        protected virtual Transform GetCameraTransform()
        {
            Camera cameraMain = Camera.main;
            if (cameraMain != null) return cameraMain.transform;
            return null;
        }

        private void FixedUpdate()
        {
            if (SoundMaterial == null)
                return;

            for (int i = 0; i < AudioContainers.Count; i++)
                AudioContainers[i].UpdateVolume();

            if (_impactAudio && !_impactAudio.isPlaying)
                _impactAudio.Stop();

            _kinematicVelocity = (transform.position - _prevPosition) / Time.deltaTime;
            _prevPosition = transform.position;

            _kinematicAngularVelocity = Quaternion.Angle(_prevRotation, transform.rotation) / Time.deltaTime / 45f;
            _prevRotation = transform.rotation;

            // if there is too much collision, then the minimum FPS will be " < X", because OnCollisionStay slows everything down
            _breakOnCollisionStay = 1 / Time.unscaledDeltaTime < 30 + _maxStep * 2;

            DistanceCheck();
        }

        private void DistanceCheck()
        {
            AudioSource source = _impactAudio;
            if (!source)
            {
                for (int i = 0; i < AudioContainers.Count; i++)
                {
                    if (!AudioContainers[i].SlideAudio) continue;
                    source = AudioContainers[i].SlideAudio;
                    break;
                }
            }

            if (!source) return;

            float sqrDistance = source.maxDistance * source.maxDistance;

            _distanceToMainCamera = Vector3.SqrMagnitude(_mainCamera.position - transform.position);
            _alertNotInSoundZone = _distanceToMainCamera > sqrDistance;
        }

        /// <summary>
        /// Enables or Disables this script along with its associated AudioSources.
        /// </summary>
        public override void SetEnabled(bool enable)
        {
            if (enable && enabled == false)
            {
                for (int i = 0; i < AudioContainers.Count; i++)
                {
                    AudioContainers[i].Enable();
                }

                _impactAudio.enabled = true;
                enabled = true;
            }
            else if (!enable && enabled == true)
            {
                if (_impactAudio)
                {
                    _impactAudio.Stop();
                    _impactAudio.enabled = false;
                }

                for (int i = 0; i < AudioContainers.Count; i++)
                {
                    AudioContainers[i].Disable();
                }

                enabled = false;
            }
        }

        #region Main Functions

        private void SetSlideTargetVolumes(GameObject otherObject, Vector3 relativeVelocity, Vector3 normal,
            Vector3 contactPoint, bool exit)
        {
            //log("Sliding! " + gameObject.name + " against " + otherObject.name + " - Relative Velocity: " + relativeVelocity + ", Normal: " + normal + ", Contact Point: " + contactPoint + ", Exit: " + exit);

            if (SoundMaterial == null || !enabled || SoundMaterial.AudioSets.Count == 0)
            {
                return;
            }

            PhysSoundMaterial material = null;
            PhysSoundBase component = otherObject == null ? null : otherObject.GetComponent<PhysSoundBase>();

            if (component)
            {
#if PHYS_SOUND_TERRAIN
                //Special case for sliding against a terrain
                if (component is PhysSoundTerrain)
                {
                    PhysSoundTerrain terr = component as PhysSoundTerrain;
                    var compDic = terr.GetComposition(contactPoint);

                    foreach (PhysSoundAudioContainer c in _audioContainersMap.Values)
                    {
                        PhysSoundComposition comp;
                        float mod = 0;

                        if (compDic.TryGetValue(c.Key, out comp))
                            mod = comp.GetAverage();

                        c.SetTargetVolumeAndPitch(gameObject, otherObject, relativeVelocity, normal, exit, mod);
                    }

                    return;
                }
                else
                    material = component.GetPhysSoundMaterial(contactPoint);
#endif
            }

            //If the other object has a PhysSound material
            if (material)
            {
                if (_audioContainersMap.TryGetValue(material.MaterialTypeKey, out PhysSoundAudioContainer aud))
                {
                    aud.SetTargetVolumeAndPitch(gameObject, otherObject, relativeVelocity, normal, exit);
                }
                else
                {
                    if (!SoundMaterial.HasAudioSet(material.MaterialTypeKey) && SoundMaterial.Fallback != null &&
                        _audioContainersMap.TryGetValue(SoundMaterial.Fallback?.MaterialTypeKey, out aud))
                    {
                        aud.SetTargetVolumeAndPitch(gameObject, otherObject, relativeVelocity, normal, exit);
                    }
                }
            }
            else
            {
                //If it doesnt we set volumes based on the fallback setting of our material

                if (SoundMaterial.Fallback != null &&
                    _audioContainersMap.TryGetValue(SoundMaterial.Fallback?.MaterialTypeKey,
                        out PhysSoundAudioContainer aud))
                {
                    aud.SetTargetVolumeAndPitch(gameObject, otherObject, relativeVelocity, normal, exit);
                }
            }
        }

        #endregion

        private void ValidateComponent<T>(bool needComponent) where T : Component
        {
            if (needComponent)
            {
                if (!GetComponent<T>())
                {
                    gameObject.AddComponent<T>();
                }
            }
        }
    }
}