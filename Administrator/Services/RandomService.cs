using System;

namespace Administrator.Services
{
    public class RandomService
    {
        private readonly Random _r;

        public RandomService(int? seed = null)
        {
            _r = seed is null ? new Random() : new Random(seed.Value);
        }

        public int Next(uint min, uint max)
        {
            return _r.Next((int) min, (int) max + 1);
        }
    }
}
