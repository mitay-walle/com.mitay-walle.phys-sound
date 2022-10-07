using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace PhysSound
{
    [System.Serializable]
    public class FoldoutList
    {
        [SerializeField]
        public List<bool> foldouts = new List<bool>();
        [SerializeField]
        public bool mainFoldout;
        [SerializeField]
        public Vector2 scrollPos;

        public bool this[int index]
        {
            get { return foldouts[index]; }
            set { foldouts[index] = value; }
        }

        public int Count
        {
            get { return foldouts.Count; }
        }

        public void Add(bool value)
        {
            foldouts.Add(value);
        }

        public void RemoveAt(int index)
        {
            foldouts.RemoveAt(index);
        }

        public void Reset()
        {
            for (int i = 0; i < foldouts.Count; i++)
            {
                foldouts[i] = false;
            }
        }

        public void Update(int count, bool defaultValue)
        {
            while (foldouts.Count > count)
                foldouts.RemoveAt(0);

            for (int i = foldouts.Count; i < count; i++)
            {
                foldouts.Add(defaultValue);
            }
        }

        public void Isolate(int index)
        {
            for (int k = 0; k < foldouts.Count; k++)
            {
                if (k != index)
                    foldouts[k] = false;
                else
                    foldouts[k] = true;
            }
        }

    }
}