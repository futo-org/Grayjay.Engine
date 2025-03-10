using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Pagers
{
    public class LazyRefreshPager<T> : MultiRefreshPager<T>
    {
        public LazyRefreshPager(Task<IPager<T>> pendingPager, IPager<T> placeholderPager, Action<IPager<T>> onChanged = null, int pageSize = 20) : base(new IPager<T>[0], new Task<IPager<T>>[]
        {
            pendingPager
        }, new IPager<T>[]
        {
            placeholderPager
        }, onChanged, pageSize)
        {

        }
        protected override IPager<T> RecreatePager(List<IPager<T>> pagers)
        {
            return pagers.FirstOrDefault();
        }
    }
}
