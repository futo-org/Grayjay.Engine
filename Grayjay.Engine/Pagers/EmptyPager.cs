using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class EmptyPager<T> : IPager<T>
    {
        public string ID { get; set; } = Guid.NewGuid().ToString();

        public T[] GetResults() => new T[0];
        public bool HasMorePages() => false;
        public void NextPage() { }
    }
}
