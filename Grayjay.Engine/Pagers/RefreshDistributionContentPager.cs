using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Pagers
{
    public class RefreshDistributionContentPager<T> : MultiRefreshPager<T>
    {
        private readonly Func<T, bool>? _filter;

        public RefreshDistributionContentPager(IEnumerable<IPager<T>> pagers, IEnumerable<Task<IPager<T>>> pendingPagers, IEnumerable<IPager<T>> placeholderPager, Action<IPager<T>> onChanged = null, int pageSize = 20, Func<T, bool>? filter = null) : base(pagers, pendingPagers, placeholderPager, onChanged, pageSize)
        {
            _filter = filter;
        }

        protected override IPager<T> RecreatePager(List<IPager<T>> pagers)
        {
            if (_filter != null)
            {
                pagers = pagers.Select(x => x is FilterPager<T> ? x : (IPager<T>)new FilterPager<T>(x, _filter)).ToList();
            }
            return new MultiDistributionPager<T>(pagers.ToDictionary(x => x, y => 1f), false, _pageSize);
        }
    }
}
