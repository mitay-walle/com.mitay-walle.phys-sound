#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System.Collections.Generic;
using UnityEngine;

namespace PhysSound
{
    [CreateAssetMenu(
        fileName = "PhysSoundSubprofile",
        menuName = "Audio/Phys Sound Subprofile")]
    public sealed class PhysSoundSubprofile : ScriptableObject
    {
        [SerializeField] private Dictionary<string, PhysSoundSurface> _surfaces = new();
        [SerializeField] private Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> _interactions = new();

        internal Dictionary<string, PhysSoundSurface> Surfaces => _surfaces;
        internal Dictionary<PhysSoundInteractionKey, PhysSoundInteraction> Interactions => _interactions;
    }
}
#endif
