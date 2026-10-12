using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Pagers
{
    public class FilterPager<T> : INestedPager<T>, IPager<T>
    {
        private readonly IPager<T> _pager;
        private readonly Func<T, bool> _filter;

        public string ID { get { return _pager.ID; } set { _pager.ID = value; } }

        public FilterPager(IPager<T> pager, Func<T, bool> filter)
        {
            _pager = pager ?? throw new ArgumentNullException(nameof(pager));
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
        }

        public IPager<T> FindPager(Func<IPager<T>, bool> query)
        {
            if (query(_pager))
                return _pager;
            else if (_pager is INestedPager<T>)
                return ((_pager as INestedPager<T>) ?? throw new InvalidOperationException()).FindPager(query);
            return null;
        }

        public T[] GetResults()
        {
            return _pager.GetResults().Where(_filter).ToArray();
        }

        public bool HasMorePages()
        {
            return _pager.HasMorePages();
        }

        public void NextPage()
        {
            _pager.NextPage();
        }
    }
}
