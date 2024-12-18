using Grayjay.Engine.Pagers.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class AdhocPager<T> : IPager<T>
    {
        private int _page = 0;
        private Func<int, T[]> _nextPage;
        private T[] _currentResults;
        private bool _hasMore = true;

        public string ID { get; set; } = Guid.NewGuid().ToString();

        public AdhocPager(Func<int, T[]> nextPage, T[] initialResults = null)
        {
            _nextPage = nextPage;
            if (initialResults != null)
                _currentResults = initialResults;
            else
                NextPage();
        }


        public bool HasMorePages()
        {
            return _hasMore;
        }
        public void NextPage()
        {
            var newResults = _nextPage(++_page);
            if (!newResults.Any())
                _hasMore = false;
            _currentResults = newResults;
        }

        public T[] GetResults()
        {
            return _currentResults;
        }
    }
}
