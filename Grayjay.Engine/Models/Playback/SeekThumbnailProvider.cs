using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;

namespace Grayjay.Engine.Models.Playback
{
    public class SeekThumbnailProvider
    {
        private readonly GrayjayPlugin _plugin;
        private readonly IJavaScriptObject _obj;

        public int ResolutionMs { get; }

        public SeekThumbnailProvider(GrayjayPlugin plugin, IJavaScriptObject obj)
        {
            _plugin = plugin;
            _obj = obj;
            ResolutionMs = obj.GetOrDefault(plugin, "resolutionMs", "SeekThumbnailProvider", 10000);
            if (ResolutionMs <= 0)
                throw new ArgumentException("Seek resolution must be positive");
        }

        public SeekThumbnail? GetThumbnail(long positionMs)
        {
            lock (_obj)
            {
                var position = Math.Max(0, positionMs);
                position -= position % ResolutionMs;
                var result = _obj.InvokeV8(_plugin.Config, "getThumbnail", position) as IJavaScriptObject;
                return result == null ? null : V8Converter.ConvertValue<SeekThumbnail>(_plugin, result);
            }
        }
    }

    public class SeekThumbnail
    {
        [V8Property("url", true)]
        public string? Url { get; set; }

        [V8Property("frames", true)]
        public SeekThumbnailFrame[]? Frames { get; set; }
    }

    public class SeekThumbnailFrame
    {
        [V8Property("time")]
        public long Time { get; set; }
        [V8Property("x")]
        public int X { get; set; }
        [V8Property("y")]
        public int Y { get; set; }
        [V8Property("w")]
        public int W { get; set; }
        [V8Property("h")]
        public int H { get; set; }
    }
}
