using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PhysSound
{
    [CreateAssetMenu(menuName = "Phys Sound/Database")]
    public class PhysSoundDatabase : ScriptableObject
    {
        [SerializeField] private List<PhysSoundMaterial> _materials = new List<PhysSoundMaterial>();
        [SerializeField] private List<PhysSoundKey> _keys = new List<PhysSoundKey>();

        private Dictionary<PhysicMaterial, PhysSoundKey> _keysMap = new Dictionary<PhysicMaterial, PhysSoundKey>();

        private Dictionary<PhysicsMaterial2D, PhysSoundKey> _keysMap2D =
            new Dictionary<PhysicsMaterial2D, PhysSoundKey>();

        private void OnEnable() => Init();

        private void Reset() => Collect();

        public void Init()
        {
            foreach (PhysSoundMaterial physSoundMaterial in _materials)
            {
                physSoundMaterial.Init();
            }

            _keysMap.Clear();
            foreach (PhysSoundKey key in _keys)
            {
                for (int i = 0; i < key.PhysicMterials.Count; i++)
                {
                    _keysMap.Add(key.PhysicMterials[i], key);
                }
            }

            _keysMap2D.Clear();
            foreach (PhysSoundKey key in _keys)
            {
                for (int i = 0; i < key.PhysicMterials2D.Count; i++)
                {
                    _keysMap2D.Add(key.PhysicMterials2D[i], key);
                }
            }
        }

        public PhysSoundKey GetSoundMaterial(PhysicMaterial physicMaterial)
        {
            if (_keysMap.TryGetValue(physicMaterial, out PhysSoundKey physSoundKey))
            {
                return physSoundKey;
            }

            return null;
        }

        public PhysSoundKey GetSoundMaterial(PhysicsMaterial2D physicMaterial)
        {
            if (_keysMap2D.TryGetValue(physicMaterial, out PhysSoundKey physSoundKey))
            {
                return physSoundKey;
            }

            return null;
        }

        [ContextMenu(("Collect all"))]
        private void Collect()
        {
            _materials = Collect<PhysSoundMaterial>();
            _keys = Collect<PhysSoundKey>();
        }

        private List<T> Collect<T>() where T : Object
        {
#if UNITY_EDITOR
            return AssetDatabase.FindAssets($"t:{nameof(T)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .ToList();
#endif
            return null;
        }
    }
}