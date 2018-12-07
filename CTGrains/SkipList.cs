using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using System.Diagnostics;

namespace CTGrains
{
    // Taken and ported to C# mostly from Redis' source
    public class SkipList<TValue, TScore> : IEnumerable<KeyValuePair<TValue, TScore>>
        where TValue : IEquatable<TValue>, IComparable
        where TScore : IComparable
    {
        public class Node
        {
            SkipListNode SLNode;

            public TValue Value => SLNode.Value;
            public TScore Score => SLNode.Score;

            internal Node(SkipListNode SLNode)
            {
                this.SLNode = SLNode;
            }
        }

        [DebuggerDisplay("{Value} {Score} {Level.Length}")]
        internal class SkipListNode
        {
            public struct SkipListLevel
            {
                public SkipListNode Forward;
                public ulong Span;
            }

            public TValue Value;
            public TScore Score;
            public SkipListNode Backward;
            public SkipListLevel[] Level;

            public SkipListNode(int Level, TScore Score, TValue Value)
            {
                this.Level = new SkipListLevel[Level];
                this.Score = Score;
                this.Value = Value;
            }
        }

        Dictionary<TValue, TScore> ValueCache;
        SkipListNode Head;
        SkipListNode Tail;
        SkipListNode[] DeserializationTails;
        public ulong Length { get; private set; }
        int Level;

        readonly Random Rand;
        readonly int MaxLevel;
        readonly float P;

        public SkipList(int MaxLevel = 32, float P = 0.25f)
        {
            this.MaxLevel = MaxLevel;
            this.P = P;
            Rand = new Random();
            ValueCache = new Dictionary<TValue, TScore>();

            Level = 1;
            Length = 0;
            Head = new SkipListNode(MaxLevel, default(TScore), default(TValue));
            for (int j = 0; j < MaxLevel; j++)
            {
                Head.Level[j].Forward = null;
                Head.Level[j].Span = 0;
            }
            Head.Backward = null;
            Tail = null;
        }

        int RandomLevel()
        {
            int level = 1;
            while ((Rand.Next() & 0xFFFF) < (P * 0xFFFF) && level < MaxLevel)
                level += 1;
            return level;
        }

        public TScore GetScore(TValue Value)
        {
            if (ValueCache.TryGetValue(Value, out var Score))
                return Score;

            return default(TScore);
        }

        public void Add(TValue Value, TScore Score)
        {
            Delete(Value); // If it exists; otherwise, this has no effect and no performance penalty

            SkipListNode x;
            SkipListNode[] update = new SkipListNode[MaxLevel];
            ulong[] rank = new ulong[MaxLevel];
            int i, level;

            x = Head;
            for (i = this.Level - 1; i >= 0; i--)
            {
                /* store rank that is crossed to reach the insert position */
                rank[i] = i == (this.Level - 1) ? 0 : rank[i + 1];
                var comp = x.Level[i].Forward?.Score.CompareTo(Score) ?? 1;
                while (x.Level[i].Forward != null &&
                    (comp > 0 || (comp == 0 && x.Level[i].Forward.Value.CompareTo(Value) > 0)))
                {
                    rank[i] += x.Level[i].Span;
                    x = x.Level[i].Forward;
                    comp = x.Level[i].Forward?.Score.CompareTo(Score) ?? 1;
                }
                update[i] = x;
            }

            level = RandomLevel();
            if (level > this.Level)
            {
                for (i = this.Level; i < level; i++)
                {
                    rank[i] = 0;
                    update[i] = this.Head;
                    update[i].Level[i].Span = this.Length;
                }
                this.Level = level;
            }

            x = new SkipListNode(level, Score, Value);
            for (i = 0; i < level; i++)
            {
                x.Level[i].Forward = update[i].Level[i].Forward;
                update[i].Level[i].Forward = x;

                /* update span covered by update[i] as x is inserted here */
                x.Level[i].Span = update[i].Level[i].Span - (rank[0] - rank[i]);
                update[i].Level[i].Span = (rank[0] - rank[i]) + 1;
            }

            /* increment span for untouched levels */
            for (i = level; i < this.Level; i++)
            {
                update[i].Level[i].Span++;
            }

            x.Backward = (update[0] == this.Head) ? null : update[0];
            if (x.Level[0].Forward != null)
                x.Level[0].Forward.Backward = x;
            else
                this.Tail = x;
            this.Length++;

            ValueCache.Add(Value, Score);
        }

        public void AddLast_ForDeserialization(TValue Value, TScore Score)
        {
            if (DeserializationTails == null)
            {
                if (Length != 0)
                    throw new InvalidOperationException("Cannot use deserialization features when not empty");
                DeserializationTails = new SkipListNode[MaxLevel];
                DeserializationTails[0] = Head;
            }

            SkipListNode x;
            int i, level;

            level = RandomLevel();
            if (level > this.Level)
            {
                for (i = this.Level; i < level; i++)
                {
                    if (DeserializationTails[i] == null)
                    {
                        DeserializationTails[i] = Head;
                        DeserializationTails[i].Level[i].Span = Length;
                    }
                }
                this.Level = level;
            }

            x = new SkipListNode(level, Score, Value);
            for (i = 0; i < level; i++)
            {
                DeserializationTails[i].Level[i].Forward = x;

                x.Level[i].Span = 0; // No span either
                DeserializationTails[i].Level[i].Span++;
            }

            for (i = level; i < this.Level; i++)
            {
                DeserializationTails[i].Level[i].Span++;
            }

            x.Backward = (DeserializationTails[0] == this.Head) ? null : DeserializationTails[0];
            this.Tail = x;
            this.Length++;

            for (i = 0; i < level; ++i)
                DeserializationTails[i] = x;

            ValueCache.Add(Value, Score);
        }

        public void FinalizeDeserialization()
        {
            DeserializationTails = null;
        }

        void DeleteNode(SkipListNode x, SkipListNode[] update)
        {
            int i;
            for (i = 0; i < this.Level; i++)
            {
                if (update[i].Level[i].Forward == x)
                {
                    update[i].Level[i].Span += x.Level[i].Span - 1;
                    update[i].Level[i].Forward = x.Level[i].Forward;
                }
                else
                {
                    update[i].Level[i].Span -= 1;
                }
            }
            if (x.Level[0].Forward != null)
            {
                x.Level[0].Forward.Backward = x.Backward;
            }
            else
            {
                this.Tail = x.Backward;
            }
            while (this.Level > 1 && this.Head.Level[this.Level - 1].Forward == null)
                this.Level--;
            this.Length--;
        }

        public bool Delete(TValue obj)
        {
            if (!ValueCache.TryGetValue(obj, out var score))
                return false;

            ValueCache.Remove(obj);

            var update = new SkipListNode[MaxLevel];
            SkipListNode x;
            int i;

            x = this.Head;
            for (i = this.Level - 1; i >= 0; i--)
            {
                var comp = x.Level[i].Forward?.Score.CompareTo(score) ?? 1;
                while (x.Level[i].Forward != null &&
                    (comp > 0 || (comp == 0 && x.Level[i].Forward.Value.CompareTo(obj) > 0)))
                {
                    x = x.Level[i].Forward;
                    comp = x.Level[i].Forward?.Score.CompareTo(score) ?? 1;
                }
                update[i] = x;
            }

            x = x.Level[0].Forward;
            if (x != null && score.CompareTo(x.Score) == 0 && x.Value.Equals(obj))
            {
                DeleteNode(x, update);
                return true;
            }
            return false;
        }

        public ulong GetRank(TValue o)
        {
            if (!ValueCache.TryGetValue(o, out var score))
                return 0;

            SkipListNode x;
            ulong rank = 0;
            int i;

            x = this.Head;
            for (i = this.Level - 1; i >= 0; i--)
            {
                var comp = x.Level[i].Forward?.Score.CompareTo(score) ?? 1;
                while (x.Level[i].Forward != null &&
                    (comp > 0 || (comp == 0 && x.Level[i].Forward.Value.CompareTo(o) >= 0)))
                {
                    rank += x.Level[i].Span;
                    x = x.Level[i].Forward;
                    comp = x.Level[i].Forward?.Score.CompareTo(score) ?? 1;
                }

                if (x.Value != null && x.Value.Equals(o))
                {
                    return rank;
                }
            }
            return 0;
        }

        public Node GetElementByRank(ulong rank)
        {
            var Res = GetElementByRank_Int(rank);
            return Res == null ? null : new Node(Res);
        }

        SkipListNode GetElementByRank_Int(ulong rank)
        {
            SkipListNode x;
            ulong traversed = 0;
            int i;

            x = this.Head;
            for (i = this.Level - 1; i >= 0; i--)
            {
                while (x.Level[i].Forward != null && (traversed + x.Level[i].Span) <= rank)
                {
                    traversed += x.Level[i].Span;
                    x = x.Level[i].Forward;
                }
                if (traversed == rank)
                {
                    return x;
                }
            }
            return null;
        }

        public IEnumerable<Node> GetRangeByRank(long start, long end)
        {
            return GetRangeByRankInt(start, end, false);
        }

        public IEnumerable<Node> GetReverseRangeByRank(long start, long end)
        {
            return GetRangeByRankInt(start, end, true);
        }

        IEnumerable<Node> GetRangeByRankInt(long start, long end, bool reverse)
        {
            var llen = (long)Length;
            if (start < 0) start = llen + start;
            if (end < 0) end = llen + end;
            if (start < 0) start = 0;

            if (start > end || start >= llen)
                return new Node[] { };

            if (end >= llen) end = llen - 1;
            var rangelen = (int)(end - start) + 1;

            var Result = new List<Node>();

            SkipListNode ln;

            if (reverse)
            {
                ln = this.Tail;
                if (start > 0)
                    ln = GetElementByRank_Int((ulong)(llen - start));
            }
            else
            {
                ln = this.Head.Level[0].Forward;
                if (start > 0)
                    ln = GetElementByRank_Int((ulong)(start + 1));
            }

            while (rangelen-- > 0)
            {
                Result.Add(new Node(ln));
                ln = reverse ? ln.Backward : ln.Level[0].Forward;
            }

            return Result;
        }

        public IEnumerator<KeyValuePair<TValue, TScore>> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        class Enumerator : IEnumerator<KeyValuePair<TValue, TScore>>
        {
            SkipList<TValue, TScore> List;
            SkipListNode Node;

            public Enumerator(SkipList<TValue, TScore> List)
            {
                this.List = List;
                Node = List.Head;
            }

            public KeyValuePair<TValue, TScore> Current => new KeyValuePair<TValue, TScore>(Node.Value, Node.Score);

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (Node.Level[0].Forward != null)
                {
                    Node = Node.Level[0].Forward;
                    return true;
                }
                else
                    return false;
            }

            public void Reset()
            {
                Node = List.Head;
            }
        }
    }
}
