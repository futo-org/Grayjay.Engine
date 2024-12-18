using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.Pagers;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;

namespace Grayjay.Engine.Models.Comments
{
    public class PlatformComment
    {
        private GrayjayPlugin _plugin;
        private IJavaScriptObject _object;

        [V8Property("contextUrl")]
        public string ContextUrl { get; set; }

        [V8Property("author")]
        public PlatformAuthorLink Author { get; set; }
        [V8Property("message")]
        public string Message { get; set; }
        [V8Property("rating")]
        public IRating Rating { get; set; }
        [V8Property("date")]
        public DateTime Date { get; set; }

        [V8Property("replyCount")]
        public int ReplyCount { get; set; }

        [V8Property("context")]
        public Dictionary<string, string> Context { get; set; }

        private bool _hasGetReplies = false;

        public PlatformComment(GrayjayPlugin plugin, IJavaScriptObject obj = null)
        {
            _plugin = plugin;
            _object = obj;
            if (obj != null)
            {
                var getRepliesObj = obj.GetProperty("getReplies");
                _hasGetReplies = getRepliesObj != null && !(getRepliesObj is Undefined);
            }
        }


        public V8Pager<PlatformComment> GetReplies()
        {
            if (!_hasGetReplies)
                return null;

            var obj = _object.InvokeMethod("getReplies");
            if (!(obj is IJavaScriptObject))
                throw new InvalidCastException($"Found {obj?.GetType()?.Name}, expected IJavaScriptObject");
            return new V8Pager<PlatformComment>(_plugin, (IJavaScriptObject)obj);
        }
    }
}
