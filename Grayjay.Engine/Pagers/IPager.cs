using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Pagers
{
    public interface IPager<T>
    {
        string ID { get; set; }

        bool HasMorePages();
        void NextPage();
        T[] GetResults();
    }
}
