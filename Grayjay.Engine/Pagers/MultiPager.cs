using Grayjay.Engine.Pagers.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public abstract class MultiPager<T> : IPager<T>
    {
        protected readonly object _pagerLock = new object();

        protected readonly List<IPager<T>> _pagers;
        protected readonly List<SingleItemPager<T>> _subSinglePagers;
        protected readonly List<IPager<T>> _failedPagers = new List<IPager<T>>();

        private int _pageSize = 9;

        private bool _didInitialize = false;

        private T[] _currentResults = new T[0];
        private Dictionary<IPager<T>, Exception> _currentResultExceptions = new Dictionary<IPager<T>, Exception>();

        public string ID { get; set; } = Guid.NewGuid().ToString();

        public bool AllowFailure { get; }

        public int TotalPagers => _pagers.Count;

        protected MultiPager(IEnumerable<IPager<T>> pagers, bool allowFailure = false, int pageSize = 9)
        {
            _pageSize = pageSize;
            AllowFailure = allowFailure;
            _pagers = pagers.ToList();
            _subSinglePagers = _pagers.Select(pager => new SingleItemPager<T>(pager)).ToList();
        }

        public MultiPager<T> Initialize()
        {
            _currentResults = LoadNextPage(true);
            _didInitialize = true;
            return this;
        }

        public bool HasMorePages()
        {
            lock (_pagerLock)
            {
                return _subSinglePagers.Any(it => !_failedPagers.Contains(it.GetPager()) && (it.HasMoreItems() || it.GetPager().HasMorePages()));
            }
        }

        public void NextPage()
        {
            Console.WriteLine("Load next page");
            if (!_didInitialize)
                throw new InvalidOperationException("Call initialize on MultiVideoPager before using it");
            LoadNextPage();
            Console.WriteLine($"New results: {_currentResults.Length}");
        }

        public T[] GetResults()
        {
            if (!_didInitialize)
                throw new InvalidOperationException("Call initialize on MultiVideoPager before using it");
            return _currentResults;
        }

        public Dictionary<IPager<T>, Exception> GetResultExceptions()
        {
            if (!_didInitialize)
                throw new InvalidOperationException("Call initialize on MultiVideoPager before using it");
            return _currentResultExceptions;
        }

        private T[] LoadNextPage(bool isInitial = false)
        {
            lock (_pagerLock)
            {
                if (_subSinglePagers.Count == 0)
                    return new T[0];
            }

            if (!isInitial && !HasMorePages())
                throw new NoNextPageException();

            var results = new List<T>();
            var exceptions = new Dictionary<IPager<T>, Exception>();
            for (var i = 0; i < _pageSize; i++)
            {
                List<SingleItemPager<T>> validPagers;
                lock (_pagerLock)
                {
                    validPagers = _subSinglePagers
                        .Where(pager => !_failedPagers.Contains(pager.GetPager()) && (pager.HasMoreItems() || pager.GetPager().HasMorePages()))
                        .ToList();
                }

                var options = new List<SelectionOption<T>>();
                foreach (var pager in validPagers)
                {
                    T item = default(T);
                    if (AllowFailure)
                    {
                        try
                        {
                            item = pager.GetCurrentItem();
                        }
                        catch (NoNextPageException)
                        {
                            //TODO: This should never happen, has to be fixed later
                            Console.WriteLine("Expected item from pager but no page found?");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to fetch page for pager, exception: {ex.Message}");
                            _failedPagers.Add(pager.GetPager());
                            exceptions[pager.GetPager()] = ex;
                        }
                    }
                    else
                    {
                        try
                        {
                            item = pager.GetCurrentItem();
                        }
                        catch (NoNextPageException)
                        {
                            //TODO: This should never happen, has to be fixed later
                            Console.WriteLine("Expected item from pager but no page found?");
                        }
                        catch(Exception ex)
                        {
                            Console.WriteLine($"Other exception in subpager: [{ex.GetType().Name}] {ex.Message}");
                        }
                    }

                    if (item != null)
                        options.Add(new SelectionOption<T>(pager, item));
                }

                if (options.Count == 0)
                    break;

                var bestIndex = SelectItemIndex(options.ToArray());
                if (bestIndex >= 0)
                {
                    var consumed = options[bestIndex].Pager.ConsumeItem();
                    if (consumed != null)
                        results.Add(consumed);
                }
            }

            _currentResults = results.ToArray();
            _currentResultExceptions = exceptions;
            return _currentResults;
        }

        protected abstract int SelectItemIndex(SelectionOption<T>[] options);

        protected class SelectionOption<T>
        {
            public SingleItemPager<T> Pager { get; }
            public T Item { get; }

            public SelectionOption(SingleItemPager<T> pager, T item)
            {
                Pager = pager;
                Item = item;
            }
        }

        public void SetExceptions(Dictionary<IPager<T>, Exception> exs)
        {
            _currentResultExceptions = exs;
        }

        public IPager<T>? FindPager(Func<IPager<T>, bool> query)
        {
            foreach (var pager in _pagers)
            {
                if (query(pager))
                    return pager;
                if (pager is MultiPager<T> multiPager)
                    return multiPager.FindPager(query);
            }

            return null;
        }

        public static string TAG => "MultiPager";
    }
}
