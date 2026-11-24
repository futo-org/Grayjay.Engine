using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.Live;
using Grayjay.Engine.Models.Playback;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.Models.Subtitles;
using Grayjay.Engine.Models.Video;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.Pagers;
using Grayjay.Engine.V8;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Detail
{
    [JsonDerivedType(typeof(VideoLocal))]
    public class PlatformVideoDetails : PlatformVideo, IPlatformContentDetails
    {
        private bool _hasGetContentRecommendations = false;
        private bool _hasGetVODEvents = false;
        private bool _hasGetPlaybackTracker = false;
        private bool _hasGetSeekThumbnailProvider = false;
        private bool _seekThumbnailProviderLoaded = false;
        private SeekThumbnailProvider? _seekThumbnailProvider;
        private PluginConfig _config = null;



        [V8Property("description")]
        public virtual string Description { get; set; }

        [V8Property("rating", true)]
        public virtual IRating Rating { get; set; }

        [V8Property("video")]
        public virtual VideoDescriptor Video { get; set; }
        [V8Property("preview", true)]
        public virtual VideoDescriptor? Preview { get; set; }

        [V8Property("live", true)]
        public virtual IVideoSource? Live { get; set; }

        [V8Property("subtitles", true)]
        public virtual SubtitleSource[] Subtitles { get; set; }

        public bool IsVOD => HasVODEvents();

        public PlatformVideoDetails() : base(null) { }
        public PlatformVideoDetails(GrayjayPlugin plugin, IJavaScriptObject obj) : base(obj)
        {
            _hasGetContentRecommendations = obj.HasFunction("getContentRecommendations");
            _hasGetVODEvents = obj.HasFunction("getVODEvents");
            _hasGetPlaybackTracker = obj.HasFunction("getPlaybackTracker");
            _hasGetSeekThumbnailProvider = obj.HasFunction("getSeekThumbnailProvider");
            _config = plugin.Config;
        }


        public IPager<PlatformContent> GetContentRecommendations()
        {
            var underlying = GetUnderlyingObject();
            if (!_hasGetContentRecommendations || underlying == null) //TODO: Check if object available
                return null;

            var contentPagerObj = (IJavaScriptObject)underlying.InvokeV8(_config, "getContentRecommendations");
            var plugin = GrayjayPlugin.GetEnginePlugin(underlying.Engine);
            return new V8Pager<PlatformContent>(plugin, contentPagerObj);
        }

        //Details-level tracker (Android's videoDetails.getPlaybackTracker parity)
        public PlaybackTracker? GetPlaybackTracker()
        {
            var underlying = GetUnderlyingObject();
            if (!_hasGetPlaybackTracker || underlying == null)
                return null;

            var trackerObj = underlying.InvokeV8(_config, "getPlaybackTracker") as IJavaScriptObject;
            if (trackerObj == null)
                return null;
            var plugin = GrayjayPlugin.GetEnginePlugin(underlying.Engine);
            return new PlaybackTracker(plugin, trackerObj);
        }

        public SeekThumbnailProvider? GetSeekThumbnailProvider()
        {
            lock (this)
            {
                var underlying = GetUnderlyingObject();
                if (!_hasGetSeekThumbnailProvider || underlying == null || _seekThumbnailProviderLoaded)
                    return _seekThumbnailProvider;

                _seekThumbnailProviderLoaded = true;
                var obj = underlying.InvokeV8(_config, "getSeekThumbnailProvider") as IJavaScriptObject;
                if (obj != null)
                    _seekThumbnailProvider = new SeekThumbnailProvider(GrayjayPlugin.GetEnginePlugin(underlying.Engine), obj);
                return _seekThumbnailProvider;
            }
        }

        public bool HasVODEvents() => _hasGetVODEvents;
        public VODEventPager GetVODEvents()
        {
            var underlying = GetUnderlyingObject();
            if (!_hasGetVODEvents || underlying == null)
                return null;

            var plugin = GrayjayPlugin.GetEnginePlugin(underlying.Engine);
            return new VODEventPager(plugin, underlying.InvokeV8<IJavaScriptObject>(_config, "getVODEvents"));

        }
    }
}
