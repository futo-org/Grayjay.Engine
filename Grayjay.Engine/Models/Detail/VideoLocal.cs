using Grayjay.Engine.Models;
using Grayjay.Engine.Models.Feed;
using Grayjay.Engine.Models.General;
using Grayjay.Engine.Models.Ratings;
using Grayjay.Engine.Models.Subtitles;
using Grayjay.Engine.Models.Video.Sources;
using Grayjay.Engine.Models.Video;
using Grayjay.Engine.V8;
using System.Security.Cryptography.X509Certificates;
using PlatformID = Grayjay.Engine.Models.General.PlatformID;
using System.Collections.Generic;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Detail
{
    public class VideoLocal : PlatformVideoDetails, IPlatformContentDetails
    {
        public ContentType ContentType => ContentType.MEDIA;

        public bool IsLocal => true;

        public PlatformVideoDetails VideoDetails { get; set; }

        public List<LocalVideoSource> VideoSources { get; set; } = new List<LocalVideoSource>();
        public List<LocalAudioSource> AudioSources { get; set; } = new List<LocalAudioSource>();
        public List<LocalSubtitleSource> SubtitleSources { get; set; } = new List<LocalSubtitleSource>();

        public string GroupID { get; set; }
        public string GroupType { get; set; }

        //PlatformContent
        public override PlatformID ID { get { return VideoDetails.ID; } set { } }
        public override DateTime DateTime => VideoDetails?.DateTime ?? DateTime.MinValue;
        public override string Name { get { return VideoDetails.Name; } set { } }
        public override PlatformAuthorLink Author { get { return VideoDetails.Author; } set { } }
        public override string Url { get { return VideoDetails.Url; } set { } }
        public override string ShareUrl { get { return VideoDetails.ShareUrl; } set { } }


        //PlatformVideo
        public override string Description { get { return VideoDetails.Description; } set { } }
        public override IRating Rating { get { return VideoDetails.Rating; } set { } }
        public override VideoDescriptor Video { 
            get {
                return new UnMuxedVideoDescriptor()
                {
                    VideoSources = VideoSources.ToArray(),
                    AudioSources = AudioSources.ToArray()
                };
            } set { } }
        public override VideoDescriptor? Preview { get { return VideoDetails.Preview; } set { } }
        public override IVideoSource? Live { get { return VideoDetails.Live; } set { } }

        [JsonIgnore]
        public override SubtitleSource[] Subtitles { get { return VideoDetails.Subtitles; } set { } }

        public VideoLocal() { }
        public VideoLocal(PlatformVideoDetails details)
        {
            VideoDetails = details;
        }

        public void DeleteFiles()
        {
            foreach(var video in VideoSources)
            {
                try
                {
                    File.Delete(video.FilePath);
                }
                catch(Exception ex)
                {
                    Logger.Error<VideoLocal>($"Failed to delete download", ex);
                }
            }
            foreach(var audio in AudioSources)
            {
                try
                {
                    File.Delete(audio.FilePath);
                }
                catch (Exception ex)
                {
                    Logger.Error<VideoLocal>($"Failed to delete download", ex);
                }
            }
        }
    }
}
