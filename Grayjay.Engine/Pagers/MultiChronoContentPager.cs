using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class MultiChronoContentPager : MultiPager<PlatformContent>
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public MultiChronoContentPager(IEnumerable<IPager<PlatformContent>> pagers, bool allowFailure = false, int pageSize = 9) : base(pagers, allowFailure, pageSize)
        {
        }

        protected override int SelectItemIndex(SelectionOption<PlatformContent>[] options)
        {
            if (options.Length == 0)
                return -1;
            int bestIndex = 0;
            for(int i = 1; i < options.Length; i++)
            {
                PlatformContent best = options[bestIndex].Item;
                PlatformContent cur = options[i].Item;
                if ((best.DateTime == null || (cur.DateTime != null && cur.DateTime! > best.DateTime!)))
                    bestIndex = i;
            }
            return bestIndex;
        }
    }
    public class MultiOrderedPager<T> : MultiPager<T>
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();

        private Func<T, T, bool> _isBetterThan;

        public MultiOrderedPager(IEnumerable<IPager<T>> pagers, Func<T, T, bool> isBetterThan, bool allowFailure = false, int pageSize = 9) : base(pagers, allowFailure, pageSize)
        {
            _isBetterThan = isBetterThan;
        }

        protected override int SelectItemIndex(SelectionOption<T>[] options)
        {
            if (options.Length == 0)
                return -1;
            int bestIndex = 0;
            for (int i = 1; i < options.Length; i++)
            {
                T best = options[bestIndex].Item;
                T cur = options[i].Item;
                if (_isBetterThan(cur, best))
                    bestIndex = i;
            }
            return bestIndex;
        }
    }
}
