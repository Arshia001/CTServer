using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CTGrainInterfaces
{
    public class WeightedRandom<T>
    {
        C5.TreeDictionary<int, T> itemsByWeightOffset = new C5.TreeDictionary<int, T>();
        int maxWeight;


        public WeightedRandom(IEnumerable<T> items, Func<T, int> weight)
        {
            foreach (var item in items)
            {
                var w = weight(item);
                if (w <= 0)
                    continue;

                itemsByWeightOffset.Add(maxWeight, item);
                maxWeight += w;
            }
        }

        public T Get(int random)
        {
            var index = random % maxWeight;
            return itemsByWeightOffset.WeakPredecessor(index).Value;
        }
    }
}
