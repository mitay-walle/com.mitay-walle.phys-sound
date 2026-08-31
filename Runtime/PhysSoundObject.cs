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
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        private readonly HashSet<PhysSoundPairKey> _pairs2D = new();
        private EntityId _ownerId2D;
#endif

        private void OnCollisionEnter(Collision collision)
        {
            if (!PhysSoundRuntime.ReportComponentEnter(this, collision, out PhysSoundPairKey pairKey))
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

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            PhysSoundRuntime.ReportComponentDisabled(_ownerId2D, _pairs2D);
            _pairs2D.Clear();
#endif
        }

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (_ownerId2D == default)
            {
                _ownerId2D = this.GetEntityId();
            }

            if (PhysSoundRuntime.ReportComponentEnter2D(_ownerId2D, collision, out PhysSoundPairKey pairKey))
            {
                _pairs2D.Add(pairKey);
            }
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (PhysSoundRuntime.TryGetPairKey2D(collision, out PhysSoundPairKey pairKey) && _pairs2D.Contains(pairKey))
            {
                PhysSoundRuntime.ReportComponentStay2D(_ownerId2D, pairKey, collision);
            }
        }

        private void OnCollisionExit2D(Collision2D collision)
        {
            if (PhysSoundRuntime.TryGetPairKey2D(collision, out PhysSoundPairKey pairKey) && _pairs2D.Remove(pairKey))
            {
                PhysSoundRuntime.ReportComponentExit(_ownerId2D, pairKey);
            }
        }
#endif

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
        private readonly HashSet<PhysSoundPairKey> _pairKeys = new HashSet<PhysSoundPairKey>();

        private PhysSoundObject _owner;
        private EntityId _ownerId;
        private bool _shuttingDown;

        internal void Initialize(PhysSoundObject owner)
        {
            _owner = owner;
            _ownerId = owner.GetEntityId();
            hideFlags = HideFlags.HideInInspector | HideFlags.DontSave;
        }

        internal void Track(PhysSoundPairKey pairKey)
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
                !PhysSoundRuntime.TryGetPairKey(collision, out PhysSoundPairKey pairKey) ||
                !_pairKeys.Contains(pairKey))
            {
                return;
            }

            PhysSoundRuntime.ReportComponentStay(_ownerId, pairKey, collision);
        }

        private void OnCollisionExit(Collision collision)
        {
            if (_shuttingDown ||
                !PhysSoundRuntime.TryGetPairKey(collision, out PhysSoundPairKey pairKey) ||
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
