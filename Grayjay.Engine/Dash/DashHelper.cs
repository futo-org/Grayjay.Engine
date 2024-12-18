using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Grayjay.Engine.Dash
{
    public static class DashHelper
    {
        public static Regex REGEX_DASH_REPRESENTATION = new Regex("<Representation (.*?)>(.*?)<\\/Representation>", RegexOptions.Singleline);
        public static Regex REGEX_DASH_TEMPLATE = new Regex("<SegmentTemplate (.*?)>(.*?)<\\/SegmentTemplate>", RegexOptions.Singleline);
        public static Regex REGEX_DASH_CUE = new Regex("<S .*?t=\"([0-9]*?)\".*?d=\"([0-9]*?)\".*?\\/>", RegexOptions.Singleline);

        private static string? GetTagAttribute(string input, string tagName)
        {
            var match = Regex.Match(input, $"{tagName}=\"(.*?)\"");
            if (match != null && match.Success)
                return match.Groups[1].Value;
            return null;
        }

        public static List<DashRepresentation> GetRepresentations(string dash)
        {
            var representations = REGEX_DASH_REPRESENTATION.Matches(dash);
            if (representations != null && representations.Count > 0)
            {
                var reps = new List<DashRepresentation>();
                foreach (Match representation in representations)
                {
                    var foundTemplate = REGEX_DASH_TEMPLATE.Match(representation.Groups[2].Value);
                    if (foundTemplate == null || !foundTemplate.Success)
                        throw new InvalidDataException("Dash contains no template");
                    var foundTemplateUrl = GetTagAttribute(foundTemplate.Groups[1].Value, "media");
                    var foundCues = REGEX_DASH_CUE.Matches(foundTemplate.Groups[2].Value);
                    if (foundCues == null || foundCues.Count == 0)
                        throw new InvalidDataException("Dash contains no cues");


                    List<DashSegment> segments = new List<DashSegment>();
                    for (int i = 1; i <= foundCues.Count; i++)
                    {
                        var cue = foundCues[i - 1];
                        segments.Add(new DashSegment()
                        {
                            StartTime = int.Parse(cue.Groups[1].Value),
                            DeltaTime = int.Parse(cue.Groups[2].Value),
                            Url = foundTemplateUrl.Replace("$Number$", i.ToString())
                        });
                    }

                    reps.Add(new DashRepresentation()
                    {
                        MimeType = GetTagAttribute(representation.Groups[1].Value, "mimeType"),
                        Codec = GetTagAttribute(representation.Groups[1].Value, "codecs"),
                        InitializationUrl = GetTagAttribute(foundTemplate.Groups[1].Value, "initialization"),
                        MediaTemplateUrl = foundTemplateUrl,
                        Segments = segments
                    });
                }
                return reps;
            }
            else
                return new List<DashRepresentation>();
        }

    }

    public class DashRepresentation
    {
        public string MimeType { get; set; }
        public string Codec { get; set; }
        public string MediaTemplateUrl { get; set; }
        public string InitializationUrl { get; set; }
        public List<DashSegment> Segments { get; set; }
    }
    public class DashSegment
    {
        public string Url { get; set; }
        public int StartTime { get; set; }
        public int DeltaTime { get; set; }
    }
}
