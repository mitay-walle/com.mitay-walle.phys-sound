namespace PhysSound.Optional.Terrains
{
    public class PhysSoundComposition
    {
        public PhysSoundKey Key;
        public float Value;
        public int Count;

        public PhysSoundComposition(PhysSoundKey key)
        {
            Key = key;
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