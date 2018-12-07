using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public class WeightedRandom<T>
    {
        IReadOnlyList<T> ItemsByWeight;

        public WeightedRandom(IEnumerable<T> Items, Func<T, int> Weight)
        {
            var List = new List<T>();
            foreach (var I in Items)
            {
                var W = Weight(I);
                for (int i = 0; i < W; ++i)
                    List.Add(I);
            }

            ItemsByWeight = List;
        }

        public T Get(int Random)
        {
            return ItemsByWeight[Random % ItemsByWeight.Count];
        }
    }
}
