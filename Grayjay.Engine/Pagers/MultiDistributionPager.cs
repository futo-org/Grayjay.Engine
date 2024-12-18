using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class MultiDistributionPager<T> : MultiPager<T>
    {
        private readonly Dictionary<IPager<T>, float> _dist;
        private readonly Dictionary<IPager<T>, float> _distConsumed;
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public MultiDistributionPager(Dictionary<IPager<T>, float> pagers, bool allowFailure = false ,int pageSize = 9)
            : base(pagers.Keys.ToList(), allowFailure, pageSize)
        {
            var distTotal = pagers.Values.Sum();
            _dist = new Dictionary<IPager<T>, float>();

            // Convert distribution values to inverted percentages
            foreach (var kv in pagers)
                _dist[kv.Key] = 1f - (kv.Value / distTotal);

            _distConsumed = new Dictionary<IPager<T>, float>();
            foreach (var kv in _dist)
                _distConsumed[kv.Key] = 0f;
        }

        protected override int SelectItemIndex(SelectionOption<T>[] options)
        {
            if (options.Length == 0)
                return -1;

            var bestIndex = 0;
            var bestConsumed = _distConsumed[options[0].Pager.GetPager()] + _dist[options[0].Pager.GetPager()];

            for (var i = 1; i < options.Length; i++)
            {
                var pager = options[i].Pager.GetPager();
                var valueAfterAdd = _distConsumed[pager] + _dist[pager];

                if (valueAfterAdd < bestConsumed)
                {
                    bestIndex = i;
                    bestConsumed = valueAfterAdd;
                }
            }

            _distConsumed[options[bestIndex].Pager.GetPager()] = bestConsumed;
            return bestIndex;
        }
    }
}
