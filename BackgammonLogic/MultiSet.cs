using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BackgammonLogic
{
    public class Multiset<T> : ICollection<T>
    {
        Dictionary<T, int> data;


        public IEnumerable<T> Unique
        {
            get
            {
                return data.Keys;
            }
        }


        public Multiset()
        {
            data = new Dictionary<T, int>();
        }

        public Multiset(int capacity)
        {
            data = new Dictionary<T, int>(capacity);
        }

        public Multiset(IEnumerable<T> values)
        {
            data = new Dictionary<T, int>();
            foreach (var val in values)
                Add(val);
        }

        public void Add(T item)
        {
            int count;
            data.TryGetValue(item, out count);
            ++count;
            data[item] = count;
        }

        public void Add(T item, int count)
        {
            int _count;
            data.TryGetValue(item, out _count);
            _count += count;
            data[item] = _count;
        }

        public void Clear()
        {
            data.Clear();
        }

        public bool Contains(T item)
        {
            return data.ContainsKey(item);
        }

        public int Count
        {
            get
            {
                return data.Values.Sum();
            }
        }

        public bool IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool Remove(T item)
        {
            int count;
            if (!data.TryGetValue(item, out count))
            {
                return false;
            }
            count--;
            if (count == 0)
            {
                data.Remove(item);
            }
            else
            {
                data[item] = count;
            }
            return true;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return new Enumerator(this);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(Multiset<T> Other)
        {
            Other.data = new Dictionary<T, int>(data);
        }

        public T Max()
        {
            return data.Keys.Max();
        }

        private class Enumerator : IEnumerator<T>
        {
            public Enumerator(Multiset<T> multiset)
            {
                baseEnumerator = multiset.data.GetEnumerator();
                index = 0;
            }

            private readonly IEnumerator<KeyValuePair<T, int>> baseEnumerator;
            private int index;

            public T Current
            {
                get
                {
                    return baseEnumerator.Current.Key;
                }
            }

            public void Dispose()
            {
                baseEnumerator.Dispose();
            }

            object System.Collections.IEnumerator.Current
            {
                get
                {
                    return baseEnumerator.Current.Key;
                }
            }

            public bool MoveNext()
            {
                KeyValuePair<T, int> kvp = baseEnumerator.Current;
                if (index < (kvp.Value - 1))
                {
                    index++;
                    return true;
                }
                else
                {
                    bool result = baseEnumerator.MoveNext();
                    index = 0;
                    return result;
                }
            }

            public void Reset()
            {
                baseEnumerator.Reset();
                index = 0;
            }
        }
    }
}
