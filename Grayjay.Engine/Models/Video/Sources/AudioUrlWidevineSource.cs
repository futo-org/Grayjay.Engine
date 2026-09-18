using Grayjay.Engine.V8;
using Microsoft.ClearScript.JavaScript;

namespace Grayjay.Engine.Models.Video.Sources
{
    public class AudioUrlWidevineSource : AudioUrlSource, IWidevineSource
    {
        public override string Type => "AudioUrlWidevineSource";

        [V8Property("licenseUri")]
        public string LicenseUri { get; set; }

        [V8Property("serviceCertificate", true)]
        public string? ServiceCertificate { get; set; }

        public AudioUrlWidevineSource() { }
        public AudioUrlWidevineSource(GrayjayPlugin plugin, IJavaScriptObject obj) : base(plugin, obj) { }
    }
}
