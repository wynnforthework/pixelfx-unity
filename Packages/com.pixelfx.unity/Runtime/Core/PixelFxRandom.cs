namespace PixelFx
{
    internal struct PixelFxRandom
    {
        private uint state;

        internal PixelFxRandom(int seed)
        {
            state = unchecked((uint)seed);
            if (state == 0) state = 1;
        }

        internal PixelFxRandom(int seed, int index)
        {
            unchecked
            {
                uint h = Hash(0x9e3779b9, seed);
                state = Hash(h, index);
                if (state == 0) state = 1;
            }
        }

        private static uint Hash(uint h, int value)
        {
            unchecked
            {
                h = (h ^ (uint)value) * 0x85ebca6b;
                h ^= h >> 13;
                h *= 0xc2b2ae35;
                return h ^ (h >> 16);
            }
        }

        internal static double At(int x, int y, int seed) =>
            Hash(Hash(Hash(0x9e3779b9, x), y), seed) / 4294967296.0;

        internal double Range(double a, double b)
        {
            unchecked
            {
                uint t = state += 0x6d2b79f5;
                t = (t ^ (t >> 15)) * (t | 1);
                t ^= t + (t ^ (t >> 7)) * (t | 61);
                return a + (b - a) * ((t ^ (t >> 14)) / 4294967296.0);
            }
        }
    }
}
