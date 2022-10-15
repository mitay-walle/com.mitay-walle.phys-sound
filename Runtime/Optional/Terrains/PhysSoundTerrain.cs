using System.Collections.Generic;
using UnityEngine;

namespace PhysSound.Optional.Terrains
{
    [AddComponentMenu("PhysSound/PhysSound Terrain")]
    public class PhysSoundTerrain : PhysSoundBase
    {
#if PHYS_SOUND_TERRAIN
        public Terrain Terrain;
        private TerrainData _terrainData;
#else
        public Behaviour Terrain;
#endif

        public List<PhysSoundMaterial> SoundMaterials = new List<PhysSoundMaterial>();
        private Dictionary<PhysSoundKey, PhysSoundComposition> _compDic = new Dictionary<PhysSoundKey, PhysSoundComposition>();
        private Vector3 _terrainPos;

        private void Start()
        {
#if PHYS_SOUND_TERRAIN
            _terrainData = Terrain.terrainData;
            _terrainPos = Terrain.transform.position;
            foreach (PhysSoundMaterial mat in SoundMaterials)
            {
                if (!_compDic.ContainsKey(mat.MaterialTypeKey))
                    _compDic.Add(mat.MaterialTypeKey, new PhysSoundComposition(mat.MaterialTypeKey));
            }
#endif
        }

        /// <summary>
        /// Gets the most prominent PhysSound Material at the given point on the terrain.
        /// </summary>
        public override PhysSoundMaterial GetPhysSoundMaterial(Vector3 contactPoint)
        {
            int dominantIndex = GetDominantTexture(contactPoint);

            if (dominantIndex < SoundMaterials.Count && SoundMaterials[dominantIndex] != null)
                return SoundMaterials[dominantIndex];

            return null;
        }

        /// <summary>
        /// Gets the composition of PhysSound Materials at the given point on the terrain.
        /// </summary>
        public Dictionary<PhysSoundKey, PhysSoundComposition> GetComposition(Vector3 contactPoint)
        {
            foreach (PhysSoundComposition c in _compDic.Values)
                c.Reset();

            float[] mix = GetTextureMix(contactPoint);

            for (int i = 0; i < mix.Length; i++)
            {
                if (i >= SoundMaterials.Count)
                    break;

                if (SoundMaterials[i] == null)
                    continue;

                PhysSoundComposition comp;

                if (_compDic.TryGetValue(SoundMaterials[i].MaterialTypeKey, out comp))
                {
                    comp.Add(mix[i]);
                }
            }

            return _compDic;
        }

        private float[] GetTextureMix(Vector3 worldPos)
        {
#if PHYS_SOUND_TERRAIN
            int mapX = (int) (((worldPos.x - _terrainPos.x) / _terrainData.size.x) * _terrainData.alphamapWidth);
            int mapZ = (int) (((worldPos.z - _terrainPos.z) / _terrainData.size.z) * _terrainData.alphamapHeight);

            float[,,] splatmapData = _terrainData.GetAlphamaps(mapX, mapZ, 1, 1);

            float[] cellMix = new float[splatmapData.GetUpperBound(2) + 1];

            for (int i = 0; i < cellMix.Length; i++)
            {
                cellMix[i] = splatmapData[0, 0, i];
            }

            return cellMix;
#endif
            return null;
        }

        private int GetDominantTexture(Vector3 worldPos)
        {
            float[] mix = GetTextureMix(worldPos);

            float maxMix = 0;
            int maxMixIndex = 0;

            for (int j = 0; j < mix.Length; j++)
            {
                if (mix[j] > maxMix)
                {
                    maxMixIndex = j;
                    maxMix = mix[j];
                }
            }

            return maxMixIndex;
        }
    }
}