using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class SingleItemPager<T>
    {
        private readonly IPager<T> _pager;
        private T[] _currentResult;
        private int _currentResultPos;

        public SingleItemPager(IPager<T> pager)
        {
            _pager = pager ?? throw new ArgumentNullException(nameof(pager));
            _currentResult = _pager.GetResults();
            _currentResultPos = 0;
        }

        public IPager<T> GetPager() => _pager;

        public bool HasMoreItems() => _currentResultPos < _currentResult.Length;

        public T GetCurrentItem()
        {
            lock (this)
            {
                if (_currentResultPos >= _currentResult.Length)
                {
                    _pager.NextPage();
                    _currentResult = _pager.GetResults();
                    _currentResultPos = 0;
                }

                if (_currentResultPos < _currentResult.Length)
                    return _currentResult[_currentResultPos];
                else
                    return default(T);
            }
        }

        public T ConsumeItem()
        {
            var result = GetCurrentItem();
            _currentResultPos++;
            return result;
        }
    }
}
