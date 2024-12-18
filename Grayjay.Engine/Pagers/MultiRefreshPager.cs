using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.Pagers
{
    public abstract class MultiRefreshPager<T> : IPager<T>
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();

        private List<ReusablePager<T>> _pagersResuable;
        private IPager<T> _currentPager;

        private bool _addPlaceholders = false;
        private int _totalPagers = 0;
        private Dictionary<Task<IPager<T>>, IPager<T>> _placeholderPagersPaired;

        private List<Task<IPager<T>>> _pending;

        public event Action<IPager<T>> OnPagerChanged;
        public event Action<Exception> OnPagerError;


        public MultiRefreshPager(IEnumerable<IPager<T>> pagers, IEnumerable<Task<IPager<T>>> pendingPagers, IEnumerable<IPager<T>> placeholderPager, Action<IPager<T>> onChanged = null)
        {
            if (onChanged != null)
                OnPagerChanged += onChanged;

            _pending = pendingPagers.ToList();
            _pagersResuable = pagers.Select(x => new ReusablePager<T>(x)).ToList();
            _totalPagers = pagers.Count() + pendingPagers.Count();
            _placeholderPagersPaired = placeholderPager.Take(pendingPagers.Count())
                .Select((x, i) => (_pending[i], x))
                .ToDictionary(x => x.Item1, y => y.x);

            foreach(var pendingPager in pendingPagers)
            {
                pendingPager.ContinueWith((task) =>
                {
                    lock(_pending)
                    {
                        _pending.Remove(task);
                    }
                    if (task.IsFaulted)
                    {
                        OnPagerError?.Invoke(task.Exception);
                        var replacing = _placeholderPagersPaired[pendingPager];
                        if (replacing != null)
                            UpdatePager(null, replacing, task.Exception);
                    }
                    else 
                        UpdatePager(pendingPager.Result);
                });
            }
            lock (_pagersResuable)
            {
                _currentPager = RecreatePager(GetCurrentSubPagers());
                _currentPager.ID = ID;

                if (_currentPager is MultiPager<T> mp)
                    mp.Initialize();
            }
        }


        public T[] GetResults()
        {
            lock (_pagersResuable)
                return _currentPager.GetResults();
        }

        public bool HasMorePages()
        {
            lock (_pagersResuable)
                return _currentPager.HasMorePages();
        }

        public void NextPage()
        {
            lock (_pagersResuable)
                _currentPager.NextPage();
        }

        private void UpdatePager(IPager<T> pagerToAdd, IPager<T> toReplacePager = null, Exception error = null)
        {
            lock(_pagersResuable)
            {
                if(pagerToAdd == null)
                {
                    if(toReplacePager != null && toReplacePager is PlaceholderPager<PlatformContent> pe && error != null)
                    { var sample = pe.FactoryMethod();   
                        _pagersResuable.Add((new PlaceholderPager<T>(5, () => (T)(object)new PlatformContentPlaceholder(sample.ID.PluginID, error, sample.ID.Platform))).AsReusable());
                        _currentPager = RecreatePager(GetCurrentSubPagers());
                        _currentPager.ID = ID;

                        if (_currentPager is MultiPager<T> cmp)
                            cmp.Initialize();

                        OnPagerChanged?.Invoke(_currentPager);
                    }
                    return;
                }

                _pagersResuable.Add(pagerToAdd.AsReusable());
                _currentPager = RecreatePager(GetCurrentSubPagers());
                _currentPager.ID = ID;

                if (_currentPager is MultiPager<T> mp)
                    mp.Initialize();

                OnPagerChanged?.Invoke(_currentPager);
            }
        }

        protected abstract IPager<T> RecreatePager(List<IPager<T>> pagers);

        private List<IPager<T>> GetCurrentSubPagers()
        {
            lock (_pending)
                return _pagersResuable.Select(x => x.GetWindow())
                    .Concat(_placeholderPagersPaired.Where(x => _pending.Contains(x.Key)).Select(x => x.Value))
                    .ToList();
        }
    }
}
