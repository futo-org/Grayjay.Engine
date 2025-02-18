using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public class ModifyPager<T> : ModifyPager<T, T>
    {
        public ModifyPager(IPager<T> innerPager, Func<T, T> modify) : base(innerPager, modify)
        {
        }
    }
    public class ModifyPager<T, R> : IPager<R>
    {
        private IPager<T> _innerPager = null;
        private Func<T, R> _modifier = null;

        public string ID { get; set; } = Guid.NewGuid().ToString();

        public ModifyPager(IPager<T> innerPager, Func<T, R> modify)
        {
            if (modify == null)
                throw new ArgumentNullException("modify");
            _innerPager = innerPager;
            _modifier = modify;
        }

        public R[] GetResults() => _innerPager.GetResults().Select(x=>_modifier(x)).ToArray();
        public bool HasMorePages() => _innerPager.HasMorePages();
        public void NextPage() { _innerPager.NextPage(); }
    }
}
