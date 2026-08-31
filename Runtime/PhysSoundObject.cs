#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Phys Sound/Phys Sound Object")]
    public sealed class PhysSoundObject : MonoBehaviour
    {
        [SerializeField] private bool _includeTriggers;

        private PhysSoundContinuousContactReceiver _continuousReceiver;
        private readonly HashSet<PhysSoundPairKey> _triggerPairs = new();
        private Collider[] _triggerColliders;
        private Collider _triggerCollider;
        private EntityId _ownerId;
#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
        private readonly HashSet<PhysSoundPairKey> _pairs2D = new();
        private readonly HashSet<PhysSoundPairKey> _triggerPairs2D = new();
        private Collider2D[] _triggerColliders2D;
        private Collider2D _triggerCollider2D;
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

            PhysSoundRuntime.ReportComponentDisabled(_ownerId, _triggerPairs);
            _triggerPairs.Clear();

#if PHYS_SOUND_2D && !PHYS_SOUND_DISABLE_2D
            PhysSoundRuntime.ReportComponentDisabled(_ownerId2D, _pairs2D);
            PhysSoundRuntime.ReportComponentDisabled(_ownerId2D, _triggerPairs2D);
            _pairs2D.Clear();
            _triggerPairs2D.Clear();
#endif
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isActiveAndEnabled || !_includeTriggers || other == null || !TryGetTriggerCollider(other, out Collider ownCollider))
            {
                return;
            }

            if (_ownerId == default)
            {
                _ownerId = this.GetEntityId();
            }

            if (PhysSoundRuntime.ReportTriggerEnter(_ownerId, ownCollider, other, out PhysSoundPairKey pairKey))
            {
                _triggerPairs.Add(pairKey);
            }
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isActiveAndEnabled || !_includeTriggers || other == null || !TryGetTriggerCollider(other, out Collider ownCollider))
            {
                return;
            }

            PhysSoundPairKey pairKey = PhysSoundRuntime.GetPairKey(ownCollider, other);
            if (_triggerPairs.Contains(pairKey))
            {
                PhysSoundRuntime.ReportTriggerStay(_ownerId, pairKey, ownCollider, other);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || !TryGetTriggerCollider(other, out Collider ownCollider))
            {
                return;
            }

            PhysSoundPairKey pairKey = PhysSoundRuntime.GetPairKey(ownCollider, other);
            if (_triggerPairs.Remove(pairKey))
            {
                PhysSoundRuntime.ReportComponentExit(_ownerId, pairKey);
            }
        }

        private bool TryGetTriggerCollider(Collider other, out Collider triggerCollider)
        {
            _triggerColliders ??= GetComponentsInChildren<Collider>();
            Collider previousCollider = _triggerCollider;
            _triggerCollider = null;
            for (int i = 0; i < _triggerColliders.Length; i++)
            {
                Collider candidate = _triggerColliders[i];
                if (candidate != null && candidate.enabled && (candidate.isTrigger || other.isTrigger) && candidate.bounds.Intersects(other.bounds))
                {
                    _triggerCollider = candidate;
                    break;
                }
            }

            if (_triggerCollider == null && previousCollider != null && previousCollider.enabled && (previousCollider.isTrigger || other.isTrigger))
            {
                _triggerCollider = previousCollider;
            }

            triggerCollider = _triggerCollider;
            return triggerCollider != null;
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

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!isActiveAndEnabled || !_includeTriggers || other == null || !TryGetTriggerCollider2D(other, out Collider2D ownCollider))
            {
                return;
            }

            if (_ownerId2D == default)
            {
                _ownerId2D = this.GetEntityId();
            }

            if (PhysSoundRuntime.ReportTriggerEnter2D(_ownerId2D, ownCollider, other, out PhysSoundPairKey pairKey))
            {
                _triggerPairs2D.Add(pairKey);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!isActiveAndEnabled || !_includeTriggers || other == null || !TryGetTriggerCollider2D(other, out Collider2D ownCollider))
            {
                return;
            }

            PhysSoundPairKey pairKey = PhysSoundRuntime.GetPairKey(ownCollider, other);
            if (_triggerPairs2D.Contains(pairKey))
            {
                PhysSoundRuntime.ReportTriggerStay2D(_ownerId2D, pairKey, ownCollider, other);
            }
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null || !TryGetTriggerCollider2D(other, out Collider2D ownCollider))
            {
                return;
            }

            PhysSoundPairKey pairKey = PhysSoundRuntime.GetPairKey(ownCollider, other);
            if (_triggerPairs2D.Remove(pairKey))
            {
                PhysSoundRuntime.ReportComponentExit(_ownerId2D, pairKey);
            }
        }

        private bool TryGetTriggerCollider2D(Collider2D other, out Collider2D triggerCollider)
        {
            _triggerColliders2D ??= GetComponentsInChildren<Collider2D>();
            Collider2D previousCollider = _triggerCollider2D;
            _triggerCollider2D = null;
            for (int i = 0; i < _triggerColliders2D.Length; i++)
            {
                Collider2D candidate = _triggerColliders2D[i];
                if (candidate != null && candidate.enabled && (candidate.isTrigger || other.isTrigger) && candidate.bounds.Intersects(other.bounds))
                {
                    _triggerCollider2D = candidate;
                    break;
                }
            }

            if (_triggerCollider2D == null && previousCollider != null && previousCollider.enabled && (previousCollider.isTrigger || other.isTrigger))
            {
                _triggerCollider2D = previousCollider;
            }

            triggerCollider = _triggerCollider2D;
            return triggerCollider != null;
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
