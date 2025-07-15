using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Grayjay.Engine.V8
{
    public class V8PromiseMetadata
    {
        [V8Property("estDuration", true)]
        public int EstimateDuration { get; private set; } = -1;
    }
}
