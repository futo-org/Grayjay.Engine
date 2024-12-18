using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class ReusablePager<T> : INestedPager<T>, IPager<T>
    {
        private readonly IPager<T> _pager;
        public List<T> PreviousResults { get; } = new List<T>();
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public ReusablePager(IPager<T> subPager)
        {
            _pager = subPager;
            lock (PreviousResults)
            {
                PreviousResults.AddRange(subPager.GetResults());
            }
        }

        public IPager<T> FindPager(Func<IPager<T>, bool> query)
        {
            if (query(_pager))
                return _pager;
            else if (_pager is INestedPager<T>)
                return ((_pager as INestedPager<T>) ?? throw new InvalidOperationException()).FindPager(query);
            return null;
        }

        public bool HasMorePages()
        {
            return _pager.HasMorePages();
        }

        public void NextPage()
        {
            _pager.NextPage();
        }

        public T[] GetResults()
        {
            T[] results;
            lock (PreviousResults)
            {
                results = _pager.GetResults();
                PreviousResults.AddRange(results);
            }
            return ModifyResults(results);
        }
        public virtual T[] ModifyResults(T[] results)
        {
            return results;
        }

        public Window<T> GetWindow()
        {
            return new Window<T>(this);
        }

        public class Window<T> : IPager<T>, INestedPager<T>
        {
            private readonly ReusablePager<T> _parent;
            private int _position;
            private int _read;
            private T[] _currentResults;
            public string ID { get; set; } = Guid.NewGuid().ToString();

            public Window(ReusablePager<T> parent)
            {
                _parent = parent;
                lock (_parent.PreviousResults)
                {
                    _currentResults = _parent.PreviousResults.ToArray();
                    _read += _currentResults.Length;
                }
            }

            public bool HasMorePages()
            {
                return _parent.PreviousResults.Count > _read || _parent.HasMorePages();
            }

            public void NextPage()
            {
                lock (_parent.PreviousResults)
                {
                    if (_parent.PreviousResults.Count <= _read)
                    {
                        _parent.NextPage();
                        _parent.GetResults();
                    }
                    _currentResults = _parent.PreviousResults.GetRange(_read, _parent.PreviousResults.Count - _read).ToArray();
                    _read += _currentResults.Length;
                }
            }

            public T[] GetResults()
            {
                return _currentResults;
            }

            public IPager<T> FindPager(Func<IPager<T>, bool> query)
            {
                return _parent.FindPager(query);
            }
        }
    }
}
