#if PHYS_SOUND_AUDIO && PHYS_SOUND_3D
using System;
using UnityEngine;

namespace PhysSound.Internal
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    internal sealed class HierarchicalStringDropdownAttribute : PropertyAttribute
    {
        internal HierarchicalStringDropdownAttribute(string configPath, params string[] categories)
        {
            ConfigPath = configPath;
            Categories = categories ?? Array.Empty<string>();
        }

        internal string ConfigPath { get; }
        internal string[] Categories { get; }
        internal bool Required { get; set; }
        internal bool Flags { get; set; }
        internal char Separator { get; set; } = '|';
        internal Type UniqueAcrossAssetType { get; set; }
    }
}
#endif
