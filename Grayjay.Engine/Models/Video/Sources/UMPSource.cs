using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Grayjay.Engine.Models.Video.Sources
{
    public record struct UMPFormatKey(int Itag, ulong LastModified, string Xtags)
    {
        public static UMPFormatKey Of(int itag, ulong lmt, string? xtags) => new UMPFormatKey(itag, lmt, xtags ?? "");
    }

    public class UMPFormat
    {
        [V8Property("itag")]
        public int Itag { get; set; }
        [V8Property("lastModified", true)]
        public string LastModifiedRaw { get; set; }
        [V8Property("xtags", true)]
        public string Xtags { get; set; }
        [V8Property("mimeType")]
        public string MimeType { get; set; }
        [V8Property("codecs", true)]
        public string Codecs { get; set; }
        [V8Property("bitrate", true)]
        public int Bitrate { get; set; }
        [V8Property("width", true)]
        public int Width { get; set; }
        [V8Property("height", true)]
        public int Height { get; set; }
        [V8Property("fps", true)]
        public int Fps { get; set; }
        [V8Property("audioChannels", true)]
        public int AudioChannels { get; set; }
        [V8Property("audioSampleRate", true)]
        public int AudioSampleRate { get; set; }
        [V8Property("language", true)]
        public string Language { get; set; }
        [V8Property("original", true)]
        public bool IsOriginalAudio { get; set; }
        [V8Property("isDrc", true)]
        public bool IsDrc { get; set; }

        [JsonIgnore]
        public ulong LastModified => ulong.TryParse(LastModifiedRaw, out var v) ? v : 0;

        [JsonIgnore]
        public UMPFormatKey Key => UMPFormatKey.Of(Itag, LastModified, Xtags);

        public bool IsVideo => MimeType?.StartsWith("video/") ?? false;
        public bool IsAudio => MimeType?.StartsWith("audio/") ?? false;

        public string ContainerMimeType
        {
            get
            {
                var mime = MimeType ?? "";
                var index = mime.IndexOf(';');
                return (index >= 0 ? mime.Substring(0, index) : mime).Trim();
            }
        }

        public string CodecName => UMPCodecs.CodecName(Codecs ?? "");

        public string QualityLabel
        {
            get
            {
                if (Height <= 0) return $"itag {Itag}";
                if (Fps > 30) return $"{Height}p{Fps}";
                return $"{Height}p";
            }
        }

        public string VideoLabel => QualityLabel;

        public string AudioLabel
        {
            get
            {
                var parts = new List<string>();
                var lang = (!string.IsNullOrWhiteSpace(Language) && !string.Equals(Language, "Unknown", StringComparison.OrdinalIgnoreCase)) ? Language : null;
                var label = "";
                if (lang != null) label += lang + " ";
                label += Bitrate > 0 ? $"{Bitrate / 1000}kbps" : $"itag {Itag}";
                if (AudioChannels > 2) label += $" {AudioChannels}ch";
                if (IsDrc) label += " (normalized)";
                if (IsOriginalAudio) label += " (original)";
                return label.Trim();
            }
        }

        public override bool Equals(object? obj) => obj is UMPFormat other && other.Key == Key;
        public override int GetHashCode() => Key.GetHashCode();
        public override string ToString() => $"itag={Itag} lmt={LastModified} " + (IsVideo ? $"{Width}x{Height}" : $"{Bitrate}bps");
    }

    public static class UMPCodecs
    {
        public static string CodecName(string codecs)
        {
            var c = codecs.ToLowerInvariant();
            if (c.StartsWith("avc1") || c.StartsWith("avc3")) return "H.264";
            if (c.StartsWith("hev1") || c.StartsWith("hvc1")) return "H.265";
            if (c.StartsWith("av01")) return "AV1";
            if (c.StartsWith("vp9") || c.StartsWith("vp09")) return "VP9";
            if (c.StartsWith("vp8") || c.StartsWith("vp08")) return "VP8";
            if (c.StartsWith("mp4a")) return "AAC";
            if (c.StartsWith("opus")) return "Opus";
            if (c.StartsWith("vorbis")) return "Vorbis";
            if (c.StartsWith("ec-3")) return "EAC3";
            if (c.StartsWith("ac-3")) return "AC3";
            var dot = codecs.IndexOf('.');
            return (dot >= 0 ? codecs.Substring(0, dot) : codecs).Trim();
        }
    }

    public class UMPSource : JSSource, IVideoSource
    {
        public const string CONTAINER = "application/vnd.yt-ump";

        public override string Type => "UMPSource";
        public override bool CanSerialize => false;

        [V8Property("name", true)]
        public string Name { get; set; } = "UMP";
        [V8Property("url")]
        public string Url { get; set; }
        [V8Property("ustreamerConfig")]
        public string UstreamerConfig { get; set; }
        [V8Property("poToken", true)]
        public string PoToken { get; set; }
        [V8Property("videoId", true)]
        public string VideoId { get; set; } = "";
        [V8Property("isLive", true)]
        public bool IsLive { get; set; }
        [V8Property("duration", true)]
        public int Duration { get; set; }
        [V8Property("width", true)]
        public int Width { get; set; }
        [V8Property("height", true)]
        public int Height { get; set; }
        [V8Property("priority", true)]
        public bool Priority { get; set; }
        [V8Property("language", true)]
        public string Language { get; set; }
        [V8Property("original", true)]
        public bool Original { get; set; }

        [V8Property("clientName", true)]
        public int ClientName { get; set; } = 1;
        [V8Property("clientVersion", true)]
        public string ClientVersion { get; set; } = "2.20250923.08.00";
        [V8Property("osName", true)]
        public string OsName { get; set; } = "Windows";
        [V8Property("osVersion", true)]
        public string OsVersion { get; set; } = "10.0";

        [V8Property("videoFormats")]
        public UMPFormat[] VideoFormats { get; set; } = Array.Empty<UMPFormat>();
        [V8Property("audioFormats")]
        public UMPFormat[] AudioFormats { get; set; } = Array.Empty<UMPFormat>();

        public string Container => CONTAINER;
        public string Codec => "";

        public bool HasGetPoToken { get; private set; }

        public UMPSource() { }
        public UMPSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj)
        {
            HasGetPoToken = obj.HasFunction("getPoToken");
        }

        public string? GetPoToken(bool forceRefresh)
        {
            if (!HasGetPoToken || _obj == null)
                return PoToken;
            var result = _obj.InvokeV8(_plugin.Config, "getPoToken", forceRefresh);
            if (result is string str && !string.IsNullOrEmpty(str))
            {
                PoToken = str;
                return str;
            }
            return PoToken;
        }

        public List<UMPVideoFormatSource> GetVideoFormatSources() =>
            VideoFormats.OrderByDescending(x => (long)x.Height * 100000 + x.Bitrate)
                .Select(x => new UMPVideoFormatSource(this, x))
                .ToList();

        public List<UMPAudioFormatSource> GetAudioFormatSources() =>
            AudioFormats.OrderByDescending(x => x.IsOriginalAudio)
                .ThenByDescending(x => x.Bitrate)
                .Select(x => new UMPAudioFormatSource(this, x))
                .ToList();
    }

    public class UMPVideoFormatSource : JSSource, IVideoSource
    {
        public override string Type => "UMPVideoFormatSource";
        public override bool CanSerialize => false;

        [JsonIgnore]
        public UMPSource Parent { get; }
        [JsonIgnore]
        public UMPFormat Format { get; }

        public int Width => Format.Width;
        public int Height => Format.Height;
        public string Container => Format.ContainerMimeType;
        public string Codec => Format.Codecs;
        public string Name => string.IsNullOrEmpty(Format.CodecName) ? Format.VideoLabel : $"{Format.VideoLabel} {Format.CodecName}";
        public int Duration => Parent.Duration;
        public bool Priority => Parent.Priority;
        public bool Original { get => Parent.Original; set { } }
        public string Language { get => Parent.Language; set { } }

        public UMPVideoFormatSource(UMPSource parent, UMPFormat format) : base(parent.GetUnderlyingPlugin(), parent.GetUnderlyingObject())
        {
            Parent = parent;
            Format = format;
        }
    }

    public class UMPAudioFormatSource : JSSource, IAudioSource
    {
        public override string Type => "UMPAudioFormatSource";
        public override bool CanSerialize => false;

        [JsonIgnore]
        public UMPSource Parent { get; }
        [JsonIgnore]
        public UMPFormat Format { get; }

        public string Container => Format.ContainerMimeType;
        public string Codec => Format.Codecs;
        public string Name => string.IsNullOrEmpty(Format.CodecName) ? Format.AudioLabel : $"{Format.AudioLabel} {Format.CodecName}";
        public int Bitrate => Format.Bitrate;
        public int Duration => Parent.Duration;
        public bool Priority => false;
        public bool Original => Format.IsOriginalAudio;
        public string? Language { get => Format.Language ?? "Unknown"; set { } }

        public UMPAudioFormatSource(UMPSource parent, UMPFormat format) : base(parent.GetUnderlyingPlugin(), parent.GetUnderlyingObject())
        {
            Parent = parent;
            Format = format;
        }
    }
}
