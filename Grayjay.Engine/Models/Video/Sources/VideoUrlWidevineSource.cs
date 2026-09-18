using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class VideoUrlWidevineSource : VideoUrlSource, IWidevineSource
    {
        public override string Type => "VideoUrlWidevineSource";

        [V8Property("licenseUri")]
        public string LicenseUri { get; set; }

        [V8Property("serviceCertificate", true)]
        public string? ServiceCertificate { get; set; }

        public VideoUrlWidevineSource() { }
        public VideoUrlWidevineSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj) { }
    }
}
