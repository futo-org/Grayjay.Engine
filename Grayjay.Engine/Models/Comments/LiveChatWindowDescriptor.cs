using Grayjay.Engine.V8;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Comments
{
    public class LiveChatWindowDescriptor
    {
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("removeElements", true)]
        public List<string> RemoveElements { get; set; } = new List<string>();
        [V8Property("removeElementsInterval", true)]
        public List<string> RemoveElementsInterval { get; set; } = new List<string>();
    }
}
