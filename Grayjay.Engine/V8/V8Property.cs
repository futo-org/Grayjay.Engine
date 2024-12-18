using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.V8
{
    public class V8PropertyAttribute: Attribute
    {
        public string Name { get; set; }
        public bool Optional { get; set; }
        public V8PropertyAttribute(string name, bool optional = false)
        {
            Name = name;
            Optional = optional;
        }
        
    }
}
