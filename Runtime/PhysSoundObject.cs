#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Phys Sound/Phys Sound Object")]
    public sealed class PhysSoundObject : MonoBehaviour
    {
        private PhysSoundContinuousContactReceiver _continuousReceiver;

        private void OnCollisionEnter(Collision collision)
        {
            if (!PhysSoundRuntime.ReportComponentEnter(this, collision, out ulong pairKey))
            {
                return;
            }

            if (_continuousReceiver == null)
            {
                _continuousReceiver = gameObject.AddComponent<PhysSoundContinuousContactReceiver>();
                _continuousReceiver.Initialize(this);
            }

            _continuousReceiver.Track(pairKey);
        }

        private void OnDisable()
        {
            if (_continuousReceiver != null)
            {
                _continuousReceiver.Shutdown();
                _continuousReceiver = null;
            }
        }

        internal void Detach(PhysSoundContinuousContactReceiver receiver)
        {
            if (_continuousReceiver == receiver)
            {
                _continuousReceiver = null;
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class PhysSoundContinuousContactReceiver : MonoBehaviour
    {
        private readonly HashSet<ulong> _pairKeys = new HashSet<ulong>();

        private PhysSoundObject _owner;
        private int _ownerId;
        private bool _shuttingDown;

        internal void Initialize(PhysSoundObject owner)
        {
            _owner = owner;
            _ownerId = owner.GetInstanceID();
            hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        }

        internal void Track(ulong pairKey)
        {
            _pairKeys.Add(pairKey);
        }

        internal void Shutdown()
        {
            if (_shuttingDown)
            {
                return;
            }

            _shuttingDown = true;
            PhysSoundRuntime.ReportComponentDisabled(_ownerId, _pairKeys);
            _pairKeys.Clear();

            if (_owner != null)
            {
                _owner.Detach(this);
            }

            Destroy(this);
        }

        private void OnCollisionStay(Collision collision)
        {
            if (_shuttingDown ||
                !PhysSoundRuntime.TryGetPairKey(collision, out ulong pairKey) ||
                !_pairKeys.Contains(pairKey))
            {
                return;
            }

            PhysSoundRuntime.ReportComponentStay(_ownerId, pairKey, collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (_shuttingDown ||
                !PhysSoundRuntime.TryGetPairKey(collision, out ulong pairKey) ||
                !_pairKeys.Remove(pairKey))
            {
                return;
            }

            PhysSoundRuntime.ReportComponentExit(_ownerId, pairKey);

            if (_pairKeys.Count == 0)
            {
                Shutdown();
            }
        }

        private void OnDisable()
        {
            if (!_shuttingDown)
            {
                Shutdown();
            }
        }
    }
}
#endif
