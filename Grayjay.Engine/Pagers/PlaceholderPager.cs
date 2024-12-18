using Grayjay.Engine.Models.Feed;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class PlaceholderPager<T> : IPager<T>
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();

        private int _pageSize;
        public Func<T> FactoryMethod { get; private set; }

        private T[] _results;

        public PlaceholderPager(int pageSize, Func<T> factoryMethod)
        {
            _pageSize = pageSize;
            FactoryMethod = factoryMethod;
            _results = Enumerable.Range(0, pageSize)
                .Select(x => factoryMethod())
                .ToArray();
        }


        public T[] GetResults()
        {
            return _results;
        }

        public bool HasMorePages()
        {
            return true;
        }

        public void NextPage()
        {

        }
    }
}
