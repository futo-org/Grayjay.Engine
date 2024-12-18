using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public interface INestedPager<T>
    {
        IPager<T> FindPager(Func<IPager<T>, bool> query);
    }
}
