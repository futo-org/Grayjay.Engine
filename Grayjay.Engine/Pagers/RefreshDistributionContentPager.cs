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
        public RefreshDistributionContentPager(IEnumerable<IPager<T>> pagers, IEnumerable<Task<IPager<T>>> pendingPagers, IEnumerable<IPager<T>> placeholderPager, Action<IPager<T>> onChanged = null) : base(pagers, pendingPagers, placeholderPager, onChanged)
        {
        }

        protected override IPager<T> RecreatePager(List<IPager<T>> pagers)
        {
            return new MultiDistributionPager<T>(pagers.ToDictionary(x => x, y => 1f), false, 20);
        }
    }
}
