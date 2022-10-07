namespace PhysSound
{
    public class PhysSoundComposition
    {
        public int KeyIndex;
        public float Value;
        public int Count;

        public PhysSoundComposition(int key)
        {
            KeyIndex = key;
        }

        public void Reset()
        {
            Value = 0;
            Count = 0;
        }

        public void Add(float val)
        {
            Value += val;
            Count++;
        }

        public float GetAverage()
        {
            return Value / Count;
        }

    }
}